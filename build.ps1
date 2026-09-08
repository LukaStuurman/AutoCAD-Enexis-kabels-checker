param(
    [string]$AutoCADManagedDir = "",
    [string]$AutoCAD2027ManagedDir = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$coreCheck = Join-Path $root "tools\VerifyExcelCases\VerifyExcelCases.csproj"
$isolationCheck = Join-Path $root "tools\VerifyExcelIsolation\VerifyExcelIsolation.csproj"
$acadProject = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\Enexis.KabelChecker.AutoCAD.csproj"
$excelWorkerProject = Join-Path $root "src\Enexis.KabelChecker.ExcelWorker\Enexis.KabelChecker.ExcelWorker.csproj"
$bundleTemplate = Join-Path $root "build\PackageContents.xml"
$dist = Join-Path $root "dist"
$bundle = Join-Path $dist "EnexisKabelChecker.bundle"
$contentsNet8 = Join-Path $bundle "Contents\Windows\net8"
$contentsNet10 = Join-Path $bundle "Contents\Windows\net10"
$excelNet8 = Join-Path $contentsNet8 "excel"
$excelNet10 = Join-Path $contentsNet10 "excel"

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

Write-Host "1/8 Controleer rekenengine tegen Excel-referentiegevallen..."
dotnet run --project $coreCheck -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Excel-referentiecontrole mislukt." }

Write-Host "2/8 Bouw geïsoleerde Excel-worker voor .NET 8..."
dotnet build $excelWorkerProject -c $Configuration -f net8.0-windows
if ($LASTEXITCODE -ne 0) { throw "Build van .NET 8 Excel-worker mislukt." }

$workerOutputNet8 = Join-Path $root "src\Enexis.KabelChecker.ExcelWorker\bin\$Configuration\net8.0-windows"
$workerOutputNet10 = Join-Path $root "src\Enexis.KabelChecker.ExcelWorker\bin\$Configuration\net10.0-windows"

Write-Host "3/8 Test ClosedXML-isolatie voor .NET 8..."
dotnet run --project $isolationCheck -c $Configuration -f net8.0-windows -- (Join-Path $workerOutputNet8 "Enexis.KabelChecker.ExcelWorker.dll")
if ($LASTEXITCODE -ne 0) { throw ".NET 8 ClosedXML-isolatietest mislukt." }

Write-Host "4/8 Bouw .NET 8-plugin voor AutoCAD 2025/2026 (R25.x)..."
Invoke-AutoCADBuild -Series "R25" -ManagedDir $AutoCADManagedDir

Write-Host "5/8 Bouw geïsoleerde Excel-worker voor .NET 10..."
dotnet build $excelWorkerProject -c $Configuration -f net10.0-windows
if ($LASTEXITCODE -ne 0) { throw "Build van .NET 10 Excel-worker mislukt." }

Write-Host "6/8 Test ClosedXML-isolatie voor .NET 10..."
dotnet run --project $isolationCheck -c $Configuration -f net10.0-windows -- (Join-Path $workerOutputNet10 "Enexis.KabelChecker.ExcelWorker.dll")
if ($LASTEXITCODE -ne 0) { throw ".NET 10 ClosedXML-isolatietest mislukt." }

Write-Host "7/8 Bouw .NET 10-plugin voor AutoCAD 2027+ (R26.0+)..."
Invoke-AutoCADBuild -Series "R26" -ManagedDir $AutoCAD2027ManagedDir

Write-Host "8/8 Maak gecombineerde .bundle..."
if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
New-Item -ItemType Directory -Path $excelNet8 -Force | Out-Null
New-Item -ItemType Directory -Path $excelNet10 -Force | Out-Null
Copy-Item $bundleTemplate (Join-Path $bundle "PackageContents.xml") -Force

$outputNet8 = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\bin\$Configuration\net8.0-windows"
$outputNet10 = Join-Path $root "src\Enexis.KabelChecker.AutoCAD\bin\$Configuration\net10.0-windows"

# De AutoCAD-plugin zelf bevat bewust GEEN ClosedXML-reference meer. ClosedXML en
# alle transitive dependencies staan in een submap en worden uitsluitend via een
# eigen AssemblyLoadContext geladen. Zo botst Autodesk's eigen ClosedXML-versie niet.
Copy-Item (Join-Path $outputNet8 "*.dll") $contentsNet8 -Force
Copy-Item (Join-Path $outputNet10 "*.dll") $contentsNet10 -Force
Copy-Item (Join-Path $workerOutputNet8 "*.dll") $excelNet8 -Force
Copy-Item (Join-Path $workerOutputNet10 "*.dll") $excelNet10 -Force

foreach ($target in @(
    @{ Name = ".NET 8"; Root = $contentsNet8; Excel = $excelNet8 },
    @{ Name = ".NET 10"; Root = $contentsNet10; Excel = $excelNet10 }
)) {
    foreach ($required in @(
        "Enexis.KabelChecker.AutoCAD.dll",
        "Enexis.KabelChecker.Core.dll"
    )) {
        if (-not (Test-Path (Join-Path $target.Root $required))) {
            throw "$required ontbreekt in de $($target.Name)-bundle."
        }
    }

    if (Test-Path (Join-Path $target.Root "ClosedXML.dll")) {
        throw "ClosedXML.dll mag niet in de AutoCAD root van de $($target.Name)-bundle staan."
    }

    foreach ($required in @(
        "Enexis.KabelChecker.ExcelWorker.dll",
        "ClosedXML.dll"
    )) {
        if (-not (Test-Path (Join-Path $target.Excel $required))) {
            throw "$required ontbreekt in de geïsoleerde Excel-map van de $($target.Name)-bundle."
        }
    }
}

Write-Host ""
Write-Host "Klaar: $bundle"
Write-Host "AutoCAD 2025/2026 gebruikt Contents\Windows\net8; AutoCAD 2027+ gebruikt Contents\Windows\net10."
Write-Host "Excel-dependencies staan geïsoleerd in de submap 'excel' om Autodesk assembly-conflicten te voorkomen."
Write-Host "Kopieer deze map naar %PROGRAMFILES%\Autodesk\ApplicationPlugins om de plugin automatisch te laden."
