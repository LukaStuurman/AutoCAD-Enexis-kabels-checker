param(
    [string]$AutoCADManagedDir = "C:\Program Files\Autodesk\AutoCAD 2025",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$coreCheck = Join-Path $root "tools\VerifyExcelCases\VerifyExcelCases.csproj"
$acadProject = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\Enexis.KabelChecker.AutoCAD.csproj"
$bundleTemplate = Join-Path $root "build\PackageContents.xml"
$runtimeDependencyScript = Join-Path $root "build\CopyDotNetRuntimeDependencies.ps1"
$dist = Join-Path $root "dist"
$bundle = Join-Path $dist "EnexisKabelChecker.bundle"
$contents = Join-Path $bundle "Contents\Windows"

Write-Host "1/3 Controleer rekenengine tegen Excel-referentiegevallen..."
dotnet run --project $coreCheck -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Excel-referentiecontrole mislukt." }

Write-Host "2/3 Bouw AutoCAD-plugin..."
dotnet build $acadProject -c $Configuration -p:AutoCADManagedDir="$AutoCADManagedDir"
if ($LASTEXITCODE -ne 0) { throw "Build van AutoCAD-plugin mislukt." }

Write-Host "3/3 Maak .bundle..."
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $contents -Force | Out-Null
Copy-Item $bundleTemplate (Join-Path $bundle "PackageContents.xml") -Force

$output = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\bin\$Configuration\net8.0-windows"
# Neem naast de plugin/core ook ClosedXML en alle NuGet runtime-afhankelijkheden mee.
# AutoCAD assemblies staan op Private=false en komen daardoor niet in deze outputmap terecht.
Copy-Item (Join-Path $output "*.dll") $contents -Force

# AutoCAD laadt plugins in een eigen assembly-context. Sommige .NET 8 framework-DLL's
# worden daardoor niet automatisch gevonden door transitive Excel-dependencies.
& $runtimeDependencyScript -Destination $contents

if (-not (Test-Path (Join-Path $contents "System.IO.Compression.Brotli.dll"))) {
    throw "System.IO.Compression.Brotli.dll ontbreekt in de bundle."
}

Write-Host ""
Write-Host "Klaar: $bundle"
Write-Host "Kopieer deze map naar %PROGRAMDATA%\Autodesk\ApplicationPlugins om de plugin automatisch te laden."
