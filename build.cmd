@echo off
rem Rebuilds "Switch it up.exe" using the C# compiler that ships with Windows.
rem Run make-icon.ps1 first if app.ico is missing.
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll ^
  /win32icon:"%~dp0app.ico" ^
  /out:"%~dp0Switch it up.exe" "%~dp0MonitorSwitch.cs" "%~dp0Ui.cs"
