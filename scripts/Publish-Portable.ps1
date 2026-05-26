param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/ReSeq/ReSeq.csproj"
$artifacts = Join-Path $root "artifacts"
$output = Join-Path $root "artifacts/ReSeq-$Runtime-portable"
$zipPath = Join-Path $root "artifacts/ReSeq-$Runtime-portable.zip"
$stableExePath = Join-Path $root "artifacts/ReSeq.exe"

[xml]$projectXml = Get-Content -LiteralPath $project
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0"
}
$versionedExePath = Join-Path $root "artifacts/ReSeq-$version-$Runtime.exe"

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Copy-WithReplace {
    param(
        [string]$Source,
        [string]$Destination
    )

    $destinationDirectory = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
    }

    $temporaryPath = "$Destination.tmp"
    Remove-PathIfExists $temporaryPath
    Copy-Item -LiteralPath $Source -Destination $temporaryPath -Force

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }

    Move-Item -LiteralPath $temporaryPath -Destination $Destination -Force
}

if (-not (Test-Path -LiteralPath $artifacts)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}

Remove-PathIfExists $output
Remove-PathIfExists $zipPath
Remove-PathIfExists $stableExePath
Get-ChildItem -LiteralPath $artifacts -Filter "ReSeq-*-$Runtime.exe" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $output

Get-ChildItem -Path $output -Filter "*.pdb" -File | Remove-Item -Force

$publishedExePath = Join-Path $output "ReSeq.exe"
if (-not (Test-Path -LiteralPath $publishedExePath)) {
    throw "Publish did not create expected executable: $publishedExePath"
}

Copy-WithReplace -Source $publishedExePath -Destination $stableExePath
Copy-WithReplace -Source $publishedExePath -Destination $versionedExePath
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Portable build: $output"
Write-Host "Portable zip:   $zipPath"
Write-Host "Single exe:     $stableExePath"
Write-Host "Versioned exe:  $versionedExePath"
