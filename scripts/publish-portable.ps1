# Publishes a self-contained portable folder and zips it for GitHub Releases.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "publish\portable"
$zip = Join-Path $root "publish\FixArea-portable-win-x64.zip"

dotnet publish (Join-Path $root "src\FixArea.App\FixArea.App.csproj") `
  -c Release -r win-x64 -o $out

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -CompressionLevel Optimal

$exe = Get-Item (Join-Path $out "FixArea.App.exe")
$zipInfo = Get-Item $zip
Write-Host ""
Write-Host "Folder: $out"
Write-Host "Zip:    $zip ($([math]::Round($zipInfo.Length/1MB,1)) MB)"
Write-Host "Upload the ZIP to GitHub Releases. Users extract and run FixArea.App.exe."
