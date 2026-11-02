@echo off
rem Build SoundLevel with the C# compiler that ships with Windows (.NET Framework).
rem No .NET SDK required.
setlocal

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [error] csc.exe not found. 64-bit Windows is required.
  pause
  exit /b 1
)

set SPEECH=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll
if not exist "%SPEECH%" (
  echo [error] System.Speech.dll not found in the GAC.
  pause
  exit /b 1
)

set SRC=^
  "%~dp0src\Interop.cs" "%~dp0src\Native.cs" "%~dp0src\AudioEngine.cs" ^
  "%~dp0src\SpeechClip.cs" "%~dp0src\Calibration.cs" "%~dp0src\Autostart.cs" ^
  "%~dp0src\Theme.cs" "%~dp0src\Controls.cs" "%~dp0src\MainForm.cs" ^
  "%~dp0src\GuiMain.cs" "%~dp0src\SelfTest.cs"

set REFS=/r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:"%SPEECH%"

echo [1/2] building SoundLevel.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ %REFS% /main:SoundLevel.GuiMain ^
  /out:"%~dp0SoundLevel.exe" %SRC%
if errorlevel 1 goto fail

echo [2/2] building selftest.exe ...
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ %REFS% /main:SoundLevel.SelfTest ^
  /out:"%~dp0selftest.exe" %SRC%
if errorlevel 1 goto fail

echo.
echo done: %~dp0SoundLevel.exe
pause
exit /b 0

:fail
echo.
echo build failed.
pause
exit /b 1
