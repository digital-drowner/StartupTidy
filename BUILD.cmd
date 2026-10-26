@echo off
setlocal
cd /d "%~dp0"
set "TIDY_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TIDY_CSC%" set "TIDY_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TIDY_CSC%" (
  echo .NET Framework compiler not found.
  exit /b 1
)
"%TIDY_CSC%" /nologo /target:winexe /optimize+ /win32manifest:app.manifest /win32icon:assets\app.ico /resource:assets\app.ico,StartupTidy.App.ico /resource:assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:assets\tray-active.ico,StartupTidy.Active.ico /out:StartupTidy.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll StartupTidy.cs Theme.cs
if errorlevel 1 exit /b 1
echo Built StartupTidy.exe
