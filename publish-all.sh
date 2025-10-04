#!/usr/bin/env bash

set -e

# Usage: ./publish-all.sh [NUGET_API_KEY]
# If NUGET_API_KEY is not provided, will read from .nuget_api_key if present.
# If provided, will save to .nuget_api_key for future use.

API_KEY_FILE=".nuget_api_key"

if [[ -n $(git status --porcelain) ]]; then
  echo "Error: You have uncommitted changes. Please commit or stash them before publishing."
  exit 1
fi

current_branch=$(git rev-parse --abbrev-ref HEAD)
if [ "$current_branch" != "main" ]; then
  echo "Error: You must be on the main branch to publish. Current branch: $current_branch"
  exit 1
fi

git fetch origin main
if ! git diff --quiet HEAD origin/main; then
  echo "Error: Local main branch is not up to date with origin/main. Please pull/rebase first."
  exit 1
fi

echo "On main branch, up to date with origin/main, and no uncommitted changes."

# get API key from input or file
if [ "$#" -ge 1 ]; then
  NUGET_API_KEY="$1"
  echo "$NUGET_API_KEY" > "$API_KEY_FILE"
  echo "NuGet API key saved to $API_KEY_FILE for future use."
elif [ -f "$API_KEY_FILE" ]; then
  NUGET_API_KEY=$(<"$API_KEY_FILE")
else
  echo "Error: NuGet API key not provided and $API_KEY_FILE not found."
  echo "Usage: $0 [NUGET_API_KEY]"
  exit 1
fi

BIN_RELEASE_DIR="bin/Release"
if [ -d "$BIN_RELEASE_DIR" ]; then
  echo "Cleaning nupkg files from $BIN_RELEASE_DIR..."
  rm -f "$BIN_RELEASE_DIR"/*.nupkg
fi

./build-all.sh -c Release

dotnet nuget push "$BIN_RELEASE_DIR"/*.nupkg \
	--api-key "$NUGET_API_KEY" \
	--source https://api.nuget.org/v3/index.json \
	--skip-duplicate

echo "Publish complete."
