param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src/ReSeq/ReSeq.csproj"
$output = Join-Path $root "artifacts/ReSeq-$Runtime-portable"

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $output

Get-ChildItem -Path $output -Filter "*.pdb" -File | Remove-Item -Force

Write-Host "Portable build: $output"
