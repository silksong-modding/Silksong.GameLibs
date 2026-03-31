# Silksong.GameLibs

Bundler Nuget package for silksong game libraries. Strips and publicizes game libraries for local and CI development.

## Versioning

This package uses semantic versioning and follows the naming scheme `(package version)-silksong(silksong version)`, for example
`1.0.0-silksong1.0.28324`. This allows the package to seamlessly support multiple game versions. When a new version of the game
is released, we can simply create a new version on nuget targeting that game version, without impacting existing consumers. When
the content of the package itself is changed (e.g. a new file being added to the publicizer) then the package version is bumped.