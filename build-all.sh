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
#
# version 1.0.28714
# note: win and mac have the patch for CVE-2025-59489 on 2025-10-03.
#       linux did not get the patch and is on the release from 2025-09-24.
#       the CVE patch contains no other changes aside from the patch.
#   windows: 5977483240701257214 
#   mac:     7917356342743942630
#   linux:   1617544312110692774
#
# version 1.0.28891
#   windows: 3690203822520536668 
#   mac:     2374057204384257562
#   linux:   5954103139200615141
#
# version 1.0.29242:
#   windows: 426651197780377263
#   mac:     2058007571598677908
#   linux:   8078874762924599313
#
# version 1.0.29315:
#   windows: 3545882420322545098 
#   mac:     7345001466169537628 
#   linux:   4349246050376532986
#
# version 1.0.29909
#   windows: 3853982342899707391
#   mac:     8626896570586771768
#   linux:   2218100490558811317
#
# version 1.0.29926
#   windows: 3703686001292550981
#   mac:     4837712425280970616
#   linux:   6891565886978421564
#
# version 1.0.29980
#   windows: 468692862190470536
#   mac:     4899622998101152532
#   linux:   7780372375671997910

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
  "1.0.28714"
  "1.0.28891"
  "1.0.29242"
  "1.0.29315"
  "1.0.29909"
  "1.0.29926"
  "1.0.29980"
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
