#!/usr/bin/env sh
set -eu
dotnet run --project tools/Roentgen.CampaignLab -- "$@"
