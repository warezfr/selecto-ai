<#
.SYNOPSIS
  Produit les deux emballages de Selecto AI (Site et Store) depuis la même base de code, dans dist\.

.DESCRIPTION
  Pour chaque emballage et chaque architecture : dotnet publish autonome (self-contained) ; si la publication autonome échoue
  (pack d'exécution indisponible hors ligne par exemple), repli sur une publication dépendante du framework .NET 8.
  Sorties : dist\SelectoAI-Site-win-x64, dist\SelectoAI-Site-win-arm64, dist\SelectoAI-Store-win-x64, dist\SelectoAI-Store-win-arm64 (exécutable SelectoAI.exe dans chacun). Site : installateur Inno Setup, ZIP portable, mise à jour par latest.json. Store : MSIX (scripts\build-msix.ps1), mises à jour par le Store.
  Le dossier de compilation intermédiaire est propre à chaque emballage (obj\<Configuration>\<Edition>\<rid>) : pas de mélange.
  Avec -Installer, appelle ensuite scripts\build-installer.ps1 pour chaque architecture (emballage Site) : installateurs Inno Setup dans dist\installers
  (ce script publie lui-même, de façon autonome, dans dist\publish\Site-<arch> ; -ReadyToRun est transmis).

.EXAMPLE
  .\scripts\build-editions.ps1
  .\scripts\build-editions.ps1 -Editions Store -Runtimes win-arm64 -FrameworkDependent
  .\scripts\build-editions.ps1 -Installer
#>
param(
    [string]$Configuration = 'Release',
    [ValidateSet('Site', 'Store')][string[]]$Editions = @('Site', 'Store'),
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [switch]$FrameworkDependent,
    [switch]$Installer,
    [switch]$ReadyToRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'SelectBar\SelectBar.csproj'
$dist = Join-Path $root 'dist'

function Publish-One([string]$edition, [string]$rid, [string]$out, [bool]$selfContained) {
    $sc = if ($selfContained) { 'true' } else { 'false' }
    & dotnet publish $project -c $Configuration -r $rid --self-contained $sc "-p:Edition=$edition" -o $out
    return ($LASTEXITCODE -eq 0)
}

$results = @()
foreach ($edition in $Editions) {
    foreach ($rid in $Runtimes) {
        $out = Join-Path $dist "SelectoAI-$edition-$rid"
        if (Test-Path $out) { Remove-Item -Recurse -Force $out }
        Write-Host "=== $edition / $rid ===" -ForegroundColor Cyan

        $mode = 'autonome'
        $ok = $false
        if (-not $FrameworkDependent) { $ok = Publish-One $edition $rid $out $true }
        if (-not $ok) {
            if (-not $FrameworkDependent) { Write-Warning "Publication autonome impossible pour $edition/$rid : repli sur une publication dépendante du framework." }
            if (Test-Path $out) { Remove-Item -Recurse -Force $out }
            $mode = 'dépendante du framework .NET 8'
            $ok = Publish-One $edition $rid $out $false
        }
        if (-not $ok) { throw "Échec de la publication $edition/$rid" }
        $results += "$edition / $rid ($mode) -> $out"
    }
}

Write-Host ''
Write-Host 'Éditions produites :' -ForegroundColor Green
$results | ForEach-Object { Write-Host "  $_" }

if ($Installer) {
    $installerScript = Join-Path $PSScriptRoot 'build-installer.ps1'
    foreach ($edition in ($Editions | Where-Object { $_ -eq 'Site' })) {
        foreach ($rid in $Runtimes) {
            $arch = $rid -replace '^win-', ''
            $extra = @{}
            if ($ReadyToRun) { $extra['ReadyToRun'] = $true }
            & $installerScript -Arch $arch -Configuration $Configuration @extra
            if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Échec de build-installer.ps1 pour $edition/$arch" }
        }
    }
}
