<#
.SYNOPSIS
  Test d'acceptation de "Selecto AI Setup" (installateur WPF optionnel), a lancer dans une VM Windows propre.

.DESCRIPTION
  Installe en silence (/S /DIR=<dossier temporaire>), controle fichiers, raccourci du Menu Demarrer et entree "Applications installees"
  (HKCU\...\Uninstall\SelectoAI), lance SelectoAI.exe --selftest, desinstalle en silence (sans /PURGEDATA) puis verifie que tout a
  disparu et que les donnees utilisateur sont intactes. Code de sortie : 0 si tout est OK, sinon le nombre de controles en ECHEC.
  Pour Inno (unins000.exe), utiliser scripts\verify-installer.ps1.

.EXAMPLE
  .\scripts\verify-setup.ps1 -Installer .\dist\installers-animated\SelectoAI-Setup-Animated-arm64-4.0.0.exe
#>
param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [int]$SelfTestTimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$failures = 0
function Check([string]$label, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host ("[OK]    {0}" -f $label) -ForegroundColor Green }
    else { $script:failures++; Write-Host ("[ECHEC] {0} {1}" -f $label, $detail) -ForegroundColor Red }
}

$uninstKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectoAI'
$dataDir = Join-Path $env:APPDATA 'SelectoAI'
$sentinel = Join-Path $dataDir ('verify-sentinel-' + [guid]::NewGuid().ToString('N') + '.txt')
$installDir = Join-Path $env:TEMP ('SelectoAI-verify-' + [guid]::NewGuid().ToString('N'))
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Selecto AI'
$createdData = $false

try {
    $installerFull = Resolve-Path -Path $Installer -ErrorAction SilentlyContinue
    Check 'Installateur present' ($null -ne $installerFull) $Installer
    if ($null -eq $installerFull) { throw 'Installateur introuvable.' }

    if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Force -Path $dataDir | Out-Null; $createdData = $true }
    Set-Content -Path $sentinel -Value 'donnee utilisateur de test' -Encoding ASCII

    # --- Installation silencieuse ---
    $p = Start-Process -FilePath $installerFull.Path -ArgumentList "/S /DIR=`"$installDir`"" -Wait -PassThru
    Check 'Installation silencieuse (code 0)' ($p.ExitCode -eq 0) "code $($p.ExitCode) ; journal : $env:TEMP\SelectoAI-Setup.log"

    $exe = Join-Path $installDir 'SelectoAI.exe'
    Check 'SelectoAI.exe installe' (Test-Path $exe)
    Check 'Icone SelectoAI.ico installee' (Test-Path (Join-Path $installDir 'SelectoAI.ico'))
    Check 'Dependances .NET autonomes (SelectoAI.dll, coreclr.dll)' ((Test-Path (Join-Path $installDir 'SelectoAI.dll')) -and (Test-Path (Join-Path $installDir 'coreclr.dll')))
    Check 'Desinstallateur Uninstall.exe installe' (Test-Path (Join-Path $installDir 'Uninstall.exe'))
    # Taille : Uninstall.exe = le Setup SANS charge utile (stub), donc nettement plus petit que l'installateur.
    $installerMb = (Get-Item $installerFull.Path).Length / 1MB
    $uninsFile = Join-Path $installDir 'Uninstall.exe'
    if (Test-Path $uninsFile) {
        $uninsMb = (Get-Item $uninsFile).Length / 1MB
        $dirMb = (Get-ChildItem -Path $installDir -Recurse -File -Force | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host ("        Taille : installateur {0:N0} Mo, Uninstall.exe {1:N0} Mo, dossier installe {2:N0} Mo" -f $installerMb, $uninsMb, $dirMb)
        Check 'Uninstall.exe sans charge utile (moins de 60 % de l''installateur)' ($uninsMb -lt ($installerMb * 0.6)) ("{0:N0} Mo pour un installateur de {1:N0} Mo" -f $uninsMb, $installerMb)
    }
    Check 'Raccourci du Menu Demarrer' ((Get-ChildItem $startMenu -Filter '*.lnk' -ErrorAction SilentlyContinue | Measure-Object).Count -ge 1)

    $key = Get-ItemProperty -Path $uninstKey -ErrorAction SilentlyContinue
    Check 'Entree "Applications installees" (HKCU\...\Uninstall\SelectoAI)' ($null -ne $key)
    if ($null -ne $key) {
        Write-Host ("        {0} {1} ({2} Ko)" -f $key.DisplayName, $key.DisplayVersion, $key.EstimatedSize)
        Check 'Version, editeur, icone, taille renseignes' ((-not [string]::IsNullOrEmpty($key.DisplayVersion)) -and $key.Publisher -and $key.DisplayIcon -and ($key.EstimatedSize -gt 0))
        Check 'UninstallString pointe vers Uninstall.exe --uninstall' ($key.UninstallString -like '*Uninstall.exe" --uninstall')
        Check 'InstallLocation correct' ($key.InstallLocation -eq $installDir)
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
    }

    # --- Mise a jour par-dessus (meme version : doit reussir et tout conserver) ---
    $p2 = Start-Process -FilePath $installerFull.Path -ArgumentList "/S /DIR=`"$installDir`"" -Wait -PassThru
    Check 'Reinstallation par-dessus (code 0)' ($p2.ExitCode -eq 0) "code $($p2.ExitCode)"
    Check 'Donnees utilisateur intactes apres mise a jour' (Test-Path $sentinel)

    # --- Desinstallation silencieuse (sans /PURGEDATA) ---
    $unins = Join-Path $installDir 'Uninstall.exe'
    if (Test-Path $unins) {
        $u = Start-Process -FilePath $unins -ArgumentList '--uninstall --silent' -Wait -PassThru
        Check 'Desinstallation silencieuse (code 0)' ($u.ExitCode -eq 0) "code $($u.ExitCode)"
    }
    # Uninstall.exe est verrouille pendant sa propre desinstallation : il reste (suppression programmee au redemarrage) avec le dossier.
    $leftover = if (Test-Path $installDir) { @(Get-ChildItem -Path $installDir -Force | Where-Object { $_.Name -ne 'Uninstall.exe' }) } else { @() }
    Check 'Dossier d''installation supprime (hormis Uninstall.exe verrouille)' ($leftover.Count -eq 0)
    Check 'Entree "Applications installees" supprimee' (-not (Test-Path $uninstKey))
    Check 'Raccourcis du Menu Demarrer supprimes' (-not (Test-Path $startMenu))
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
