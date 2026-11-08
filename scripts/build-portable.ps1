<#
.SYNOPSIS
  Produit les ZIP portables de Selecto AI (emballage Site, sans installation) : SelectoAI-<arch>-portable-<version>.zip.

.DESCRIPTION
  Pour chaque architecture : dotnet publish autonome (-p:Edition=Site) vers dist\publish\Site-<arch> (ou reutilise la publication
  existante avec -SkipPublish), puis zip (sans .pdb) dans dist\portable. Le zip contient un dossier SelectoAI-<arch> avec
  SelectoAI.exe et, si -ReadMe est donne, un LISEZ-MOI.txt. Aucune signature ici : signer SelectoAI.exe avant de zipper si besoin.

.EXAMPLE
  .\scripts\build-portable.ps1 -Arch x64
#>
param(
    [ValidateSet('x64', 'arm64', 'Both')][string]$Arch = 'Both',
    [switch]$SkipPublish,
    [switch]$ReadyToRun,
    [string]$ReadMe = '',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'SelectBar\SelectBar.csproj'
$publishRoot = Join-Path $root 'dist\publish'
$outDir = Join-Path $root 'dist\portable'

$csproj = Get-Content -Raw -Path $project
if ($csproj -notmatch '<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>') { throw "Version introuvable dans $project" }
$version = $Matches[1]
$arches = if ($Arch -eq 'Both') { @('x64', 'arm64') } else { @($Arch) }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($a in $arches) {
    $pub = Join-Path $publishRoot "Site-$a"
    Write-Host "=== portable / $a ===" -ForegroundColor Cyan
    if (-not $SkipPublish) {
        if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
        $dn = @('publish', $project, '-c', $Configuration, '-p:Edition=Site', '-r', "win-$a", '--self-contained', 'true', '-o', $pub)
        if ($ReadyToRun) { $dn += '-p:PublishReadyToRun=true' }
        & dotnet @dn
        if ($LASTEXITCODE -ne 0) { throw "Echec de dotnet publish pour Site/$a" }
    }
    if (-not (Test-Path (Join-Path $pub 'SelectoAI.exe'))) { throw "SelectoAI.exe absent de $pub (publication manquante ?)" }

    $zipPath = Join-Path $outDir "SelectoAI-$a-portable-$version.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    $fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            $folder = "SelectoAI-$a/"
            $base = (Resolve-Path $pub).Path.TrimEnd('\')
            Get-ChildItem -Path $base -Recurse -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
                $rel = $_.FullName.Substring($base.Length).TrimStart('\') -replace '\\', '/'
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $folder + $rel, [System.IO.Compression.CompressionLevel]::Optimal)
            }
            if ($ReadMe -and (Test-Path $ReadMe)) {
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $ReadMe, $folder + 'LISEZ-MOI.txt', [System.IO.Compression.CompressionLevel]::Optimal)
            }
        } finally { $zip.Dispose() }
    } finally { $fs.Dispose() }
    '  {0}  {1:N1} Mo' -f (Split-Path -Leaf $zipPath), ((Get-Item $zipPath).Length / 1MB) | Write-Host
}
Write-Host "Dossier : $outDir"
