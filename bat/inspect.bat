@echo off
REM Usage: inspect.bat <RecordType> <EditorID_or_FormID>
REM Example: inspect.bat SurfaceBlock OverlayBlock
REM Example: inspect.bat Worldspace 0x00000C36
REM Example: inspect.bat list dummy
C:\Git\FrankyCLI\bin\Debug\net6.0\FrankyCLI.exe dummy gen_inspect dummy %1 %2
pause
