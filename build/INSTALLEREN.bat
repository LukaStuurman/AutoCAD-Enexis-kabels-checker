@echo off
setlocal
title Enexis Kabel Checker - installeren

powershell -NoProfile -Command "$p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent()); if ($p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 0 } else { exit 1 }"
if errorlevel 1 (
  echo Beheerdersrechten worden aangevraagd...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

set "SOURCE=%~dp0EnexisKabelChecker.bundle"
set "PLUGINROOT=%ProgramFiles%\Autodesk\ApplicationPlugins"
set "TARGET=%PLUGINROOT%\EnexisKabelChecker.bundle"

if not exist "%SOURCE%\PackageContents.xml" (
  echo.
  echo FOUT: EnexisKabelChecker.bundle staat niet naast INSTALLEREN.bat.
  echo Pak eerst de volledige release-ZIP uit en voer daarna dit bestand uit.
  echo.
  pause
  exit /b 1
)

if not exist "%PLUGINROOT%" mkdir "%PLUGINROOT%"
if exist "%TARGET%" rmdir /S /Q "%TARGET%"

xcopy "%SOURCE%\*" "%TARGET%\" /E /I /Y >nul
if errorlevel 1 (
  echo.
  echo FOUT: installatie is mislukt.
  echo Doelmap: %TARGET%
  echo.
  pause
  exit /b 1
)

echo.
echo ================================================
echo Enexis Kabel Checker is geinstalleerd.
echo ================================================
echo.
echo 1. Sluit AutoCAD volledig als het nog open staat.
echo 2. Start AutoCAD 2025 of 2026 opnieuw.
echo 3. Typ: ENEXISKABELCHECK
echo.
echo Installatiemap:
echo %TARGET%
echo.
pause
