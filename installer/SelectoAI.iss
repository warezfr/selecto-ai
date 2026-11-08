; Selecto AI - installateur Inno Setup 6 (6.3 ou plus recent : x64compatible).
; Compilation : ISCC.exe /DArch=x64 /DVersion=4.0.0 /DSourceDir=<dossier publie> /DOutputDir=<sortie> installer\SelectoAI.iss
; (voir scripts\build-installer.ps1 et installer\README.md). Fichier enregistre en UTF-8 avec BOM.

#ifndef Arch
  #define Arch "x64"
#endif
#ifndef Version
  #define Version "4.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\publish\Site-" + Arch
#endif
#ifndef OutputDir
  #define OutputDir "..\dist\installers"
#endif

#if (Arch != "x64") && (Arch != "arm64")
  #error "Arch doit valoir x64 ou arm64 (/DArch=x64|arm64)."
#endif

; Constantes modifiables
#define Publisher "EL MAHDI ZINEDDINE"
#define Copyright "© 2026 EL MAHDI ZINEDDINE"
#define Description "Selecto AI : barre d'actions sur le texte sélectionné"
#define Website "https://selecto.span.observer"
#define ExeName "SelectoAI.exe"
#define IconFile "..\SelectBar\Assets\SelectoAI.ico"
; Bitmaps de l'assistant (logo S.) : produits par selecto\branding\build-installer-images.mjs, copies dans installer\wizard\.
#define WizardDir "wizard"
#define AppDisplayName "Selecto AI"

; Echec clair (et non silencieux) si l'icone ou la publication manquent.
#if !FileExists(AddBackslash(SourcePath) + IconFile)
  #error "Icone introuvable : SelectBar\Assets\SelectoAI.ico (produite par l'agent branding). Genere-la avant de compiler l'installateur."
#endif
#if !FileExists(AddBackslash(SourcePath) + WizardDir + "\wizard-large.bmp") || !FileExists(AddBackslash(SourcePath) + WizardDir + "\wizard-small.bmp")
  #error "Bitmaps de l'assistant introuvables dans installer\wizard (wizard-large.bmp, wizard-small.bmp) : voir installer\README.md."
#endif
#if !FileExists(SourceDir + "\" + ExeName) && !FileExists(AddBackslash(SourcePath) + SourceDir + "\" + ExeName)
  #error "SelectoAI.exe introuvable dans SourceDir : publie d'abord l'application (scripts\build-installer.ps1 le fait)."
#endif

[Setup]
; AppId fixe et IDENTIQUE a celui des versions 3.x (Demo et Pro) : la 4.0.0 remplace une 3.x installee (meme dossier, meme entree
; « Applications installees », meme mutex d'instance unique).
AppId={{B6F2A8D4-5C3E-4F71-9A0B-7E1D2C4F8A63}
AppName={#AppDisplayName}
AppVersion={#Version}
AppVerName={#AppDisplayName} {#Version}
AppPublisher={#Publisher}
AppPublisherURL={#Website}
AppSupportURL={#Website}
AppUpdatesURL={#Website}
VersionInfoVersion={#Version}
VersionInfoCompany={#Publisher}
VersionInfoProductName={#AppDisplayName}
VersionInfoDescription={#Description} (installateur)
VersionInfoCopyright={#Copyright}
AppCopyright={#Copyright}
DefaultDirName={autopf}\Selecto AI
DefaultGroupName=Selecto AI
DisableProgramGroupPage=yes
; Installation par utilisateur par defaut, sans droits administrateur ; l'utilisateur peut choisir "pour tous".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.17763
; Assistant moderne (Windows 11) : page d'accueil et de fin avec l'image latérale « S. », logo en en-tête des autres pages.
; Listes de bitmaps : Inno prend celui qui convient le mieux au DPI (100 %, 150 %, 200 %).
#ifdef DarkWizard
WizardStyle=modern dynamic
#else
WizardStyle=modern
#endif
DisableWelcomePage=no
WizardImageFile={#WizardDir}\wizard-large.bmp,{#WizardDir}\wizard-large-150.bmp,{#WizardDir}\wizard-large-200.bmp
WizardSmallImageFile={#WizardDir}\wizard-small.bmp,{#WizardDir}\wizard-small-150.bmp,{#WizardDir}\wizard-small-200.bmp
#ifdef DarkWizard
; Option (Inno Setup 6.6 ou plus, /DDarkWizard) : assistant clair/sombre selon Windows, avec les variantes sombres.
WizardImageFileDynamicDark={#WizardDir}\wizard-large-dark.bmp,{#WizardDir}\wizard-large-150-dark.bmp,{#WizardDir}\wizard-large-200-dark.bmp
WizardSmallImageFileDynamicDark={#WizardDir}\wizard-small-dark.bmp,{#WizardDir}\wizard-small-150-dark.bmp,{#WizardDir}\wizard-small-200-dark.bmp
#endif
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\SelectoAI.ico
UninstallDisplayName={#AppDisplayName}
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=SelectoAI-Setup-{#Arch}-{#Version}
; Instance en cours : mutex reel de l'application (Program.cs). La section Code (InitializeSetup, InitializeUninstall) ferme
; l'application AVANT ces verifications : silencieux = fermeture automatique, interactif = question Oui/Non.
; AppMutex et CloseApplications restent en filet de securite (sans effet quand l'application est deja fermee).
AppMutex=Local\SelectoAI.SingleInstance
SetupMutex=SelectoAISetupMutex
CloseApplications=yes
CloseApplicationsFilter=SelectoAI.exe
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"; LicenseFile: "license-fr.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "license-en.txt"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"; LicenseFile: "license-es.txt"

[Messages]
french.WelcomeLabel1=Bienvenue dans l'installation de [name]
french.WelcomeLabel2=Sélectionne un texte dans n'importe quelle application : la barre apparaît, prête à copier, chercher, traduire ou résumer.%n%n[name/ver] va être installé sur cet ordinateur, avec une IA locale et gratuite.%n%nFerme les autres applications si tu le peux, puis clique sur Suivant pour continuer.
french.FinishedHeadingLabel=[name] est installé
french.FinishedLabelNoIcons=[name] est prêt. Sélectionne un texte dans n'importe quelle application pour voir la barre.
french.FinishedLabel=[name] est prêt. Sélectionne un texte dans n'importe quelle application pour voir la barre.%n%nClique sur Terminer pour fermer l'assistant.
english.WelcomeLabel1=Welcome to the [name] Setup
english.WelcomeLabel2=Select text in any app and the bar appears, ready to copy, search, translate or summarize.%n%n[name/ver] will be installed on this computer, with a free, local AI.%n%nClose other applications if you can, then click Next to continue.
english.FinishedHeadingLabel=[name] is installed
english.FinishedLabelNoIcons=[name] is ready. Select text in any app to see the bar.
english.FinishedLabel=[name] is ready. Select text in any app to see the bar.%n%nClick Finish to close Setup.

[CustomMessages]
french.TaskGroupStartup=Démarrage :
french.TaskStartup=Lancer Selecto AI avec Windows
french.DowngradeBlocked=La version %1 de Selecto AI est déjà installée, plus récente que celle de cet installateur (%2).%nLa rétrogradation est refusée. Relance l'installateur avec /FORCE pour l'imposer.
french.PurgeTitle=Supprimer aussi tes réglages et ton IA locale ?
french.PurgeQuestion=Supprimer aussi tes réglages et ton IA locale ?%n%nRéglages : %1 (%2)%nIA locale (moteur Ollama et modèles) : %3 (%4)%n%nOui : ces dossiers et la clé de registre Selecto AI sont supprimés définitivement (un Ollama installé séparément n'est jamais touché).%nNon : ils sont conservés pour une future réinstallation.
french.SizeMB=%1 Mo
french.SizeGB=%1 Go
french.SizeNone=absent
french.CloseAppQuestion=Selecto AI est ouvert. Le fermer pour continuer ?
spanish.TaskGroupStartup=Inicio:
spanish.TaskStartup=Iniciar Selecto AI con Windows
spanish.DowngradeBlocked=La versión %1 de Selecto AI ya está instalada y es más reciente que este instalador (%2).%nNo se permite la degradación. Ejecuta el instalador con /FORCE para forzarla.
spanish.PurgeTitle=¿Eliminar también tus ajustes y tu IA local?
spanish.PurgeQuestion=¿Eliminar también tus ajustes y tu IA local?%n%nAjustes: %1 (%2)%nIA local (motor Ollama y modelos): %3 (%4)%n%nSí: estas carpetas y la clave de registro de Selecto AI se eliminan de forma permanente (un Ollama instalado aparte nunca se toca).%nNo: se conservan para una futura reinstalación.
spanish.SizeMB=%1 MB
spanish.SizeGB=%1 GB
spanish.SizeNone=no presente
spanish.CloseAppQuestion=Selecto AI está abierto. ¿Cerrarlo para continuar?
english.TaskGroupStartup=Startup:
english.TaskStartup=Launch Selecto AI when Windows starts
english.DowngradeBlocked=Selecto AI version %1 is already installed and is newer than this installer (%2).%nDowngrading is refused. Run the installer with /FORCE to override.
english.PurgeTitle=Also delete your settings and local AI?
english.PurgeQuestion=Also delete your settings and local AI?%n%nSettings: %1 (%2)%nLocal AI (Ollama engine and models): %3 (%4)%n%nYes: these folders and the Selecto AI registry key are deleted permanently (a separately installed Ollama is never touched).%nNo: they are kept for a future reinstall.
english.SizeMB=%1 MB
english.SizeGB=%1 GB
english.SizeNone=not present
english.CloseAppQuestion=Selecto AI is open. Close it to continue?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:TaskStartup}"; GroupDescription: "{cm:TaskGroupStartup}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#IconFile}"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Raccourcis « Selecto AI Pro » des versions 3.x : le produit s'appelle Selecto AI, sans édition.
Type: files; Name: "{group}\Selecto AI Pro.lnk"
Type: files; Name: "{autodesktop}\Selecto AI Pro.lnk"

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\SelectoAI.ico"
Name: "{group}\{cm:UninstallProgram,{#AppDisplayName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\SelectoAI.ico"; Tasks: desktopicon

[Registry]
; Meme valeur et meme format que l'application (StartupEntries.Name, chemin entre guillemets) : pas de doublon.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Selecto AI"; ValueData: """{app}\{#ExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppDisplayName}}"; Flags: nowait postinstall runasoriginaluser

[Code]
const
  UninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B6F2A8D4-5C3E-4F71-9A0B-7E1D2C4F8A63}_is1';
  // Entrée « Applications installées » de l'ancien installateur WPF (v2), à retirer lors de la mise à jour.
  LegacyUninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectoAI';
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'Selecto AI';
  MutexName = 'Local\SelectoAI.SingleInstance';
  AppDataFolder = 'SelectoAI';
  AttrDirectory = $10;
  AttrReparsePoint = $400;

function ParamPresent(const Name: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Name) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function NextVersionPart(var S: String): Integer;
var
  P: Integer;
begin
  P := Pos('.', S);
  if P = 0 then
  begin
    Result := StrToIntDef(S, 0);
    S := '';
  end
  else
  begin
    Result := StrToIntDef(Copy(S, 1, P - 1), 0);
    Delete(S, 1, P);
  end;
end;

function CompareVersions(A, B: String): Integer;
var
  I, X, Y: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    X := NextVersionPart(A);
    Y := NextVersionPart(B);
    if X < Y then
    begin
      Result := -1;
      Exit;
    end;
    if X > Y then
    begin
      Result := 1;
      Exit;
    end;
  end;
end;

function InstalledVersion: String;
var
  V: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU, UninstallKeyPath, 'DisplayVersion', V) then Result := V
  else if RegQueryStringValue(HKLM, UninstallKeyPath, 'DisplayVersion', V) then Result := V
  else if IsWin64 and RegQueryStringValue(HKLM64, UninstallKeyPath, 'DisplayVersion', V) then Result := V;
end;

// Ferme SelectoAI.exe : fermeture douce d'abord (taskkill sans /F), puis forcée si le mutex reste pris.
// Le code retour de taskkill n'est pas utilisé (1 = « ne peut être terminé que de force », fréquent pour une appli
// de zone de notification sans fenêtre) : seul le mutex d'instance fait foi. Result = True si l'application ne tourne plus.
function CloseRunningApp: Boolean;
var
  RC, I: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#ExeName}', '', SW_HIDE, ewWaitUntilTerminated, RC);
  for I := 1 to 12 do
  begin
    if not CheckForMutexes(MutexName) then break;
    Sleep(250);
  end;
  if CheckForMutexes(MutexName) then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#ExeName}', '', SW_HIDE, ewWaitUntilTerminated, RC);
    for I := 1 to 40 do
    begin
      if not CheckForMutexes(MutexName) then break;
      Sleep(250);
    end;
  end;
  Result := not CheckForMutexes(MutexName);
end;

function InitializeSetup: Boolean;
var
  I: Integer;
begin
  // Mise à jour lancée par l'application : elle se ferme juste avant. On attend qu'elle libère son mutex (10 s au plus).
  for I := 1 to 40 do
  begin
    if not CheckForMutexes(MutexName) then break;
    Sleep(250);
  end;
  Result := True;
  // Toujours ouverte : on la ferme ICI, avant la vérification AppMutex d'Inno, sinon la boîte
  // « Selecto AI est actuellement en cours d'exécution » apparaît (et le mode silencieux sort en code 1).
  // Silencieux : fermeture sans question. Interactif : Oui = fermer et continuer, Non = annuler l'installation.
  if CheckForMutexes(MutexName) then
  begin
    if WizardSilent then
      Result := CloseRunningApp
    else if MsgBox(CustomMessage('CloseAppQuestion'), mbConfirmation, MB_YESNO) = IDYES then
      Result := CloseRunningApp
    else
      Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Installed: String;
begin
  Result := '';
  // Refus de rétrograder sauf /FORCE.
  Installed := InstalledVersion;
  if (Installed <> '') and (CompareVersions('{#Version}', Installed) < 0) and (not ParamPresent('/FORCE')) then
  begin
    Result := FmtMessage(CustomMessage('DowngradeBlocked'), [Installed, '{#Version}']);
    Exit;
  end;
  // Si l'application tourne encore malgré la proposition de fermeture, on termine SelectoAI.exe.
  if CheckForMutexes(MutexName) then CloseRunningApp;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if CheckForMutexes(MutexName) then CloseRunningApp;
end;

// Retire l'entrée de l'ancien installateur (v2) seulement si elle désigne ce même dossier d'installation,
// avec son Uninstall.exe. Réglages, dossiers %APPDATA%/%LOCALAPPDATA% et clé Run ne sont pas touchés.
procedure RemoveLegacyEntry(Root: Integer);
var
  Loc: String;
begin
  if RegQueryStringValue(Root, LegacyUninstallKeyPath, 'InstallLocation', Loc) and
     (CompareText(RemoveBackslashUnlessRoot(Loc), RemoveBackslashUnlessRoot(ExpandConstant('{app}'))) = 0) then
  begin
    RegDeleteKeyIncludingSubkeys(Root, LegacyUninstallKeyPath);
    if FileExists(ExpandConstant('{app}\Uninstall.exe')) then DeleteFile(ExpandConstant('{app}\Uninstall.exe'));
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RemoveLegacyEntry(HKCU);
    // Installation « pour tous les utilisateurs » : l'ancienne entrée éventuelle est en HKLM (accès refusé sinon).
    if IsAdminInstallMode then RemoveLegacyEntry(HKLM);
  end;
end;

function DirSizeBytes(const Dir: String): Int64;
var
  FR: TFindRec;
  Full: String;
  Sz: Int64;
begin
  Result := 0;
  if not DirExists(Dir) then Exit;
  if FindFirst(AddBackslash(Dir) + '*', FR) then
  begin
    try
      repeat
        if (FR.Name <> '.') and (FR.Name <> '..') and ((FR.Attributes and AttrReparsePoint) = 0) then
        begin
          Full := AddBackslash(Dir) + FR.Name;
          if (FR.Attributes and AttrDirectory) <> 0 then
            Result := Result + DirSizeBytes(Full)
          else if FileSize64(Full, Sz) then
            Result := Result + Sz;
        end;
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
end;

function FormatSize(const Dir: String; Bytes: Int64): String;
var
  Tenths: Int64;
begin
  if not DirExists(Dir) then
    Result := CustomMessage('SizeNone')
  else if Bytes >= 1073741824 then
  begin
    Tenths := (Bytes * 10) div 1073741824;
    Result := FmtMessage(CustomMessage('SizeGB'), [IntToStr(Tenths div 10) + '.' + IntToStr(Tenths mod 10)]);
  end
  else
    Result := FmtMessage(CustomMessage('SizeMB'), [IntToStr(Bytes div 1048576)]);
end;

// Arrête uniquement les processus lancés depuis le dossier privé de Selecto AI (le moteur Ollama privé),
// jamais un Ollama installé par l'utilisateur ailleurs.
procedure StopPrivateEngine(const LocalDir: String);
var
  Dir, Cmd: String;
  RC: Integer;
begin
  Dir := LocalDir;
  StringChangeEx(Dir, '''', '''''', True);
  Cmd := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith(''' + Dir + '\'', [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force"';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Cmd, '', SW_HIDE, ewWaitUntilTerminated, RC);
  Sleep(1000);
end;

procedure PurgeUserData(const RoamingDir, LocalDir: String);
begin
  StopPrivateEngine(LocalDir);
  if DirExists(RoamingDir) then DelTree(RoamingDir, True, True, True);
  if DirExists(LocalDir) then DelTree(LocalDir, True, True, True);
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\SelectoAI');
end;

// Supprime les sous-dossiers VIDES de Dir (restes de la v2, que Inno n'a pas journalises), puis Dir s'il est vide.
// Aucun fichier n'est supprime : un dossier qui contient un fichier, ou un point de reparsement, reste en place.
function RemoveEmptyDirs(const Dir: String): Boolean;
var
  FR: TFindRec;
  Full: String;
  AllEmpty: Boolean;
begin
  AllEmpty := True;
  if FindFirst(AddBackslash(Dir) + '*', FR) then
  begin
    try
      repeat
        if (FR.Name <> '.') and (FR.Name <> '..') then
        begin
          Full := AddBackslash(Dir) + FR.Name;
          if ((FR.Attributes and AttrDirectory) <> 0) and ((FR.Attributes and AttrReparsePoint) = 0) then
          begin
            if not RemoveEmptyDirs(Full) then AllEmpty := False;
          end
          else
            AllEmpty := False;
        end;
      until not FindNext(FR);
    finally
      FindClose(FR);
    end;
  end;
  if AllEmpty then
    Result := RemoveDir(Dir)
  else
    Result := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RoamingDir, LocalDir: String;
  RoamingSize, LocalSize: Int64;
  Purge: Boolean;
begin
  if CurUninstallStep = usUninstall then
    // Entrée « Lancer avec Windows » (qu'elle vienne de l'installateur ou de l'application).
    RegDeleteValue(HKCU, RunKeyPath, RunValueName);

  if CurUninstallStep = usPostUninstall then
  begin
    RoamingDir := ExpandConstant('{userappdata}\') + AppDataFolder;
    LocalDir := ExpandConstant('{localappdata}\') + AppDataFolder;
    if UninstallSilent then
      Purge := ParamPresent('/PURGEDATA')
    else if DirExists(RoamingDir) or DirExists(LocalDir) then
    begin
      RoamingSize := DirSizeBytes(RoamingDir);
      LocalSize := DirSizeBytes(LocalDir);
      // Réponse par défaut : NON.
      Purge := MsgBox(FmtMessage(CustomMessage('PurgeQuestion'), [
        RoamingDir, FormatSize(RoamingDir, RoamingSize), LocalDir, FormatSize(LocalDir, LocalSize)]),
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    end
    else
      Purge := False;
    if Purge then PurgeUserData(RoamingDir, LocalDir);
    // Sous-dossiers vides laisses par la v2, puis le dossier d'installation s'il est vide (aucun fichier touche).
    RemoveEmptyDirs(ExpandConstant('{app}'));
  end;
end;
