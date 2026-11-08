<#
.SYNOPSIS
  Construit "Selecto AI Setup" : l'installateur WPF (Fluent, anime), un seul .exe autonome par architecture (emballage Site).

.DESCRIPTION
  Pour chaque architecture :
    1. dotnet publish de l'application (autonome, -p:Edition=Site) vers dist\publish\Site-<arch>, signature de SelectoAI.exe ;
    2. zip de cette publication (hors .pdb) -> dist\payload\Site-<arch>\payload.zip ;
    3. dotnet publish de SelectBar.Setup en FICHIER UNIQUE autonome et COMPRIME, SANS charge utile : c'est le « stub » ;
    4. signature du stub (facultative), puis ajout A LA FIN du fichier de la charge utile et d'un trailer de 24 octets :
       Setup = [stub][payload.zip][trailer]. Le Setup copie le stub seul (sans charge utile) comme Uninstall.exe : le desinstallateur
       ne pese que le runtime compresse, pas la charge utile. Ne PAS resigner le Setup final (le trailer doit rester en fin de fichier) ;
    5. copie vers dist\installers-animated\SelectoAI-Setup-Animated-<arch>-<version>.exe.
  OPTIONNEL : l'installateur principal est Inno Setup (scripts\build-installer.ps1, dist\installers). Sorties de ce script
  dans dist\installers-animated : SelectoAI-Setup-Animated-<arch>-<version>.exe, SHA256SUMS.txt, latest.json.
  Signature (facultative, jamais de secret dans le depot) : SELECTO_SIGN_PFX + SELECTO_SIGN_PASSWORD, ou SELECTO_SIGN_THUMBPRINT.
  Sans ces variables, l'installateur est produit NON SIGNE (SmartScreen affichera "editeur inconnu").
  Voir installer\README.md : Inno Setup est l'installateur recommande (moins d'alertes antivirus, pas de charge utile accolee).

.PARAMETER Arch
  x64, arm64 ou Both (defaut).
.PARAMETER ReadyToRun
  Ajoute -p:PublishReadyToRun=true (application et Setup : demarrage plus rapide, fichiers plus gros).
.PARAMETER SkipPublish
  Reutilise dist\publish\Site-<arch> deja present.

.EXAMPLE
  .\scripts\build-setup.ps1 -Arch arm64
  .\scripts\build-setup.ps1 -ReadyToRun
#>
param(
    [ValidateSet('x64', 'arm64', 'Both')][string]$Arch = 'Both',
    [switch]$ReadyToRun,
    [switch]$SkipPublish,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'SelectBar\SelectBar.csproj'
$setupProject = Join-Path $root 'SelectBar.Setup\SelectBar.Setup.csproj'
$icon = Join-Path $root 'SelectBar\Assets\SelectoAI.ico'
$publishRoot = Join-Path $root 'dist\publish'
$payloadRoot = Join-Path $root 'dist\payload'
$setupRoot = Join-Path $root 'dist\setup'
$outDir = Join-Path $root 'dist\installers-animated'

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

function Add-PayloadOverlay([string]$stubExe, [string]$payloadZip, [string]$targetExe) {
    # Format (voir SelectBar.Setup\Logic\OverlayTrailer.cs) : stub + payload + 'SELPAYL1' + stubLen(Int64 LE) + payloadLen(Int64 LE).
    $stubLen = (Get-Item $stubExe).Length
    $payloadLen = (Get-Item $payloadZip).Length
    $trailer = New-Object byte[] 24
    [System.Text.Encoding]::ASCII.GetBytes('SELPAYL1').CopyTo($trailer, 0)
    [System.BitConverter]::GetBytes([long]$stubLen).CopyTo($trailer, 8)
    [System.BitConverter]::GetBytes([long]$payloadLen).CopyTo($trailer, 16)
    if (Test-Path $targetExe) { Remove-Item -Force $targetExe }
    $out = [System.IO.File]::Open($targetExe, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        foreach ($part in @($stubExe, $payloadZip)) {
            $in = [System.IO.File]::OpenRead($part)
            try { $in.CopyTo($out, 1MB) } finally { $in.Dispose() }
        }
        $out.Write($trailer, 0, 24)
    } finally { $out.Dispose() }
    $expected = $stubLen + $payloadLen + 24
    if ((Get-Item $targetExe).Length -ne $expected) { throw "Taille inattendue de $targetExe (attendu $expected octets)" }
}

function New-PayloadZip([string]$sourceDir, [string]$zipPath) {
    # Entrees avec "/" (meme sous Windows PowerShell 5.1), sans .pdb. Le Setup normalise de toute facon.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    $fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            $base = (Resolve-Path $sourceDir).Path.TrimEnd('\')
            Get-ChildItem -Path $base -Recurse -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
                $rel = $_.FullName.Substring($base.Length).TrimStart('\') -replace '\\', '/'
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
            }
        } finally { $zip.Dispose() }
    } finally { $fs.Dispose() }
}

# --- Version (lue dans SelectBar.csproj) ---
$csproj = Get-Content -Raw -Path $appProject
if ($csproj -notmatch '<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>') { throw "Version introuvable dans $appProject" }
$version = $Matches[1]

if (-not (Test-Path $icon)) { throw "Icone introuvable : $icon. Genere-la avant de construire l'installateur." }

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

$e = 'Site'
foreach ($a in $arches) {
    Write-Host "=== $e / $a ===" -ForegroundColor Cyan
    $pub = Join-Path $publishRoot "$e-$a"

    # 1. Application
    if (-not $SkipPublish) {
        if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
        $dn = @('publish', $appProject, '-c', $Configuration, "-p:Edition=$e", '-r', "win-$a", '--self-contained', 'true', '-o', $pub)
        if ($ReadyToRun) { $dn += '-p:PublishReadyToRun=true' }
        & dotnet @dn
        if ($LASTEXITCODE -ne 0) { throw "Echec de dotnet publish (application) pour $e/$a" }
    }
    if (-not (Test-Path (Join-Path $pub 'SelectoAI.exe'))) { throw "SelectoAI.exe absent de $pub (publication manquante ?)" }
    Copy-Item -Force $icon (Join-Path $pub 'SelectoAI.ico')   # icone des raccourcis, comme l'installateur Inno
    if ($signtool) { Sign-File (Join-Path $pub 'SelectoAI.exe') $signtool $signArgs }

    # 2. Charge utile
    $payloadDir = Join-Path $payloadRoot "$e-$a"
    New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
    $payload = Join-Path $payloadDir 'payload.zip'
    New-PayloadZip $pub $payload
    Write-Host ('Charge utile : {0:N1} Mo' -f ((Get-Item $payload).Length / 1MB))

    # 3. Stub du Setup (fichier unique autonome, compresse, SANS charge utile)
    $sdir = Join-Path $setupRoot "$e-$a"
    if (Test-Path $sdir) { Remove-Item -Recurse -Force $sdir }
    $sn = @('publish', $setupProject, '-c', $Configuration, "-p:Arch=$a", "-p:Version=$version",
            '-r', "win-$a", '--self-contained', 'true', '-o', $sdir)
    if ($ReadyToRun) { $sn += '-p:PublishReadyToRun=true' }
    & dotnet @sn
    if ($LASTEXITCODE -ne 0) { throw "Echec de dotnet publish (Setup) pour $e/$a" }
    $exe = Join-Path $sdir 'SelectoAI-Setup.exe'
    if (-not (Test-Path $exe)) { throw "SelectoAI-Setup.exe absent de $sdir" }

    Write-Host ('Stub du Setup (= Uninstall.exe) : {0:N1} Mo' -f ((Get-Item $exe).Length / 1MB))

    # 4. Signature du stub seul, ajout de la charge utile, copie
    if ($signtool) { Sign-File $exe $signtool $signArgs }
    $target = Join-Path $outDir "SelectoAI-Setup-Animated-$a-$version.exe"
    Add-PayloadOverlay $exe $payload $target
    $built += $target
}

# --- SHA256SUMS.txt et latest.json (tous les installateurs de cette version presents dans le dossier) ---
$files = Get-ChildItem -Path $outDir -Filter "SelectoAI-Setup-*-$version.exe" | Sort-Object Name
$sumLines = @()
$entries = @()
foreach ($f in $files) {
    $hash = (Get-FileHash -Algorithm SHA256 -Path $f.FullName).Hash.ToLowerInvariant()
    $sumLines += "$hash  $($f.Name)"
    if ($f.Name -match '^SelectoAI-Setup-Animated-(x64|arm64)-') {
        $entries += [ordered]@{
            edition = 'Site'
            arch    = $Matches[1]
            file    = $f.Name
            size    = $f.Length
            sha256  = $hash
            url     = 'A COMPLETER (URL de telechargement publique)'
        }
    }
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outDir 'SHA256SUMS.txt'), (($sumLines -join "`n") + "`n"), $utf8)
$latest = [ordered]@{ version = $version; generated = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); installers = $entries }
[System.IO.File]::WriteAllText((Join-Path $outDir 'latest.json'), ($latest | ConvertTo-Json -Depth 5), $utf8)

# --- Resume ---
Write-Host ''
Write-Host "Installateurs Setup produits (version $version) :" -ForegroundColor Green
$signedLabel = if ($signtool) { 'signe' } else { 'NON SIGNE' }
foreach ($t in $built) {
    $fi = Get-Item $t
    $h = (Get-FileHash -Algorithm SHA256 -Path $t).Hash.ToLowerInvariant()
    '  {0}  {1:N1} Mo  {2}  {3}' -f $fi.Name, ($fi.Length / 1MB), $signedLabel, $h | Write-Host
}
Write-Host "Dossier : $outDir (SHA256SUMS.txt, latest.json)"
