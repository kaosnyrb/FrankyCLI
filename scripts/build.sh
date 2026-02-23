#!/usr/bin/env bash
# Build Retrograde.Library — safe, no piping, absolute path
dotnet build "c:/Git/FrankyCLI/Retrograde.Library/Retrograde.csproj" --no-restore 2>&1
