#!/bin/sh
curl -O https://raw.githubusercontent.com/karashiiro/DalamudPluginProjectTemplate/master/.github/workflows/dotnet.yml
mkdir .github
mkdir .github/workflows
mv dotnet.yml .github/workflows