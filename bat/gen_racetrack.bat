@echo off
dotnet build || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_worldspace RG_Racetrack %RANDOM% Spacer Racetrack
powershell -Command "& { $f = '%LOCALAPPDATA%\Starfield\Plugins.txt'; $e = '*RG_Racetrack.esm'; $lines = if (Test-Path $f) { Get-Content $f } else { @() }; if ($lines -notcontains $e) { Add-Content $f $e; Write-Host 'Added RG_Racetrack.esm to Plugins.txt' } else { Write-Host 'RG_Racetrack.esm already in Plugins.txt' } }"
