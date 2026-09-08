param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [ValidateSet(8, 10)]
    [int]$RuntimeMajor = 8
)

$ErrorActionPreference = 'Stop'

$requiredAssemblies = @(
    'System.IO.Compression.Brotli.dll'
)

$runtimePattern = "^Microsoft\.NETCore\.App (?<version>${RuntimeMajor}\.0\.\d+) \[(?<root>.+)\]$"
$runtimes = @(
    dotnet --list-runtimes | ForEach-Object {
        if ($_ -match $runtimePattern) {
            [pscustomobject]@{
                Version = [version]$Matches['version']
                Directory = Join-Path $Matches['root'] $Matches['version']
            }
        }
    } | Sort-Object Version -Descending
)

if ($runtimes.Count -eq 0) {
    throw "Geen Microsoft.NETCore.App $RuntimeMajor.0-runtime gevonden. Installeer .NET $RuntimeMajor voordat de bundle wordt gebouwd."
}

$runtimeDirectory = $runtimes[0].Directory
if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
}

foreach ($assemblyName in $requiredAssemblies) {
    $source = Join-Path $runtimeDirectory $assemblyName
    if (-not (Test-Path $source)) {
        throw "Vereiste .NET runtime-DLL ontbreekt: $source"
    }

    Copy-Item $source (Join-Path $Destination $assemblyName) -Force
    Write-Host "Runtime-DLL toegevoegd: $assemblyName uit $runtimeDirectory"
}
