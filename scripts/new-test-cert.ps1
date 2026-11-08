<#
.SYNOPSIS
  Cree un certificat auto-signe DE TEST pour installer (sideload) un MSIX de Selecto AI et lancer le Windows App Certification Kit.

.DESCRIPTION
  NE JAMAIS utiliser ce certificat pour la soumission : le Microsoft Store signe lui-meme le paquet envoye (aucun certificat final a gerer).
  Le sujet (-Subject) doit etre IDENTIQUE au Publisher du manifeste : build-msix.ps1 -Sign utilise CN=SelectoAITest par defaut.
  Sorties dans dist\msix\test-cert : SelectoAI-test.pfx (mot de passe -Password), SelectoAI-test.cer (partie publique).
  -Trust installe le .cer dans « Personnes de confiance » de l'ordinateur (administrateur requis) : indispensable pour Add-AppxPackage.
  Retrait : Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where Subject -eq 'CN=SelectoAITest' | Remove-Item

.EXAMPLE
  .\scripts\new-test-cert.ps1 -Trust
#>
param(
    [string]$Subject = 'CN=SelectoAITest',
    [string]$Password = 'selecto-test',
    [string]$OutDir,
    [switch]$Trust
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root 'dist\msix\test-cert' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$pfx = Join-Path $OutDir 'SelectoAI-test.pfx'
$cer = Join-Path $OutDir 'SelectoAI-test.cer'

$cert = New-SelfSignedCertificate -Type Custom -Subject $Subject -KeyUsage DigitalSignature -FriendlyName 'Selecto AI (test sideload)' `
    -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(1) `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Write-Host "Certificat de test : $pfx (sujet $Subject, valable 1 an, mot de passe : $Password)" -ForegroundColor Green

if ($Trust) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) { throw '-Trust demande une session PowerShell administrateur.' }
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    Write-Host 'Certificat installe dans Personnes de confiance (ordinateur local).' -ForegroundColor Green
} else {
    Write-Host 'Pour installer un paquet signe avec ce certificat : relance avec -Trust (administrateur).' -ForegroundColor Yellow
}
