<#
.SYNOPSIS
  Publie Selecto AI (autonome, emballage Site) et compile les installateurs Inno Setup (x64 / arm64).

.DESCRIPTION
  Pour chaque architecture : dotnet publish autonome (-p:Edition=Site) vers dist\publish\Site-<arch>, puis ISCC.exe sur installer\SelectoAI.iss.
  Sorties dans dist\installers (installateur PRINCIPAL) : SelectoAI-Setup-<arch>-<version>.exe, SHA256SUMS.txt, latest.json.
  Signature (facultative, jamais de secret dans le depot) : SELECTO_SIGN_PFX + SELECTO_SIGN_PASSWORD, ou SELECTO_SIGN_THUMBPRINT.
  Sans ces variables, l'installateur est produit NON SIGNE (SmartScreen affichera "editeur inconnu").

.PARAMETER Arch
  x64, arm64 ou Both (defaut).
.PARAMETER ReadyToRun
  Ajoute -p:PublishReadyToRun=true (demarrage plus rapide, publication plus grosse).
.PARAMETER SkipPublish
  Reutilise dist\publish\Site-<arch> deja present.
.PARAMETER InstallInno
  Installe Inno Setup 6 avec winget (winget install -e --id JRSoftware.InnoSetup) s'il est absent. Jamais fait sans ce parametre.
.PARAMETER DownloadBaseUrl
  Dossier public des installateurs (HTTPS). Sert a fabriquer l'URL de chaque installateur dans latest.json. Defaut : https://github.com/warezfr/selecto-ai/releases/download/v<version>/ (entree Site ; l'entree de compatibilite Demo des clients 3.x garde https://selecto.span.observer/downloads/<fichier>)
.PARAMETER Notes
  Texte facultatif (FR) affiche dans la notification de mise a jour ; ecrit dans latest.json (champ notes).

.EXAMPLE
  .\scripts\build-installer.ps1 -Arch arm64
  .\scripts\build-installer.ps1 -InstallInno -ReadyToRun
#>
param(
    [ValidateSet('x64', 'arm64', 'Both')][string]$Arch = 'Both',
    [switch]$ReadyToRun,
    [switch]$SkipPublish,
    [switch]$InstallInno,
    [string]$Configuration = 'Release',
    [string]$DownloadBaseUrl = '',
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$legacyBase = 'https://selecto.span.observer/downloads/'   # les clients 3.x n'acceptent que cet hote (le site redirige vers GitHub)
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'SelectBar\SelectBar.csproj'
$iss = Join-Path $root 'installer\SelectoAI.iss'
$icon = Join-Path $root 'SelectBar\Assets\SelectoAI.ico'
$publishRoot = Join-Path $root 'dist\publish'
# Installateur principal (Inno Setup) : dist\installers. L'installateur WPF optionnel (build-setup.ps1) sort dans dist\installers-animated.
$outDir = Join-Path $root 'dist\installers'

function Find-Iscc {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kits) {
        $found = Get-ChildItem -Path $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

function Get-SignArguments {
    # Retourne $null si aucune variable de signature n'est definie.
    if ($env:SELECTO_SIGN_PFX) {
        $a = @('sign', '/fd', 'SHA256', '/tr', 'http://timestamp.digicert.com', '/td', 'SHA256', '/f', $env:SELECTO_SIGN_PFX)
        if ($env:SELECTO_SIGN_PASSWORD) { $a += @('/p', $env:SELECTO_SIGN_PASSWORD) }
        return $a
    }
    if ($env:SELECTO_SIGN_THUMBPRINT) {
        return @('sign', '/fd', 'SHA256', '/tr', 'http://timestamp.digicert.com', '/td', 'SHA256', '/sha1', $env:SELECTO_SIGN_THUMBPRINT)
    }
    return $null
}

function Sign-File([string]$file, [string]$signtool, $signArgs) {
    & $signtool @signArgs $file
    if ($LASTEXITCODE -ne 0) { throw "Echec de la signature de $file" }
}

# --- Version (lue dans SelectBar.csproj) ---
$csproj = Get-Content -Raw -Path $project
if ($csproj -notmatch '<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>') { throw "Version introuvable dans $project" }
$version = $Matches[1]
if ([string]::IsNullOrWhiteSpace($DownloadBaseUrl)) { $DownloadBaseUrl = "https://github.com/warezfr/selecto-ai/releases/download/v$version/" }
if (-not $DownloadBaseUrl.StartsWith('https://')) { throw "DownloadBaseUrl doit etre en HTTPS : $DownloadBaseUrl" }
$downloadBase = $DownloadBaseUrl.TrimEnd('/') + '/'

# --- Inno Setup ---
$iscc = Find-Iscc
if (-not $iscc) {
    if ($InstallInno) {
        Write-Host 'Installation d''Inno Setup 6 via winget...' -ForegroundColor Cyan
        & winget install -e --id JRSoftware.InnoSetup
        if ($LASTEXITCODE -ne 0) { throw 'winget n''a pas pu installer Inno Setup.' }
        $iscc = Find-Iscc
    }
    if (-not $iscc) {
        throw 'ISCC.exe (Inno Setup 6) est introuvable. Installe Inno Setup 6.3 ou plus (https://jrsoftware.org/isinfo.php) ou relance avec -InstallInno.'
    }
}
Write-Host "Inno Setup : $iscc"

if (-not (Test-Path $icon)) { throw "Icone introuvable : $icon (produite par l'agent branding). Genere-la avant de compiler l'installateur." }

# --- Signature ---
$signArgs = Get-SignArguments
$signtool = $null
if ($signArgs) {
    $signtool = Find-SignTool
    if (-not $signtool) { throw 'Variables de signature definies mais signtool.exe est introuvable (SDK Windows).' }
    Write-Host "Signature activee (signtool : $signtool)" -ForegroundColor Green
} else {
    Write-Warning 'Aucune signature (SELECTO_SIGN_PFX / SELECTO_SIGN_THUMBPRINT absents) : installateur NON SIGNE. SmartScreen affichera "editeur inconnu".'
}

$arches = if ($Arch -eq 'Both') { @('x64', 'arm64') } else { @($Arch) }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$built = @()

$e = 'Site'   # seul emballage installable par Inno Setup (le Store passe par scripts\build-msix.ps1)
foreach ($a in $arches) {
    $pub = Join-Path $publishRoot "$e-$a"
    Write-Host "=== $e / $a ===" -ForegroundColor Cyan

    if (-not $SkipPublish) {
        if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
        $dn = @('publish', $project, '-c', $Configuration, "-p:Edition=$e", '-r', "win-$a", '--self-contained', 'true', '-o', $pub)
        if ($ReadyToRun) { $dn += '-p:PublishReadyToRun=true' }
        & dotnet @dn
        if ($LASTEXITCODE -ne 0) { throw "Echec de dotnet publish pour $e/$a" }
    }
    if (-not (Test-Path (Join-Path $pub 'SelectoAI.exe'))) { throw "SelectoAI.exe absent de $pub (publication manquante ?)" }

    if ($signtool) { Sign-File (Join-Path $pub 'SelectoAI.exe') $signtool $signArgs }

    $name = "SelectoAI-Setup-$a-$version.exe"
    $target = Join-Path $outDir $name
    if (Test-Path $target) { Remove-Item -Force $target }

    & $iscc "/DArch=$a" "/DVersion=$version" "/DSourceDir=$pub" "/DOutputDir=$outDir" $iss
    if ($LASTEXITCODE -ne 0) { throw "Echec d'ISCC pour $e/$a" }
    if (-not (Test-Path $target)) { throw "Installateur attendu introuvable : $target" }

    if ($signtool) { Sign-File $target $signtool $signArgs }
    $built += $target
}

# --- SHA256SUMS.txt et latest.json (tous les installateurs de cette version presents dans le dossier) ---
$files = Get-ChildItem -Path $outDir -Filter "SelectoAI-Setup-*-$version.exe" | Sort-Object Name
$sumLines = @()
$entries = @()
foreach ($f in $files) {
    $hash = (Get-FileHash -Algorithm SHA256 -Path $f.FullName).Hash.ToLowerInvariant()
    $sumLines += "$hash  $($f.Name)"
    if ($f.Name -match '^SelectoAI-Setup-(x64|arm64)-') {
        $arch = $Matches[1]
        $entries += [ordered]@{
            edition = 'Site'
            arch    = $arch
            file    = $f.Name
            size    = $f.Length
            sha256  = $hash
            url     = $downloadBase + $f.Name
        }
        # Compatibilité : les clients 3.x cherchent l'entrée « Demo » de leur architecture ; sans elle ils ne verraient pas la 4.0.0.
        $entries += [ordered]@{
            edition = 'Demo'
            arch    = $arch
            file    = $f.Name
            size    = $f.Length
            sha256  = $hash
            url     = $legacyBase + $f.Name
        }
    }
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outDir 'SHA256SUMS.txt'), (($sumLines -join "`n") + "`n"), $utf8)
$notesValue = if ([string]::IsNullOrWhiteSpace($Notes)) { $null } else { $Notes.Trim() }
$latest = [ordered]@{ version = $version; generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); notes = $notesValue; installers = $entries }
[System.IO.File]::WriteAllText((Join-Path $outDir 'latest.json'), ($latest | ConvertTo-Json -Depth 5), $utf8)

# --- Resume ---
Write-Host ''
Write-Host "Installateurs produits (version $version) :" -ForegroundColor Green
$signedLabel = if ($signtool) { 'signe' } else { 'NON SIGNE' }
foreach ($t in $built) {
    $fi = Get-Item $t
    $h = (Get-FileHash -Algorithm SHA256 -Path $t).Hash.ToLowerInvariant()
    '  {0}  {1:N1} Mo  {2}  {3}' -f $fi.Name, ($fi.Length / 1MB), $signedLabel, $h | Write-Host
}
Write-Host "Dossier : $outDir (SHA256SUMS.txt, latest.json)"
