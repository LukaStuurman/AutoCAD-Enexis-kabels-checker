@echo off
setlocal
title Enexis Kabel Checker - verwijderen

powershell -NoProfile -Command "$p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent()); if ($p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 0 } else { exit 1 }"
if errorlevel 1 (
  echo Beheerdersrechten worden aangevraagd...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "TARGET=%ProgramFiles%\Autodesk\ApplicationPlugins\EnexisKabelChecker.bundle"

if exist "%TARGET%" (
  rmdir /S /Q "%TARGET%"
  echo.
  echo Enexis Kabel Checker is verwijderd.
) else (
  echo.
  echo De plugin was niet geinstalleerd in:
  echo %TARGET%
)

echo.
echo Start AutoCAD opnieuw om de wijziging door te voeren.
echo.
pause
