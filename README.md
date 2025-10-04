﻿# Silksong.GameLibs

Bundler Nuget package for silksong game libraries. Strips and publicizes game libraries for local and CI development.

## Versioning

This package uses semantic versioning and follows the naming scheme `(package version)-silksong(silksong version)`, for example
`1.0.0-silksong1.0.28324`. This allows the package to seamlessly support multiple game versions. When a new version of the game
is released, we can simply create a new version on nuget targeting that game version, without impacting existing consumers. When
the content of the package itself is changed (e.g. a new file being added to the publicizer) then the package version is bumped.

## Build instructions

This section is for people wanting to generate the package for a new game version; if you are just creating mods this section is not relevant
to you.

To build this package, you should have ready:

- The version(s) of the game you want to mod
- The dotnet CLI to build (Visual Studio's build environment will not work because it does not support netstandard2.1)

### Building for all supported versions

A build script, `build-all.sh`, is provided to build the package for all supported Silksong versions. This script will automatically build for each version listed in the script. You can optionally specify the build configuration (e.g. Release or Debug) using the `-c` or `--configuration` flag (default is Debug):

```sh
./build-all.sh -c Release
```

This will invoke `dotnet build` for each version, setting the required `TargetSilksongVersion` property and the specified configuration.

### Building for a single version manually

If you want to build for a specific Silksong version, you must set the `TargetSilksongVersion` property manually:

```sh
dotnet build -c Release -p:TargetSilksongVersion=1.0.28324
```

Replace `1.0.28324` with the desired version. The build will fail if `TargetSilksongVersion` is not set.

### Preparing game files

1. Note down the game version and related Steam depot IDs. Add them in a comment in the csproj so that future builders can
   retrieve that version to build against it again in the future.
2. Copy the contents of the Managed folder to `ref/(game version)`, for example, `ref/1.0.28324`

Adding/removing assemblies in the build and/or publicization can be done by adjusting the SystemFiles and GameFiles item groups
in the csproj.

## Publishing packages

A publish script, `publish-all.sh`, is provided to automate publishing all generated NuGet packages. It will:

1. Check that you are on the main branch and that it is up to date with origin/main
2. Check that you have no uncommitted changes
3. Clean all `.nupkg` files from the project's `bin/Release` folder
4. Run `build-all.sh` with Release configuration
5. Run `dotnet nuget push --skip-duplicate` for all generated packages using an API key you provide

**Note:** You must run `publish-all.sh` from the solution directory so that the relative `bin/Release` path is correct.

### API Key Handling

- You can provide your NuGet API key as a command line argument: `./publish-all.sh <NUGET_API_KEY>`
- If not provided, the script will look for a `.nuget_api_key` file in the project directory and use its contents.
- If you provide the key as an argument, it will be saved to `.nuget_api_key` for future use (this file is gitignored).

Usage:

```sh
./publish-all.sh [NUGET_API_KEY]
```

Replace `[NUGET_API_KEY]` with your NuGet API key, or omit to use the saved key.