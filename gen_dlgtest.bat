@echo off
call .\deploy_cleanslate.bat dlgtest || goto :eof
dotnet build || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_dlgtest dlgtest || goto :eof
pushd "C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data"
StarfieldPluginBridge.exe dlgtest.esm
popd
