<#
.SYNOPSIS
  Test d'acceptation d'un installateur Selecto AI, a lancer dans une VM Windows propre.

.DESCRIPTION
  Installe en silence (/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=<dossier temporaire>), controle les fichiers
  et la cle de desinstallation, lance SelectoAI.exe --selftest (delai 3 min), desinstalle en silence (sans /PURGEDATA) puis
  verifie que le dossier et la cle ont disparu et que les donnees utilisateur sont intactes.
  Code de sortie : 0 si tous les controles sont OK, sinon le nombre de controles en ECHEC (au moins 1).

.EXAMPLE
  .\scripts\verify-installer.ps1 -Installer .\dist\installers\SelectoAI-Setup-arm64-4.0.0.exe
#>
param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [int]$SelfTestTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$failures = 0
function Check([string]$label, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host ("[OK]    {0}" -f $label) -ForegroundColor Green }
    else {
        $script:failures++
        Write-Host ("[ECHEC] {0} {1}" -f $label, $detail) -ForegroundColor Red
    }
}

$appId = '{B6F2A8D4-5C3E-4F71-9A0B-7E1D2C4F8A63}_is1'
$uninstKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
$dataDir = Join-Path $env:APPDATA 'SelectoAI'
$sentinel = Join-Path $dataDir ('verify-sentinel-' + [guid]::NewGuid().ToString('N') + '.txt')
$installDir = Join-Path $env:TEMP ('SelectoAI-verify-' + [guid]::NewGuid().ToString('N'))
$createdData = $false

try {
    $installerFull = (Resolve-Path -Path $Installer -ErrorAction SilentlyContinue)
    Check 'Installateur present' ($null -ne $installerFull) $Installer
    if ($null -eq $installerFull) { throw 'Installateur introuvable.' }
    $installerPath = $installerFull.Path

    # Donnee utilisateur factice : doit survivre a la desinstallation sans /PURGEDATA.
    if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Force -Path $dataDir | Out-Null; $createdData = $true }
    Set-Content -Path $sentinel -Value 'donnee utilisateur de test' -Encoding ASCII

    # --- Installation ---
    $instArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=`"$installDir`""
    $p = Start-Process -FilePath $installerPath -ArgumentList $instArgs -Wait -PassThru
    Check 'Installation silencieuse (code 0)' ($p.ExitCode -eq 0) "code $($p.ExitCode)"

    $exe = Join-Path $installDir 'SelectoAI.exe'
    Check 'SelectoAI.exe installe' (Test-Path $exe)
    Check 'Icone SelectoAI.ico installee' (Test-Path (Join-Path $installDir 'SelectoAI.ico'))
    Check 'Dependances .NET autonomes (SelectoAI.dll, coreclr.dll)' ((Test-Path (Join-Path $installDir 'SelectoAI.dll')) -and (Test-Path (Join-Path $installDir 'coreclr.dll')))
    Check 'Desinstallateur unins000.exe installe' (Test-Path (Join-Path $installDir 'unins000.exe'))

    $key = Get-ItemProperty -Path $uninstKey -ErrorAction SilentlyContinue
    Check 'Cle de desinstallation du registre' ($null -ne $key)
    if ($null -ne $key) {
        Write-Host ("        {0} {1}" -f $key.DisplayName, $key.DisplayVersion)
        Check 'Version affichee renseignee' (-not [string]::IsNullOrEmpty($key.DisplayVersion))
    }

    # --- Auto-test de l'application ---
    if (Test-Path $exe) {
        $st = Start-Process -FilePath $exe -ArgumentList '--selftest' -PassThru
        if ($st.WaitForExit($SelfTestTimeoutSeconds * 1000)) {
            Check 'SelectoAI.exe --selftest (code 0)' ($st.ExitCode -eq 0) "code $($st.ExitCode)"
        } else {
            try { $st.Kill() } catch { }
            Check 'SelectoAI.exe --selftest termine dans le delai' $false "plus de $SelfTestTimeoutSeconds s"
        }
        $report = Join-Path $dataDir 'selftest-report.txt'
        if (Test-Path $report) { Write-Host "        Rapport : $report" }
    }

    # --- Desinstallation (sans /PURGEDATA) ---
    $unins = Join-Path $installDir 'unins000.exe'
    if (Test-Path $unins) {
        $u = Start-Process -FilePath $unins -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
        Check 'Desinstallation silencieuse (code 0)' ($u.ExitCode -eq 0) "code $($u.ExitCode)"
        # Le desinstallateur se relance depuis %TEMP% : on attend la disparition du dossier (60 s maximum).
        $deadline = (Get-Date).AddSeconds(60)
        while ((Test-Path $installDir) -and ((Get-Date) -lt $deadline)) { Start-Sleep -Seconds 2 }
    }
    Check 'Dossier d''installation supprime' (-not (Test-Path $installDir))
    Check 'Cle de desinstallation supprimee' (-not (Test-Path $uninstKey))
    Check 'Donnees utilisateur intactes (sans /PURGEDATA)' (Test-Path $sentinel)
}
catch {
    Write-Host "Erreur : $($_.Exception.Message)" -ForegroundColor Red
    $failures++
}
finally {
    if (Test-Path $sentinel) { Remove-Item -Force $sentinel -ErrorAction SilentlyContinue }
    if ($createdData -and (Test-Path $dataDir) -and -not (Get-ChildItem $dataDir -Force -ErrorAction SilentlyContinue)) {
        Remove-Item -Force $dataDir -ErrorAction SilentlyContinue
    }
    if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir -ErrorAction SilentlyContinue }
}

Write-Host ''
if ($failures -eq 0) { Write-Host 'RESULTAT : OK' -ForegroundColor Green; exit 0 }
Write-Host "RESULTAT : ECHEC ($failures controle(s))" -ForegroundColor Red
exit $failures
