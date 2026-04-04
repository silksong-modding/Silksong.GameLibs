using QRCoder;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CDN = SteamKit2.CDN;

namespace _build;


[Serializable]
public class SteamClientException : Exception
{
    public SteamClientException() { }
    public SteamClientException(string message) : base(message) { }
    public SteamClientException(string message, Exception inner) : base(message, inner) { }
}

internal class SteamClientWrapper
{
    public const uint APP_ID = SilksongVersionInfo.STEAM_APPID;

    public record CdnAuthInfo(byte[] DepotKey, string CdnAuthToken);

    private SteamClient Client { get; }
    private SteamUser User { get; }
    private SteamApps Apps { get; }
    private SteamContent Content { get; }

    private CDN.Client CdnClient { get; }
    private CDN.Server CdnProxyServer { get; set; }
    private CDN.Server CdnServer { get; set; }
    private readonly SemaphoreSlim cdnSemaphore = new(1, 1);

    // key is depot id. values are lazy tasks because getoradd may run the code twice and cannot handle async code.
    // use GetAuthForDepotAsync. See https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
    private readonly ConcurrentDictionary<uint, Lazy<Task<CdnAuthInfo>>> loginInfos = [];

    public SteamClientWrapper()
    {
        Client = new();
        User = Client.GetHandler<SteamUser>();
        Apps = Client.GetHandler<SteamApps>();
        Content = Client.GetHandler<SteamContent>();

        CdnClient = new(Client);
    }

    /// <summary>
    /// Asynchronously establishes a connection to the server and attempts to log in using the specified credentials.
    /// If either the username or password is null, Steam Guard QR login will be used instead.
    /// </summary>
    /// <remarks>If the connection or authentication fails, the returned task will complete with an exception.</remarks>
    /// <param name="username">The optional user name to use for authentication.</param>
    /// <param name="password">The optional password associated with the specified user name.</param>
    /// <returns>A task that represents the asynchronous connect and login operation.</returns>
    public Task ConnectAndLoginAsync(string username, string password)
    {
        return Task.Run(() => ConnectAndLoginInternal(username, password));
    }

    public Task LogOutAsync()
    {
        return Task.Run(User.LogOff);
    }

    public async Task<SilksongVersionInfo> GetProductInfoAsync()
    {
        SteamApps.PICSTokensCallback tokenResult = await Apps.PICSGetAccessTokens(APP_ID, null);
        ulong token = tokenResult.AppTokens.TryGetValue(APP_ID, out ulong t)
            ? t
            : throw new SteamClientException("Could not get PICS token for Silksong, do you own the game?");

        SteamApps.PICSRequest request = new SteamApps.PICSRequest(APP_ID, token);
        AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet productInfoResult = await Apps.PICSGetProductInfo(request, null);
        SteamApps.PICSProductInfoCallback.PICSProductInfo appInfo = productInfoResult.Results.FirstOrDefault()
            ?.Apps.Select(a => a.Value)
            .FirstOrDefault(a => a.ID == APP_ID);
        if (appInfo == null)
        {
            throw new SteamClientException("Steam did not return any app info for Silksong.");
        }
        LogKeyValues(appInfo.KeyValues);

        KeyValue depots = appInfo.KeyValues["depots"];
        ulong winManifestId = depots[SilksongVersionInfo.STEAM_DEPOT_ID_WINDOWS.ToString()]["manifests"]["public"]["gid"].AsUnsignedLong();
        ulong macManifestId = depots[SilksongVersionInfo.STEAM_DEPOT_ID_MAC.ToString()]["manifests"]["public"]["gid"].AsUnsignedLong();
        ulong linuxManifestId = depots[SilksongVersionInfo.STEAM_DEPOT_ID_LINUX.ToString()]["manifests"]["public"]["gid"].AsUnsignedLong();
        return new()
        {
            Version = "UNKNOWN",
            WindowsManifestId = winManifestId,
            MacManifestId = macManifestId,
            LinuxManifestId = linuxManifestId,
        };
    }

    private void LogKeyValues(KeyValue kv, int indent = 0)
    {
        Log.Verbose("{Indent}{Key}={Value}", new string(' ', indent), kv.Name, kv.Value);
        foreach (KeyValue child in kv.Children)
        {
            LogKeyValues(child, indent + 2);
        }
    }

    public async Task<DepotManifest> GetManifestAsync(uint depotId, ulong manifestId)
    {
        CdnAuthInfo authInfo = await GetAuthForDepotAsync(depotId);
        ulong manifestRequestCode = await Content.GetManifestRequestCode(depotId, APP_ID, manifestId);
        return await CdnClient.DownloadManifestAsync(
            depotId,
            manifestId,
            manifestRequestCode,
            CdnServer,
            authInfo.DepotKey,
            CdnProxyServer,
            authInfo.CdnAuthToken
        );
    }

    public async Task DownloadFileAsync(uint depotId, DepotManifest.FileData file, Stream destination, CancellationToken ct = default)
    {
        CdnAuthInfo authInfo = await GetAuthForDepotAsync(depotId);
        destination.SetLength((long)file.TotalSize);
        foreach (DepotManifest.ChunkData chunk in file.Chunks)
        {
            ct.ThrowIfCancellationRequested();

            byte[] chunkBytesUncompressed = ArrayPool<byte>.Shared.Rent((int)chunk.UncompressedLength);
            int written = await CdnClient.DownloadDepotChunkAsync(
                depotId,
                chunk,
                CdnServer,
                chunkBytesUncompressed,
                authInfo.DepotKey,
                CdnServer,
                authInfo.CdnAuthToken
            );
            if (written == 0)
            {
                throw new SteamClientException($"Unable to retrieve chunk {Convert.ToHexString(chunk.ChunkID)}");
            }
            destination.Seek((long)chunk.Offset, SeekOrigin.Begin);
            await destination.WriteAsync(chunkBytesUncompressed.AsMemory(0, written), ct);
            ArrayPool<byte>.Shared.Return(chunkBytesUncompressed);
        }
    }

    private void ConnectAndLoginInternal(string username, string password)
    {
        CallbackManager manager = new(Client);

        bool continueEventLoop = true;

        manager.Subscribe<SteamClient.ConnectedCallback>(async args =>
        {
            Log.Information("Logging in to steam...");
            AuthPollResult pollResponse;
            if (username != null && password != null)
            {
                pollResponse = await AuthenticateWithCredentialsAsync(username, password);
            }
            else
            {
                pollResponse = await AuthenticateWithQrAsync();
            }

            Log.Information("Logging in as {User}", pollResponse.AccountName);
            User.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
                LoginID = 0x4E554B45 // The ascii characters of "NUKE" in hex, to ensure uniqueness against default logged in steam client sessions
            });
        });
        manager.Subscribe<SteamClient.DisconnectedCallback>(args =>
        {
            throw new SteamClientException("Lost connection to the server");
        });

        manager.Subscribe<SteamUser.LoggedOnCallback>(args =>
        {
            if (args.Result != EResult.OK)
            {
                throw new SteamClientException($"Unable to log in to Steam: {args.Result} / {args.ExtendedResult}");
            }
            Log.Information("Successfully logged in!");
            continueEventLoop = false;
        });

        Client.Connect();
        while (continueEventLoop)
        {
            manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }
    }

    private async Task<AuthPollResult> AuthenticateWithCredentialsAsync(string user, string password)
    {
        CredentialsAuthSession session = await Client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
        {
            Username = user,
            Password = password,
            Authenticator = new UserConsoleAuthenticator()
        });

        return await session.PollingWaitForResultAsync();
    }

    private async Task<AuthPollResult> AuthenticateWithQrAsync()
    {
        QrAuthSession session = await Client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());
        session.ChallengeURLChanged = () =>
        {
            Log.Information("Steam has refreshed the challenge URL");
            DrawQRCode(session);
        };
        DrawQRCode(session);

        return await session.PollingWaitForResultAsync();
    }

    private void DrawQRCode(QrAuthSession authSession)
    {
        Log.Information("Challenge URL: {Challenge}", authSession.ChallengeURL);

        // Encode the link as a QR code
        using QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
        using AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);
        string qrCodeAsAsciiArt = qrCode.GetGraphic(1, drawQuietZones: false);

        Log.Information("Use the Steam Mobile App to sign in via QR code");
        // intentionally doesn't use serilog; don't want to populate it to disk plus formatting is wonky
        Console.WriteLine(qrCodeAsAsciiArt);
    }

    private async Task EnsureCdnAsync()
    {
        // do the cheap check to see if we need to bother claiming the lock at all
        if (CdnServer == null)
        {
            await cdnSemaphore.WaitAsync();
            try
            {
                // check again to make sure we didn't get race conditioned between the first check and claiming the lock
                if (CdnServer == null)
                {
                    IReadOnlyCollection<CDN.Server> servers = await Content.GetServersForSteamPipe();
                    CdnProxyServer = servers.Where(s => s.UseAsProxy).FirstOrDefault();
                    CdnServer = servers.Where(s =>
                    {
                        bool isEligible = s.AllowedAppIds.Length == 0 || s.AllowedAppIds.Contains(APP_ID);
                        return isEligible && (s.Type == "SteamCache" || s.Type == "CDN");
                    }).OrderBy(s => s.WeightedLoad).First();
                }
            }
            finally
            {
                cdnSemaphore.Release();
            }
        }
    }

    private async Task<CdnAuthInfo> GetAuthForDepotAsync(uint depotId)
    {
        await EnsureCdnAsync();
        Lazy<Task<CdnAuthInfo>> lazyTask = loginInfos.GetOrAdd(depotId, new Lazy<Task<CdnAuthInfo>>(async () =>
        {
            SteamApps.DepotKeyCallback depotKeyResult = await Apps.GetDepotDecryptionKey(depotId, APP_ID);
            SteamContent.CDNAuthToken cdnAuthToken = await Content.GetCDNAuthToken(APP_ID, depotId, CdnServer.Host);
            return new CdnAuthInfo(depotKeyResult.DepotKey, cdnAuthToken.Token);
        }));
        return await lazyTask.Value;
    }
}
