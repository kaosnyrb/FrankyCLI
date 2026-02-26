@echo off
call .\deploy_cleanslate.bat outlaws02 || goto :eof
dotnet build || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_quest outlaws02 || goto :eof
pushd "C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data"
StarfieldPluginBridge.exe outlaws02.esm
popd
