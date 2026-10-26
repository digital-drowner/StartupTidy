@echo off
setlocal
cd /d "%~dp0"
set "TIDY_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TIDY_CSC%" set "TIDY_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TIDY_CSC%" exit /b 1
if not exist scratch mkdir scratch
"%TIDY_CSC%" /nologo /target:winexe /out:scratch\TidyTestDummy.exe /reference:System.Windows.Forms.dll Dummy.cs
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:Tests /out:scratch\Tests.exe /reference:System.Windows.Forms.dll /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs Tests.cs
if errorlevel 1 exit /b 1
scratch\Tests.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:UiRegression /out:scratch\UiRegression.exe /reference:System.Windows.Forms.dll /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs UiRegression.cs
if errorlevel 1 exit /b 1
scratch\UiRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:BulkRegression /out:scratch\BulkRegression.exe /reference:System.Windows.Forms.dll /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs BulkRegression.cs
if errorlevel 1 exit /b 1
scratch\BulkRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:IconRegression /out:scratch\IconRegression.exe /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs IconRegression.cs
if errorlevel 1 exit /b 1
scratch\IconRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:ThemeRegression /out:scratch\ThemeRegression.exe /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs ThemeRegression.cs
if errorlevel 1 exit /b 1
scratch\ThemeRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:LayoutRegression /out:scratch\LayoutRegression.exe /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs LayoutRegression.cs
if errorlevel 1 exit /b 1
scratch\LayoutRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:ImportantRegression /out:scratch\ImportantRegression.exe /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs ImportantRegression.cs
if errorlevel 1 exit /b 1
scratch\ImportantRegression.exe "%CD%\scratch"
if errorlevel 1 exit /b 1
"%TIDY_CSC%" /nologo /target:exe /main:PickerFilterRegression /out:scratch\PickerFilterRegression.exe /resource:..\assets\app.ico,StartupTidy.App.ico /resource:..\assets\tray-inactive.ico,StartupTidy.Inactive.ico /resource:..\assets\tray-active.ico,StartupTidy.Active.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll ..\StartupTidy.cs ..\Theme.cs PickerFilterRegression.cs
if errorlevel 1 exit /b 1
scratch\PickerFilterRegression.exe "%CD%\scratch"
exit /b %errorlevel%
