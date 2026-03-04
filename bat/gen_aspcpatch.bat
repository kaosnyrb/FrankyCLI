@echo off
dotnet build || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_aspcpatch || goto :eof
