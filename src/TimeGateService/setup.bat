@ECHO OFF

REM The following directory is for .NET 4.0
set DOTNETFX2=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319
set PATH=%PATH%;%DOTNETFX2%

if not exist "C:\Program Files\Obscure\" mkdir "C:\Program Files\Obscure"
XCOPY /y ".\TimeGateService.exe" "C:\Program Files\Obscure\" 


echo (Un)Installing Time Gate Service...
echo ---------------------------------------------------
InstallUtil "C:\Program Files\Obscure\TimeGateService.exe"
echo ---------------------------------------------------
pause
echo Done.