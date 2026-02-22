@echo off
dotnet build || goto :eof
call deploy_cleanslate.bat || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_worldspace cleanslate 0 Spacer ScienceOutpost
