## Build instructions

This section is for people wanting to generate the package for a new game version; if you are just creating mods this section is not relevant
to you.

To build this package, you should have ready:

- The version(s) of the game you want to mod
- The dotnet CLI to build
- [Highly recommended] The [Nuke global tool](https://nuke.build/docs/getting-started/installation/) with shell completion
  configured

This package uses [Nuke](https://nuke.build) as a build system. The Nuke global tool is not required, but is recommended for
additional features such as shell completion and secret management. If you don't want to use the global tool, you can use one
of the bootstrap scripts (`build.cmd` for windows, `build.ps1` for powershell, or `build.sh` for mac/linux) in place of `nuke`
in all of the example commands below.

### Preparing game files for an installed patch

1. Note down the game version and related Steam manifest IDs. Add them to `build/SilksongVersionInfo.cs` so they can be used in
   builds.
   - The easiest way to get manifest versions is to go to [SteamDB](https://steamdb.info/app/1030300/patchnotes/), find the patch
     corresponding to the update date, and then grab the new manifest ids from the bottom of each depot's section of the patch page.
2. Copy the contents of the Managed folder to `ref/(game version)`, for example, `ref/1.0.28324`

Adding/removing assemblies in the build and/or publicization can be done by adjusting the SystemFiles and GameFiles item groups
in the csproj.

### Retrieving files for old patches using DepotDownloader

In the event of missing a patch or tranferring ownership, retrieving references for old files will be necessary. In such
cases, [DepotDownloader](https://github.com/SteamRE/DepotDownloader) can be used to retrieve them from Steam.

1. Collect the app, depot, and manifest ID
    1. App ID is `1030300`
    2. Depot ID is `1030301` for Windows, `1030302` for Mac, and `1030303` for Linux. For consistency, using the Windows
       depot is probably desirable even if you are not on Windows.
    3. Manifest ID is documented in `build-all.sh` for each patch.
2. Create a refs folder for the target version: `mkdir refs/<target version>`
3. Run depot downloader 
   ```sh
   depotdownloader -app 1030300 \
       -depot 1030301 \
       -manifest <manifest of target version> \
       -dir ref/<target version> \
       -filelist depotdownloader-file-filter.txt \
       -qr
   ```
4. Move the files up to the root level of the refs directory and delete the `Hollow Knight Silksong_Data` and `.DepotDownloader`
   directories.

### Cleaning up

Build artifacts can be cleaned up with `nuke clean`.

### Building packages

Packages can be built using `nuke package`. Optionally, you can specify a specific version or list of versions to limit the
scope of build, such as:
```sh
nuke package --target-silksong-versions 1.0.29980 1.0.30000
```

The `--dry-run` flag can be specified to indicate what actions will be taken without performing them.

### Publishing packages

Packages can be published using `nuke publish`. Similar to the build, you can specify a specific version or list of versions
to publish, and use the `--dry-run` flag to run through the prerequisite checks and action plan without executing it. A NuGet
API key with the appropriate publishing scope is also required; see the next section for additional details

The publishing target has several guard rails to prevent incorrect publishes; it will:

1. Clean and rebuild old build artifacts
2. Check that you are on the main branch and that it is up to date with origin/main
3. Check that you have no uncommitted changes
4. Run `dotnet nuget push --skip-duplicate` for all generated packages using an API key you provide

#### API Key Handling

NuGet API keys should be treated with care to prevent malicious actors from hijacking the update feed. Nuke has
built-in secret management through the global tool. The recommended steps to manage the API are as follows:

1. Create an empty json file at `build/parameters.local-secrets.json` and copy the `$schema` from `parameters.json`. This
   file is gitignored so you can use your own secrets
2. Run `nuke :secrets local-secrets`, choose a password, and use the interface to enter your API key, then save.
3. In future runs of `nuke publish`, provide the flag `--profile local-secrets` to load your encrypted secrets from the file.
   You will be prompted to re-enter the chosen password

Refer to [Nuke's documentation](https://nuke.build/docs/global-tool/secrets/) for additional detail.

There are also less-secure ways to enter the API key, should you choose to do so:

- In a `parameters.<profile>.json` entry using plaintext instead Nuke's encryption mechanism
- With the `--nuget-api-key` flag
- In an environment variable, `NUGET_API_KEY` or `NUKE_NUGET_API_KEY`