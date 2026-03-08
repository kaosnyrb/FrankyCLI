@echo off
dotnet build || goto :eof
call .\bat\deploy_cleanslate.bat outlaws02 || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_worldspace outlaws02 -1 Spacer SmallIndustryBase
pushd "C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data"
StarfieldPluginBridge.exe outlaws02.esm
popd
