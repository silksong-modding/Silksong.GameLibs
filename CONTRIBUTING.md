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

### Common Parameters

The following parameters are supported in all build targets except for Clean:

- `--target-silksong-versions`: The versions of Silksong to run the command for. Multiple versions can be specified separated
                                by spaces, e.g. `--target-silksong-versions 1.0.29980 1.0.30000`. If not specified, the command
                                will run for all known versions.
- `--dry-run`: Treats the build as a dryrun build. Actions will be logged, but not performed. This is helpful for testing and
               verification purposes

### Secret Parameters

Certain parameters contain highly sensitive information like NuGet tokens or Steam passwords. Rather than enter/store these
in plaintext, you can use Nuke's secret management tooling. The recommended way to do this is as follows:

1. Create an empty json file at `.nuke/parameters.local-secrets.json` and copy the `$schema` from `parameters.json`. This
   file is gitignored so you can use your own secrets
2. Run `nuke :secrets local-secrets`, choose a password, and use the interface to enter your secrets, then save.
3. In future runs requiring secrets, provide the flag `--profile local-secrets` to load your encrypted secrets from the file.
   You will be prompted to re-enter the chosen password

Refer to [Nuke's documentation](https://nuke.build/docs/global-tool/secrets/) for additional detail.

There are also less-secure ways to enter secrets, should you choose to do so:

- With command line flags as usual (preferred for future CI use cases)
- In a `parameters.<profile>.json` entry using plaintext instead Nuke's encryption mechanism
- In an environment variable, such as `NUGET_API_KEY` or `NUKE_NUGET_API_KEY`

### Preparing game files

If adding support for a new patch, the first step is to record the version and manifest IDs for each depot at that version
in `build/SilksongVersionInfo.cs`. The easiest way to retrieve these is from [SteamDB](https://steamdb.info/app/1030300/patchnotes/);
find the patch corresponding to the release date and copy down the new manifest IDs from the bottom of each depot's secion
of the patch page.

Game files can be downloaded using `nuke download-depots`. This will prompt you to log into Steam. You must own the game on
Steam to use this tooling. By default, a Steam Guard QR code will be generated to log in without typing your username and password.
The secret parameters `--steam-user` and `--steam-password` can be used to log in by credentials, which is especially useful if
you do not have 2FA/Steam Guard. See the section about secret management above for best practices.

If you have the game on another platform like GOG you can also manually copy the content of the `Managed` folder to the 
`ref/<game version>`, preferably with the Windows copy of the game for consistency. However, it is generally harder or impossible
to access very old versions of the game from non-Steam platforms.

Adding/removing assemblies in the build and/or publicization can be done by adjusting the SystemFiles and GameFiles item groups
in the csproj.

### Building packages

Packages can be built using `nuke package`. Packaging requires game files to be available in the `ref` folder. Steam users
can accomplish this with the `download-depots` target as detailed above.

### Publishing packages

Packages can be published using `nuke publish`. A NuGet API key (`--nuget-api-key`) with the appropriate publishing scope 
is also required; see the section about secret management above for best practices.

The publishing target has several guard rails to prevent incorrect publishes. Before publishing it will:

1. Clean and rebuild old build artifacts
2. Check that you are on the main branch and that it is up to date with origin/main
3. Check that you have no uncommitted changes

### Cleaning up

Build artifacts can be cleaned up with `nuke clean`.

### Combining targets

Nuke supports chaining targets and will ensure that they execute in the correct order. For example, to download depots, build,
and publish from a fresh state, you might do `nuke clean download-depots publish` and all 3 targets will be executed. You can
use `nuke --plan` to visualize the order targets will be executed.
