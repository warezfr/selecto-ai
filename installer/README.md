# Installateurs Windows de Selecto AI

## Formats de distribution

| Format | Rôle | Statut |
| --- | --- | --- |
| **`SelectoAI-Setup-<arch>-<version>.exe`** (Inno Setup, `SelectoAI.iss`, sortie `dist\installers\`) | **Installateur PRINCIPAL** (version 4.0.0) : assistant standard, `/VERYSILENT` pour le silencieux, winget et entreprise, « pour tous les utilisateurs » possible. Produit par `scripts\build-installer.ps1`. | Écrit, jamais compilé dans cette session (à valider dans la VM) |
| **`SelectoAI-Setup-Animated-<arch>-<version>.exe`** (projet `SelectBar.Setup`, sortie `dist\installers-animated\`) | **Optionnel** : installateur WPF Fluent animé (Mica, clair/sombre, logo « S. »), un seul exécutable autonome avec l'application accolée à la fin. Son début sert de désinstallateur (`Uninstall.exe`). | Écrit, **jamais compilé ni lancé** (voir « Ce qui reste ») |
| MSIX (`msix/`) | Microsoft Store (édition `Store`, gratuite). | Codé, à tester dans la VM ; non publié |
| MSI, zip | **Pas prévus.** | |

Emballage Site, x64 et ARM64, publication .NET **autonome** (aucun prérequis pour l'utilisateur).

## Pourquoi Inno Setup est l'installateur principal

- **Moins d'alertes antivirus** : Inno Setup produit un installateur standard, sans charge utile zippée accolée à un stub auto-extractible. Ce « packer » fait partie des comportements qui déclenchent les heuristiques (Bitdefender a signalé l'exe téléchargé comme peu fiable).
- **Pas de relance depuis `%TEMP%`** ni de script `cmd` caché pour la désinstallation : le désinstallateur Inno est celui de l'éditeur, reconnu.
- **Standard attendu** pour le silencieux (`/VERYSILENT`), winget et les déploiements en entreprise.
- L'installateur WPF animé reste disponible en option (`scripts\build-setup.ps1`, dossier `dist\installers-animated`) pour ceux qui veulent l'expérience d'installation Fluent.
- Signature : mêmes variables `SELECTO_SIGN_*` pour les deux installateurs.

## Selecto AI Setup (installateur optionnel, animé)

### Comportement

- Par utilisateur, sans droits administrateur (`asInvoker`) : `%LOCALAPPDATA%\Programs\Selecto AI` par défaut (l'emplacement d'une installation existante est repris pour une mise à jour). Même `AppId` que les 3.x (Démo et Pro) : la 4.0.0 remplace une 3.x installée. Une version plus ancienne est refusée, sauf `/FORCE`.
- Écrans (fenêtre fixe 760×480, `FluentWindow`, Mica, suit le thème de Windows) : **Bienvenue** (logo animé, accroche, « Installer », « Options » : dossier, raccourci Bureau, lancer avec Windows, langue FR/EN) → **Installation** (anneau de progression, étapes Préparation / Copie des fichiers / Raccourcis / Finalisation, carrousel de 3 diapositives, annulation avec retour en arrière) → **Terminé** (coche animée, « Lancer Selecto AI » coché) ; **Erreur** (message, « Copier les détails », « Réessayer », journal `%TEMP%\SelectoAI-Setup.log`) ; **Désinstallation** (confirmation, case « Supprimer aussi mes réglages et mon IA locale (X Mo) » décochée par défaut, progression, fin).
- Animations : opacité, translation et tracé uniquement, 120 à 700 ms, supprimées si `SystemParameters.ClientAreaAnimation` est faux ; seule boucle : la barre flottante de démonstration de l'écran d'accueil (6 s, une seule opacité/translation), arrêtée en quittant l'écran.
- Reprend les comportements de l'installateur Inno : mutex `Local\SelectoAI.SingleInstance` (fermeture propre de l'application, puis arrêt de `SelectoAI.exe` si besoin ; relancée à la fin si elle tournait en mode silencieux ; en interactif, c'est la case « Lancer Selecto AI » de l'écran final), raccourcis Menu Démarrer (+ Bureau en option), valeur `HKCU\...\Run` « Selecto AI » identique à celle de l'application (`StartupEntries`), arrêt du moteur Ollama privé **uniquement** par le chemin de son processus, purge facultative (`%APPDATA%\SelectoAI`, `%LOCALAPPDATA%\SelectoAI`, `HKCU\Software\SelectoAI`), jamais le Ollama de l'utilisateur.
- Entrée « Applications installées » : `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectoAI` (nom, version, éditeur, icône, taille, site, `UninstallString`). Si une installation Inno de la même application existe pour l'utilisateur, son entrée `{...}_is1` est retirée (le Setup la remplace ; une installation Inno « pour tous » en HKLM n'est pas touchée).
- Désinstallation : le Setup copie son début (le « stub », sans la charge utile, voir « Taille ») en `<dossier>\Uninstall.exe`. Lancé avec `--uninstall`, il désinstalle **sur place** (aucune copie dans `%TEMP%`, aucun script `cmd` caché). Son propre `Uninstall.exe` étant verrouillé pendant l'opération, il est supprimé **au prochain redémarrage** (`MoveFileEx` avec `MOVEFILE_DELAY_UNTIL_REBOOT`), ainsi que le dossier, s'il est vide à ce moment. Si la suppression différée échoue, le fichier est listé dans le journal `%TEMP%\SelectoAI-Setup.log`. Garde-fous : le dossier doit avoir au moins deux niveaux, ne pas être sous `Windows` ni un profil utilisateur entier, et contenir `SelectoAI.exe` ou `Uninstall.exe`.
- Annulation / échec : chaque modification est défaite dans l'ordre inverse (fichiers remplacés restaurés depuis une sauvegarde temporaire, fichiers et dossiers créés supprimés, raccourcis, valeur Run). La finalisation (entrée du registre) est le point de non-retour.

### Ligne de commande

| Option | Effet |
| --- | --- |
| *(aucune)* | installation interactive |
| `--uninstall` | désinstallation interactive |
| `/S`, `--silent` (aussi `/SILENT`, `/VERYSILENT`) | sans interface, code de sortie 0 (succès) ou 1 (échec) |
| `/DIR="C:\chemin"` | dossier d'installation |
| `/PURGEDATA` | avec `--uninstall --silent` : supprime aussi réglages et IA locale |
| `/FORCE` | autorise la rétrogradation |
| `/DESKTOPICON`, `/STARTUP`, `/TASKS="desktopicon,startup"` | raccourci Bureau, lancer avec Windows (silencieux) |
| `/NORESTART`, `/SUPPRESSMSGBOXES`, `/CURRENTUSER` | acceptés et ignorés (compatibilité Inno) |
| `--theme=light\|dark`, `--lang=fr\|en` | forcer le thème / la langue |
| `--demo` | rejoue les écrans automatiquement **sans rien installer** (accueil 4,5 s, progression simulée 12 s, fin) |
| `--demo --screen=<welcome\|progress\|done\|error\|uninstall\|uninstall-progress\|uninstall-done> --hold` | fige un écran (pour captures d'images et vidéos) |

### Compiler et tester (VM Windows 11, SDK .NET 8)

```powershell
.\scripts\build-setup.ps1                                # x64 + arm64
.\scripts\build-setup.ps1 -Arch arm64                    # une seule architecture
.\scripts\build-setup.ps1 -ReadyToRun                    # démarrage plus rapide, fichiers plus gros
.\scripts\build-setup.ps1 -SkipPublish                   # réutilise dist\publish\Site-<arch>

dotnet test SelectBar.Setup.Tests                         # logique pure (aussi sur Mac/Linux)
dotnet run --project SelectBar.Setup -- --demo --lang=fr  # voir les écrans sans publier ni installer
.\scripts\verify-setup.ps1 -Installer .\dist\installers-animated\SelectoAI-Setup-Animated-arm64-4.0.0.exe   # acceptation
```

`build-setup.ps1` publie l'application, la zippe (`dist\payload\Site-<arch>\payload.zip`, sans `.pdb`), publie `SelectBar.Setup` en **fichier unique autonome compressé** sans charge utile (le stub), signe le stub, lui accole la charge utile (voir « Taille »), puis produit dans `dist\installers-animated\` : `SelectoAI-Setup-Animated-<arch>-<version>.exe`, `SHA256SUMS.txt`, `latest.json`. Signature : mêmes variables que pour Inno (`SELECTO_SIGN_PFX` + `SELECTO_SIGN_PASSWORD`, ou `SELECTO_SIGN_THUMBPRINT`) ; sinon **non signé**, avec avertissement (SmartScreen affichera « éditeur inconnu »).

Le dossier `dist\installers\` reçoit la sortie de `build-installer.ps1` (Inno, installateur principal) ; `dist\installers-animated\` reçoit celle de `build-setup.ps1`, pour ne pas écraser les fichiers.

---

# Installateur Inno Setup (silencieux, winget, entreprise)

Installateur **Inno Setup 6** (script `SelectoAI.iss`), un seul script pour l'emballage Site, en x64 et ARM64. Rien n'a encore été compilé : à valider dans une VM Windows.

## Habillage de l'assistant Inno (thème Fluent, logo « S. »)

- `WizardStyle=modern`, `SetupIconFile` = `SelectBar\Assets\SelectoAI.ico`. Page d'accueil et page finale : image latérale (dégradé bleu `#0067C0` vers marine, logo « S. », « Selecto AI », « Sélectionne. Agis. »). Autres pages : logo « S. » en en-tête. Textes d'accueil et de fin en français et en anglais (`[Messages]` de `SelectoAI.iss`).
- Bitmaps dans `installer\wizard\` (BMP 24 bits) : `wizard-large*.bmp` (164x314, 246x471, 328x628) et `wizard-small*.bmp` (55x55, 83x83, 110x110), plus les variantes `-dark`. Inno choisit la taille selon le DPI (listes séparées par des virgules).
- Régénération (Mac, Node 22 + Chrome, sans dépendance) : `node <selecto>/branding/build-installer-images.mjs installer/wizard`. Le script vit dans le dépôt `selecto` (`branding/`) avec les SVG maîtres ; la compilation s'arrête avec un message clair si les BMP manquent.
- Assistant clair/sombre automatique : Inno Setup **6.6 ou plus** et `/DDarkWizard` (`WizardStyle=modern dynamic` + `WizardImageFileDynamicDark`). Désactivé par défaut. **Non vérifié** (noms de directives et rendu).

## Compiler (VM Windows 10/11)

Prérequis : SDK .NET 8, **Inno Setup 6.3 ou plus** (`winget install -e --id JRSoftware.InnoSetup`, ou `-InstallInno` ci-dessous), et l'icône `SelectBar\Assets\SelectoAI.ico` (sinon la compilation s'arrête avec un message clair).

```powershell
.\scripts\build-installer.ps1                              # x64 + arm64
.\scripts\build-installer.ps1 -Arch arm64                  # une seule architecture
.\scripts\build-installer.ps1 -ReadyToRun                  # démarrage plus rapide, publication plus grosse
.\scripts\build-installer.ps1 -SkipPublish                 # réutilise dist\publish\Site-<arch>
.\scripts\build-installer.ps1 -InstallInno                 # installe Inno Setup via winget s'il manque (jamais sans ce paramètre)
```

Directement avec Inno : `ISCC.exe /DArch=x64 /DVersion=4.0.0 /DSourceDir=<dossier publié> /DOutputDir=<sortie> installer\SelectoAI.iss`.

## Fichiers produits (`dist\installers\`)

- `SelectoAI-Setup-<arch>-<version>.exe`
- `SHA256SUMS.txt`
- `latest.json` : version, édition, architecture, taille, SHA-256 et URL (à compléter) pour une future mise à jour automatique.

Tailles attendues (Inno) : publication ~150 à 200 Mo, installateur ~60 à 90 Mo (LZMA2 solide).

## Installer, mettre à jour, désinstaller

- Interactif : par utilisateur par défaut (`%LOCALAPPDATA%\Programs\Selecto AI`, sans droits administrateur) ; la boîte de choix permet « pour tous les utilisateurs ». Langues : français (défaut) et anglais. Tâches : raccourci Bureau (décoché), « Lancer Selecto AI avec Windows » (décoché, même valeur `HKCU\...\Run` « Selecto AI » que l'application), option finale « Lancer Selecto AI » (cochée).
- Silencieux : `SelectoAI-Setup-....exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="C:\chemin"`.
- **Mise à jour** : réinstaller une version plus récente par-dessus conserve tous les réglages (ils sont dans `%APPDATA%\SelectoAI` et `%LOCALAPPDATA%\SelectoAI`, jamais dans le dossier du programme). La 4.0.0 remplace une 3.x installée (même `AppId`). Une version plus ancienne est refusée, sauf `/FORCE`. Si Selecto AI tourne (mutex `Local\SelectoAI.SingleInstance`) : en silencieux, l'installateur le ferme sans question (`taskkill /IM SelectoAI.exe`, puis `/F` si le mutex reste pris, jusqu'à 10 s) ; en interactif, il demande « Selecto AI est ouvert. Le fermer pour continuer ? » (Oui = fermeture et suite, Non = annulation). La vérification `AppMutex` d'Inno ne s'affiche plus, l'application étant déjà fermée (`AppMutex` et `CloseApplications` restent en filet de sécurité).
- **Mise à jour depuis la v2** (installateur WPF `SelectoAI-Setup-Animated-…`) : deux entrées pouvaient rester dans « Applications installées ». Après installation, l'entrée `HKCU\…\Uninstall\SelectoAI` de la v2 et son `<dossier>\Uninstall.exe` sont retirés, **uniquement** si son `InstallLocation` désigne le même dossier (en HKLM pour une installation « pour tous »). Restent intacts : réglages (`%APPDATA%`, `%LOCALAPPDATA%`), entrée `HKCU\…\Run`, et l'entrée Inno `{…}_is1` de la nouvelle installation.
- **Désinstallation** : « Applications installées » ou `unins000.exe /VERYSILENT [/PURGEDATA]` dans le dossier d'installation. En interactif, une boîte demande s'il faut supprimer aussi les réglages et l'IA locale (taille calculée de `%APPDATA%\SelectoAI` et `%LOCALAPPDATA%\SelectoAI`), réponse par défaut **Non**. En silencieux, les données ne sont supprimées qu'avec `/PURGEDATA`. Un Ollama installé séparément et `~/.ollama` ne sont jamais touchés.
- Après désinstallation (v3), les sous-dossiers **vides** laissés par la v2 dans le dossier d'installation sont retirés, puis le dossier lui-même s'il est vide ; aucun fichier n'est supprimé.
- Test d'acceptation dans la VM : `.\scripts\verify-installer.ps1 -Installer .\dist\installers\<fichier>.exe` (installe, contrôle, lance `--selftest`, désinstalle, vérifie ; code de sortie 0 si tout est OK).

## Signature de code et SmartScreen

Aucun secret dans le dépôt. Variables d'environnement lues par `build-installer.ps1` : `SELECTO_SIGN_PFX` (chemin du .pfx) + `SELECTO_SIGN_PASSWORD`, ou `SELECTO_SIGN_THUMBPRINT` (certificat du magasin). Le script signe `SelectoAI.exe` puis l'installateur (le désinstallateur interne n'est pas signé). Sans ces variables, l'installateur est produit **non signé** (avertissement affiché) et **SmartScreen affichera « éditeur inconnu »** ; même signé, un certificat sans réputation (hors EV) déclenche l'avertissement un certain temps.

## Licence affichée

`license-fr.txt`, `license-en.txt` et `license-es.txt` contiennent le texte de la licence MIT (identique au fichier `LICENSE` à la racine), précédé d'une ligne d'introduction.

## Microsoft Store et winget

- `msix/` : manifeste modèle, `scripts\build-msix.ps1`, `scripts\new-test-cert.ps1`, fiches Store FR/EN et README (édition Store gratuite, Partner Center).
- `winget/` : trois manifestes modèles (version, installateur, locale fr-FR) ; URL et SHA-256 à compléter après l'hébergement des fichiers.

## Ce qui reste à décider ou vérifier

- Certificat de signature de code (achat, EV ou non) ; hébergement des installateurs et de `latest.json`.
- Microsoft Store : compte développeur, nom réservé, modèle de licence Store (voir `msix/README.md`).
- Identifiant winget définitif.
- Rendu de l'assistant Inno (images latérales et d'en-tête, DPI 150/200 %, textes de bienvenue/fin, variante `/DDarkWizard`).
- Compilation et tests réels (ISCC, installation, fermeture d'une instance en cours, ARM64) dans la VM.

## Taille de Selecto AI Setup

Avant : installateur 223 Mo, installation 405 Mo, parce que `Uninstall.exe` était une copie complète du Setup (runtime .NET non compressé ~157 Mo + charge utile 66 Mo). L'application seule occupe donc ~182 Mo une fois extraite (405 - 223) ; **« installation ≤ 150 Mo » est impossible sans réduire l'application elle-même** (hors périmètre du Setup).

Ce qui est fait :
- **(a) Compression du fichier unique** (`EnableCompressionInSingleFile`) : le runtime et les assemblages du Setup sont compressés (estimé 157 → ~65-75 Mo). Coût : décompression en mémoire au démarrage (1 à 3 s de plus, plus sur ARM), à mesurer dans la VM.
- **(b) Désinstallateur sans charge utile** : le Setup est publié une seule fois, SANS charge utile (le « stub »), puis `build-setup.ps1` y accole `payload.zip` et un trailer de 24 octets : `[stub][payload.zip]['SELPAYL1' + longueur du stub Int64 + longueur de la charge utile Int64]` (`SelectBar.Setup\Logic\OverlayTrailer.cs`, testé). À l'exécution, le Setup lit son trailer, extrait la charge utile par un flux limité à cette plage, et copie les `longueur du stub` premiers octets en `Uninstall.exe`. Le stub est signé seul (avant l'ajout) ; **ne pas re-signer le Setup final** (le trailer doit rester en fin de fichier). Sans trailer (développement, `Uninstall.exe`), `HasPayload` vaut faux : `--demo` et `dotnet run` fonctionnent comme avant, et un ancien `Uninstall.exe` (copie complète) se désinstalle toujours.
- Attendu : installateur ≈ 66 + ~70 ≈ **135 Mo** (cible ≤ 140), `Uninstall.exe` ≈ **70 Mo**, installation ≈ 182 + 70 ≈ **250 Mo** (au lieu de 405). Le contrôle d'acceptation affiche les tailles réelles.

Écarté :
- **(c) Désinstallateur partageant le runtime de l'application installée** (`Uninstall.exe` minuscule, ~2 Mo, installation ≈ 185 Mo) : le désinstallateur se recopie dans `%TEMP%` pour pouvoir supprimer son dossier, et une copie sans `hostfxr`/`coreclr` ne démarre pas ; le lancer sur place reviendrait à supprimer le runtime sous un CLR vivant (assemblages chargés à la demande, donc `FileNotFoundException` en cours de route). Piste future si 250 Mo gêne : copier tout le dossier de l'application dans `%TEMP%` puis le supprimer par `rmdir /s /q`, ou un désinstallateur léger distinct (autre projet).
- Embarquer un second Setup sans charge utile dans le premier : l'installateur retomberait à ~200 Mo.

## Ce qui reste pour Selecto AI Setup

Rien de ce qui suit n'a été vérifié (aucun SDK .NET sur le Mac de développement : le code a été écrit sans compiler) :

- Compilation de `SelectBar.Setup` (API WPF UI 4.3 : `FluentWindow`, `TitleBar`, `Button`, `ControlAppearance`) et exécution de `SelectBar.Setup.Tests`.
- Tout le rendu : Mica, thème clair/sombre, animations (tracé du S, chute du point, coche, carrousel, barre flottante), fluidité dans la VM ARM, coins arrondis, boutons de la barre de titre.
- Tout le comportement Windows : extraction de la charge utile, rollback sur annulation, fermeture/relance de l'application, raccourcis (`WScript.Shell`), clé Run, entrée « Applications installées », désinstallation différée (`Uninstall.exe`, dossier, copie temporaire), purge des données, code de sortie en silencieux, fichier unique autonome ARM64.
- Taille (voir la section dédiée) : toutes les valeurs sont des estimations non mesurées ; démarrage du stub compressé sur une VM ARM, comportement du fichier unique avec des octets ajoutés à sa fin, et signature du stub à contrôler (`Get-AuthenticodeSignature` sur `Uninstall.exe` ET sur l'installateur) dans la VM.
- Pas de choix « pour tous les utilisateurs » dans le Setup (réservé à Inno).
