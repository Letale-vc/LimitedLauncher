# Builds a single-file Release exe into publish/ (ignored by git).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'publish'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'src') -c Release -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done: $(Join-Path $out 'LimitedLauncher.exe')"
