<#
.SYNOPSIS
  Produit les paquets MSIX de l'edition Store de Selecto AI (x64, arm64) et le .msixbundle pour le Partner Center.

.DESCRIPTION
  Pour chaque architecture : dotnet publish autonome de l'edition Store (-p:Edition=Store -r win-<arch>) vers dist\publish\Store-<arch>,
  mise en page (publication + logos de branding\dist + AppxManifest renseigne), makepri (ressources d'images, si disponible), makeappx pack.
  Sorties dans dist\msix : SelectoAI-<version>-x64.msix, SelectoAI-<version>-arm64.msix et SelectoAI-<version>.msixbundle (makeappx bundle).

  SIGNATURE : les paquets sont NON SIGNES. C'est voulu : le Store signe lui-meme le paquet soumis, le certificat final n'existe pas.
  -Sign signe avec un certificat AUTO-SIGNE DE TEST (scripts\new-test-cert.ps1), uniquement pour installer le paquet sur un PC de test
  (sideload) ou lancer le Windows App Certification Kit. Un paquet signe ainsi ne doit JAMAIS etre soumis.

  Valeurs du Partner Center (Gestion des produits > Identite du produit) : -IdentityName, -Publisher (CN=...), -PublisherDisplayName.
  Avec -Sign, elles ont des valeurs de test par defaut (SelectoAI.Test, CN=SelectoAITest) : le Publisher du manifeste DOIT etre
  identique au sujet du certificat de test.

  Windows App Certification Kit (a lancer sur un PC Windows avec le SDK, en administrateur) :
    .\scripts\new-test-cert.ps1 -Trust                      (une fois ; installe le certificat de test)
    .\scripts\build-msix.ps1 -Sign -Arch x64
    Add-AppxPackage .\dist\msix\SelectoAI-<version>-x64.msix
    & "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" reset
    & "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" test -apptype desktop -packagefullname <PackageFullName> -reportoutputpath .\dist\msix\wack-report.xml
  (PackageFullName : Get-AppxPackage *SelectoAI* | Select -Expand PackageFullName). Voir installer\msix\README.md.

.EXAMPLE
  .\scripts\build-msix.ps1 -IdentityName "12345Publisher.SelectoAI" -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Mon nom"
  .\scripts\build-msix.ps1 -Sign -Arch x64 -SkipPublish
#>
param(
    [ValidateSet('x64', 'arm64', 'Both')][string]$Arch = 'Both',
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$Configuration = 'Release',
    [string]$AssetsDir,
    [switch]$SkipPublish,
    [switch]$Sign,
    [string]$PfxPath,
    [string]$PfxPassword = 'selecto-test'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'SelectBar\SelectBar.csproj'
if (-not $AssetsDir) {
    # branding\ est dans le depot du site (Desktop\Code\selecto) ; sinon indiquer -AssetsDir
    $candidates = @((Join-Path $root 'branding\dist'), (Join-Path (Split-Path -Parent $root) 'selecto\branding\dist'))
    $AssetsDir = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $AssetsDir) { throw "Logos introuvables ($($candidates -join ' ; ')) : passe -AssetsDir <dossier branding\dist>." }
}
$manifestTemplate = Join-Path $root 'installer\msix\AppxManifest.xml'
$publishRoot = Join-Path $root 'dist\publish'
$outDir = Join-Path $root 'dist\msix'

# --- Version : 4 nombres, dernier = 0 (regle du Store) ---
if (-not $Version) {
    $csproj = Get-Content -Raw -Path $project
    if ($csproj -notmatch '<Version>\s*([0-9]+)\.([0-9]+)\.([0-9]+)\s*</Version>') { throw 'Version x.y.z introuvable dans SelectBar.csproj (passe -Version x.y.z.0).' }
    $Version = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.0$') { throw "Version MSIX invalide : $Version (format x.y.z.0 ; le Store reserve le dernier nombre)." }

# --- Identite ---
if ($Sign) {
    if (-not $IdentityName) { $IdentityName = 'SelectoAI.Test' }
    if (-not $Publisher) { $Publisher = 'CN=SelectoAITest' }
    if (-not $PublisherDisplayName) { $PublisherDisplayName = 'Selecto AI (test)' }
}
if (-not $IdentityName -or -not $Publisher -or -not $PublisherDisplayName) {
    throw 'Passe -IdentityName, -Publisher et -PublisherDisplayName (Partner Center > Gestion des produits > Identite du produit), ou -Sign pour un paquet de test.'
}
if ($Publisher -notmatch '^CN=') { throw "Publisher doit etre de la forme CN=... (recu : $Publisher)." }

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kits) {
        $hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
        $found = Get-ChildItem -Path $kits -Recurse -Filter $name -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\$hostArch\\" -or $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

$makeappx = Find-SdkTool 'makeappx.exe'
if (-not $makeappx) { throw 'makeappx.exe introuvable : installe le SDK Windows 10/11 (composant "Windows SDK Signing Tools for Desktop Apps").' }
$makepri = Find-SdkTool 'makepri.exe'
if (-not $makepri) { Write-Warning 'makepri.exe introuvable : pas de resources.pri (les icones utilisent le fichier de base, sans variantes de resolution).' }
$signtool = $null
if ($Sign) {
    $signtool = Find-SdkTool 'signtool.exe'
    if (-not $signtool) { throw 'signtool.exe introuvable (SDK Windows).' }
    if (-not $PfxPath) { $PfxPath = Join-Path $outDir 'test-cert\SelectoAI-test.pfx' }
    if (-not (Test-Path $PfxPath)) { throw "Certificat de test introuvable : $PfxPath (lance d'abord scripts\new-test-cert.ps1)." }
}

# --- Logos : fichier de base (resolution 100 %) + variantes de resolution pour makepri ---
function Resolve-Logo([string]$baseName, [bool]$required) {
    foreach ($candidate in @("$baseName.png", "$baseName.scale-100.png")) {
        $p = Join-Path $AssetsDir $candidate
        if (Test-Path $p) { return $p }
    }
    if ($required) { throw "Logo manquant : $baseName(.scale-100).png dans $AssetsDir." }
    return $null
}
$logoSources = @{}
foreach ($n in @('StoreLogo', 'Square44x44Logo', 'Square150x150Logo')) { $logoSources[$n] = Resolve-Logo $n $true }
$wideSource = Resolve-Logo 'Wide310x150Logo' $false
if ($wideSource) { $logoSources['Wide310x150Logo'] = $wideSource } else { Write-Warning 'Wide310x150Logo absent : tuile large non fournie.' }

$archList = if ($Arch -eq 'Both') { @('x64', 'arm64') } else { @($Arch) }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$packages = @()

foreach ($a in $archList) {
    Write-Host "=== Store / $a ===" -ForegroundColor Cyan
    $pub = Join-Path $publishRoot "Store-$a"
    if (-not $SkipPublish) {
        if (Test-Path $pub) { Remove-Item -Recurse -Force $pub }
        & dotnet publish $project -c $Configuration "-p:Edition=Store" -r "win-$a" --self-contained true -o $pub
        if ($LASTEXITCODE -ne 0) { throw "Echec de dotnet publish pour Store/$a" }
    }
    if (-not (Test-Path (Join-Path $pub 'SelectoAI.exe'))) { throw "SelectoAI.exe absent de $pub (publication manquante ? retire -SkipPublish)." }

    # mise en page
    $layout = Join-Path $outDir "layout-Store-$a"
    if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
    New-Item -ItemType Directory -Force -Path $layout | Out-Null
    Copy-Item -Path (Join-Path $pub '*') -Destination $layout -Recurse -Force
    Get-ChildItem -Path $layout -Recurse -Filter '*.pdb' | Remove-Item -Force
    $assets = Join-Path $layout 'Assets'
    New-Item -ItemType Directory -Force -Path $assets | Out-Null
    foreach ($n in $logoSources.Keys) { Copy-Item $logoSources[$n] (Join-Path $assets "$n.png") -Force }
    # variantes de resolution (scale-100/200/400, targetsize-*) : utilisees par makepri
    Get-ChildItem -Path $AssetsDir -File | Where-Object { $_.Name -match '^(StoreLogo|Square44x44Logo|Square150x150Logo|Wide310x150Logo)\.(scale|targetsize)-' } |
        ForEach-Object { Copy-Item $_.FullName (Join-Path $assets $_.Name) -Force }

    # manifeste
    $xml = Get-Content -Raw -Encoding UTF8 -Path $manifestTemplate
    $xml = $xml.Replace('[À RENSEIGNER depuis Partner Center : Identity Name]', $IdentityName)
    $xml = $xml.Replace('[À RENSEIGNER depuis Partner Center : Publisher display name]', [System.Security.SecurityElement]::Escape($PublisherDisplayName))
    $xml = $xml.Replace('[À RENSEIGNER depuis Partner Center : Publisher]', [System.Security.SecurityElement]::Escape($Publisher))
    $xml = $xml.Replace('{{VERSION}}', $Version).Replace('{{ARCH}}', $a)
    if (-not $wideSource) { $xml = [regex]::Replace($xml, '\s*<uap:DefaultTile[^>]*/>', '') }
    if ([regex]::IsMatch($xml, '(="|>)\[À RENSEIGNER')) { throw 'Le manifeste contient encore des valeurs "À RENSEIGNER".' }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $manifestPath = Join-Path $layout 'AppxManifest.xml'
    [System.IO.File]::WriteAllText($manifestPath, $xml, $utf8)

    # ressources d'images (variantes de resolution) : facultatif
    if ($makepri) {
        $cfg = Join-Path $outDir "priconfig-$a.xml"
        $pri = Join-Path $layout 'resources.pri'
        & $makepri createconfig /cf $cfg /dq en-US /o | Out-Null
        if ($LASTEXITCODE -eq 0) { & $makepri new /pr $layout /cf $cfg /mn $manifestPath /of $pri /o | Out-Null }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "makepri a echoue pour $a : paquet sans resources.pri."
            if (Test-Path $pri) { Remove-Item -Force $pri }
        }
    }

    # paquet
    $msix = Join-Path $outDir "SelectoAI-$Version-$a.msix"
    if (Test-Path $msix) { Remove-Item -Force $msix }
    & $makeappx pack /d $layout /p $msix /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack a echoue pour $a." }
    $packages += $msix
}

# --- Bundle (x64 + arm64) ---
$bundle = $null
if ($packages.Count -gt 1) {
    $bundleInput = Join-Path $outDir 'bundle-input'
    if (Test-Path $bundleInput) { Remove-Item -Recurse -Force $bundleInput }
    New-Item -ItemType Directory -Force -Path $bundleInput | Out-Null
    foreach ($p in $packages) { Copy-Item $p $bundleInput }
    $bundle = Join-Path $outDir "SelectoAI-$Version.msixbundle"
    if (Test-Path $bundle) { Remove-Item -Force $bundle }
    & $makeappx bundle /d $bundleInput /p $bundle /bv $Version /o
    if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle a echoue.' }
} else {
    Write-Warning 'Une seule architecture : pas de .msixbundle (lance sans -Arch pour produire x64 + arm64).'
}

# --- Signature de test (facultative) ---
$all = @($packages)
if ($bundle) { $all += $bundle }
if ($Sign) {
    foreach ($f in $all) {
        & $signtool sign /fd SHA256 /f $PfxPath /p $PfxPassword $f
        if ($LASTEXITCODE -ne 0) { throw "signtool a echoue pour $f (le Publisher du manifeste doit etre identique au sujet du certificat)." }
    }
    Write-Warning 'Paquets signes avec le certificat de TEST : pour sideload et WACK uniquement, JAMAIS pour la soumission au Store.'
} else {
    Write-Host 'Paquets NON SIGNES (normal : le Store signe a la soumission).' -ForegroundColor Yellow
}

Write-Host ''
foreach ($f in $all) { Write-Host ('{0}  ({1:N1} Mo)' -f $f, ((Get-Item $f).Length / 1MB)) -ForegroundColor Green }
if ($bundle) { Write-Host "A envoyer au Partner Center : $bundle" -ForegroundColor Green }
