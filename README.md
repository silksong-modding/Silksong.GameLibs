# Silksong.GameLibs

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

- The version of the game you want to mod
- The dotnet CLI to build (Visual Studio's build environment will not work because it does not support netstandard2.1)

Then do the following:

1. Note down the game version and related Steam depot IDs. Add them in a comment in the csproj so that future builders can
   retrieve that version to build against it again in the future.
2. Update the TargetSilksongVersion property in the csproj.
3. Copy the contents of the Managed folder to `ref/(game version)`, for example, `ref/1.0.28324`
4. Run `dotnet build`. This will strip and publicize the configured files and bundle everything to a nuget package.

Adding/removing assemblies in the build and/or publicization can be done by adjusting the SystemFiles and GameFiles item groups
in the csproj.