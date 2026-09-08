param(
    [string]$AutoCADManagedDir = "",
    [string]$AutoCAD2027ManagedDir = "",
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
$contentsNet8 = Join-Path $bundle "Contents\Windows\net8"
$contentsNet10 = Join-Path $bundle "Contents\Windows\net10"

function Invoke-AutoCADBuild {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("R25", "R26")]
        [string]$Series,
        [string]$ManagedDir = ""
    )

    $buildArgs = @(
        "build",
        $acadProject,
        "-c", $Configuration,
        "-p:AutoCADApiSeries=$Series"
    )

    if ([string]::IsNullOrWhiteSpace($ManagedDir)) {
        $buildArgs += "-p:UseAutoCADNuGet=true"
    } else {
        $buildArgs += "-p:UseAutoCADNuGet=false"
        $buildArgs += "-p:AutoCADManagedDir=$ManagedDir"
    }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build van AutoCAD-plugin voor $Series mislukt."
    }
}

Write-Host "1/4 Controleer rekenengine tegen Excel-referentiegevallen..."
dotnet run --project $coreCheck -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Excel-referentiecontrole mislukt." }

Write-Host "2/4 Bouw .NET 8-plugin voor AutoCAD 2025/2026 (R25.x)..."
Invoke-AutoCADBuild -Series "R25" -ManagedDir $AutoCADManagedDir

Write-Host "3/4 Bouw .NET 10-plugin voor AutoCAD 2027+ (R26.0+)..."
Invoke-AutoCADBuild -Series "R26" -ManagedDir $AutoCAD2027ManagedDir

Write-Host "4/4 Maak gecombineerde .bundle..."
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $contentsNet8 -Force | Out-Null
New-Item -ItemType Directory -Path $contentsNet10 -Force | Out-Null
Copy-Item $bundleTemplate (Join-Path $bundle "PackageContents.xml") -Force

$outputNet8 = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\bin\$Configuration\net8.0-windows"
$outputNet10 = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\bin\$Configuration\net10.0-windows"

# Iedere AutoCAD-generatie krijgt zijn eigen plugin- en dependencyset. Zo komt
# een .NET 8 runtime-DLL nooit naast de .NET 10-build terecht (of andersom).
Copy-Item (Join-Path $outputNet8 "*.dll") $contentsNet8 -Force
Copy-Item (Join-Path $outputNet10 "*.dll") $contentsNet10 -Force

& $runtimeDependencyScript -Destination $contentsNet8 -RuntimeMajor 8
& $runtimeDependencyScript -Destination $contentsNet10 -RuntimeMajor 10

foreach ($target in @(
    @{ Name = ".NET 8"; Path = $contentsNet8 },
    @{ Name = ".NET 10"; Path = $contentsNet10 }
)) {
    foreach ($required in @(
        "Enexis.KabelChecker.AutoCAD.dll",
        "Enexis.KabelChecker.Core.dll",
        "ClosedXML.dll",
        "System.IO.Compression.Brotli.dll"
    )) {
        if (-not (Test-Path (Join-Path $target.Path $required))) {
            throw "$required ontbreekt in de $($target.Name)-bundle."
        }
    }
}

Write-Host ""
Write-Host "Klaar: $bundle"
Write-Host "AutoCAD 2025/2026 gebruikt Contents\Windows\net8; AutoCAD 2027+ gebruikt Contents\Windows\net10."
Write-Host "Kopieer deze map naar %PROGRAMFILES%\Autodesk\ApplicationPlugins om de plugin automatisch te laden."
