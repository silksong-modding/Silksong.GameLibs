#!/usr/bin/env bash

# Steam depots:
#   windows: 1030301
#   mac:     1030302
#   linux:   1030303
#
# version 1.0.28324 manifests:
#   windows: 3229726349000518284
#   mac:     1365730835793684614
#   linux:   8384590172287463475
#
# version 1.0.28497 manifests:
#   windows: 539129767115354441
#   mac:     8670159430480702509
#   linux:   6701825740120558137
#
# version 1.0.28561 manifests:
#   windows: 8642535143474926050
#   mac:     9022715293716759452
#   linux:   6373658714389144408
#
# version 1.0.28650 manifests:
#   windows: 3900764848237536293
#   mac:     7832939953657548180
#   linux:   7495630131038458486

set -e

# Default configuration
CONFIGURATION="Debug"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

versions=(
  "1.0.28324"
  "1.0.28497"
  "1.0.28561"
  "1.0.28650"
)

for version in "${versions[@]}"; do
  echo "Building for Silksong version $version with configuration $CONFIGURATION..."
  dotnet build -c "$CONFIGURATION" -p:TargetSilksongVersion=$version
  if [ $? -ne 0 ]; then
    echo "Build failed for version $version"
    exit 1
  fi
done

echo "All versions built successfully!"