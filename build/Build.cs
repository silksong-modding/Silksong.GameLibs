using _build;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Serilog;
using SteamKit2;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

partial class Build : NukeBuild
{
    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository Repository;

    public static int Main() => Execute<Build>(x => x.Package);

    [Parameter("Target Silksong versions",
        ValueProviderType = typeof(SilksongVersionInfo),
        ValueProviderMember = nameof(SilksongVersionInfo.AllVersionStrings))]
    readonly SilksongVersionInfo[] TargetSilksongVersions;

    [Parameter("Whether to treat the build as a dryrun. Actions will be logged but not performed.")]
    readonly bool DryRun;

    [Secret, Parameter("The NuGet API key to use for publishing")]
    readonly string NuGetApiKey;

    [Secret, Parameter("Steam username for downloading game files in DepotDownload target. If either username or password is absent, QR login will be used.")]
    readonly string SteamUser;
    [Secret, Parameter("Steam password for downloading game files in DepotDownload target. If either username or password is absent, QR login will be used.")]
    readonly string SteamPassword;

    readonly AbsolutePath BinDir = RootDirectory / "bin";
    readonly AbsolutePath ObjDir = RootDirectory / "obj";
    readonly AbsolutePath DepotStagingDir = TemporaryDirectory / "depots";

    Target Clean => _ => _
        .Before(DownloadDepots, Package, Publish)
        .Executes(() =>
        {
            BinDir.CreateOrCleanDirectory();
            ObjDir.CreateOrCleanDirectory();
            DepotStagingDir.CreateOrCleanDirectory();
        });

    Target Package => _ => _
        .Description("Builds NuGet packages for the specified game versions. If no versions are specified, all versions will be build.")
        .Executes(() =>
        {
            IEnumerable<SilksongVersionInfo> targetVersions = GetTargetVersions();
            Log.Information("Packaging for target versions {Versions}", string.Join(", ", targetVersions));
            if (DryRun)
            {
                Log.Information("Skipping build in dryrun mode");
                return;
            }

            DotNetTasks.DotNetPack(_ => _
                .SetProject(Solution.Silksong_GameLibs.RelativePath)
                .SetConfiguration("Release")
                .CombineWith(targetVersions, (_, v) => _
                    .SetProperty("BaseIntermediateOutputPath", $"{ObjDir / v}/")
                    .SetProperty("BaseOutputPath", $"{BinDir / v}/")
                    .SetProperty("TargetSilksongVersion", v)),
                degreeOfParallelism: 8
            );
        });

    Target Publish => _ => _
        .Description("Publishes NuGet packages for the specified game versions with guardrails. If no versions are specified, all versions will published.")
        .DependsOn(Clean, Package)
        .Requires(() => NuGetApiKey)
        .Executes(() =>
        {
            Assert.True(GitTasks.GitHasCleanWorkingCopy(), "Error: You have uncommitted changes. Please commit or stash them before publishing.");
            Assert.True(Repository.IsOnMainBranch(), $"Error: You must be on the main branch to publish. Current branch: {Repository.Branch}");

            GitTasks.Git("fetch origin main");
            GitTasks.Git("diff --quiet HEAD origin/main", exitHandler: p =>
            {
                if (p.ExitCode != 0)
                {
                    Assert.Fail("Error: Local main branch is not up to date with origin/main. Please pull/rebase first.");
                }
                return null;
            });

            IEnumerable<SilksongVersionInfo> targetVersions = GetTargetVersions();
            Log.Information("On main branch, up to date with origin/main, and no uncommitted changes.");
            Log.Information("Publishing for target versions {Versions}", string.Join(", ", targetVersions));

            if (DryRun)
            {
                Log.Information("Skipping publish in dryrun mode");
                return;
            }
            IEnumerable<AbsolutePath> packages = targetVersions.Select(v => BinDir / v).SelectMany(x => x.GlobFiles("**/*.nupkg"));
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetApiKey)
                .SetSkipDuplicate(true)
                .CombineWith(packages, (_, p) => _
                    .SetTargetPath(p)),
                degreeOfParallelism: 8
            );
        });

    Target DownloadDepots => _ => _
        .Description("Downloads game files for the specified game versions. If no versions are specified, all versions will be downloaded.")
        .Before(Package)
        .Executes(async () =>
        {
            IEnumerable<SilksongVersionInfo> targetVersions = GetTargetVersions();
            Log.Information("Downloading game files for target versions {Versions}", string.Join(", ", targetVersions));

            SteamClientWrapper clientWrapper = new();

            await clientWrapper.ConnectAndLoginAsync(SteamUser, SteamPassword);

            uint depotId = SilksongVersionInfo.STEAM_DEPOT_ID_WINDOWS;

            if (DryRun)
            {
                Log.Information("All downloads will be skipped in dryrun mode");
            }

            await Parallel.ForEachAsync(targetVersions, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (v, ct) =>
            {
                AbsolutePath targetDir = RootDirectory / "ref" / v;
                if (!IsEmptyOrAbsentDirectory(targetDir))
                {
                    Log.Warning("{TargetPath} exists and is not empty, skipping download", targetDir);
                    return;
                }

                AbsolutePath intermediateDir = DepotStagingDir / v;
                intermediateDir.CreateOrCleanDirectory();

                DepotManifest manifest = await clientWrapper.GetManifestAsync(depotId, v.WindowsManifestId);
                IEnumerable<DepotManifest.FileData> validFiles = manifest.Files
                    .Where(x =>
                        !x.Flags.HasFlag(EDepotFileFlag.Directory)
                        && !x.Flags.HasFlag(EDepotFileFlag.Symlink)
                    );
                foreach (DepotManifest.FileData file in validFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    // normalize to unix-like file names
                    file.FileName = file.FileName.Replace('\\', '/');
                    if (ManifestPathMatcher().IsMatch(file.FileName))
                    {
                        string relativePath = Path.GetRelativePath("Hollow Knight Silksong_Data/Managed", Path.TrimEndingDirectorySeparator(file.FileName));
                        AbsolutePath intermediatePath = intermediateDir / relativePath;
                        Log.Information("{Version}: Downloading {File} to {Path}", v, relativePath, intermediatePath);

                        if (DryRun)
                        {
                            continue;
                        }

                        await clientWrapper.DownloadFileAsync(depotId, file, intermediatePath.ToFileInfo(), ct);
                    }
                }

                Log.Information("{Version}: Copying {Intermediate} to {Target}", v, intermediateDir, targetDir);
                intermediateDir.Copy(targetDir, ExistsPolicy.DirectoryMerge, createDirectories: true);
            });

            await clientWrapper.LogOutAsync();
        });

    private IEnumerable<SilksongVersionInfo> GetTargetVersions()
    {
        return TargetSilksongVersions != null && TargetSilksongVersions.Length > 0
            ? TargetSilksongVersions
            : SilksongVersionInfo.AllVersions;
    }

    private bool IsEmptyOrAbsentDirectory(AbsolutePath path)
    {
        if (path.FileExists())
        {
            // file is not a directory!
            return false;
        }

        if (!path.DirectoryExists())
        {
            // doesn't exist
            return true;
        }

        if (path.GetFiles().Any() || path.GetDirectories().Any())
        {
            // has stuff in it
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^Hollow Knight Silksong_Data/Managed/.+$")]
    private static partial Regex ManifestPathMatcher();
}
