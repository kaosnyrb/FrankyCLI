@echo off
dotnet build || goto :eof
call .\deploy_cleanslate.bat outlaws02 || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_worldspace outlaws02 0 Spacer SmallIndustryBase
