# Édition Store (MSIX) : Microsoft Store

Statut : **codée, non compilée, non testée** (aucun `dotnet` sur le Mac de développement ; tout se vérifie dans la VM Windows 11 ARM64, voir « Non testable hors Store »). Rien n'est publié.

## Modèle : gratuit, logiciel libre (MIT)

Une seule fiche Store : application **gratuite** (prix « Gratuit » dans le Partner Center), sans essai, sans achat dans l'application, code public sous licence MIT (https://github.com/warezfr/selecto-ai). Depuis la 4.0.0 l'édition `Store` ne demande plus rien au Store : plus de licence, d'essai ni d'achat. Elle ne change que l'emballage et deux comportements :

- le démarrage avec Windows passe par la tâche de démarrage du manifeste (`SelectBar/Edition/Store/StoreStartup.cs`) ;
- pas de vérification de mise à jour dans l'application : le Store met à jour (la ligne « Rechercher une mise à jour » de À propos est absente).

## Différences Site / Store

| | Site | Store |
|---|---|---|
| Distribution | Installateur Inno Setup, ZIP portable (GitHub Releases, site) | Microsoft Store (MSIX) |
| Signature | certificat du vendeur (SmartScreen) | signé par Microsoft à la soumission |
| Mises à jour | « Rechercher une mise à jour » (`latest.json`) | **automatiques par le Store** |
| Lancer avec Windows | valeur `HKCU\...\Run` | **`desktop:StartupTask`** (`SelectoStartup`) ; l'utilisateur peut le couper dans le Gestionnaire des tâches |
| Données (`%APPDATA%`, `%LOCALAPPDATA%`) | dossiers réels | **redirigées** vers le magasin privé du paquet (`%LOCALAPPDATA%\Packages\<PFN>\LocalCache`), supprimées à la désinstallation |
| Framework cible | `net8.0-windows` | `net8.0-windows10.0.19041.0` (projection WinRT) |
| Constante | aucune | `EDITION_STORE` |

Chemins vérifiés (lecture du code) : tous les dossiers de données viennent de `Environment.GetFolderPath(ApplicationData | LocalApplicationData)` + `SelectoAI` (réglages, extensions, journal, ollama portable, tampons) : rien ne suppose un autre emplacement. Le dossier d'installation (`AppContext.BaseDirectory`) n'est lu que pour copier les extensions d'exemple. La migration depuis l'ancien dossier `SelectBar` ne concerne pas un premier lancement Store. **À vérifier dans la VM** : que le moteur Ollama (exécutable lancé depuis `%LOCALAPPDATA%\SelectoAI\ollama`, centaines de Mo de modèles) fonctionne bien depuis le dossier redirigé, y compris pour le processus enfant.

## Construire

Prérequis : SDK .NET 8, SDK Windows 10/11 (`makeappx.exe`, `signtool.exe`, `makepri.exe`), logos dans `selecto\branding\dist` (déjà présents : `StoreLogo`, `Square44x44Logo`, `Square150x150Logo`, `Wide310x150Logo`, variantes `scale-*`).

```powershell
# Paquets pour le Partner Center (NON signés : Microsoft signe)
.\scripts\build-msix.ps1 -IdentityName "<Identity Name>" -Publisher "CN=<...>" -PublisherDisplayName "<nom>"
# => dist\msix\SelectoAI-<version>-x64.msix, -arm64.msix et SelectoAI-<version>.msixbundle  (à envoyer : le .msixbundle)

# Paquet de TEST signé avec un certificat auto-signé (sideload dans la VM, jamais pour la soumission)
.\scripts\new-test-cert.ps1 -Trust          # administrateur
.\scripts\build-msix.ps1 -Sign -Arch arm64   # ou x64 ; Identity de test SelectoAI.Test / CN=SelectoAITest
Add-AppxPackage .\dist\msix\SelectoAI-2.0.0.0-arm64.msix
```

Version MSIX : quatre nombres `x.y.z.0` (dernier nombre réservé au Store), tirée de `<Version>` de `SelectBar.csproj` ou de `-Version`. Chaque nouvelle soumission doit avoir une version supérieure. `-SkipPublish` réutilise `dist\publish\Store-<arch>`.

### Windows App Certification Kit (avant de soumettre)

Sur un Windows avec le SDK, en administrateur : paquet de test installé (voir plus haut), puis
```powershell
$kit = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe"
& $kit reset
& $kit test -apptype desktop -packagefullname (Get-AppxPackage *SelectoAI*).PackageFullName -reportoutputpath .\dist\msix\wack-report.xml
```
Ou l'interface « Windows App Cert Kit » > Validate Microsoft Store App. Corriger tout échec avant d'envoyer ; le Store refait ses propres contrôles. Sur Windows ARM64, lancer le kit avec le paquet arm64.

## Pas à pas du Partner Center (compte particulier)

Les intitulés de menus ci-dessous sont donnés de mémoire (l'interface du Partner Center change) : les confirmer à l'écran.

1. **Compte** : https://partner.microsoft.com/dashboard > programme développeur Windows & Xbox > inscription **individuelle** (gratuite pour un particulier). Vérification d'identité demandée par Microsoft.
2. **Réserver le nom** « Selecto AI » : Applications et jeux > Nouveau produit > Application MSIX ou PWA > réserver le nom. (Le nom doit être libre ; la réservation dure 3 mois.)
3. **Identité du produit** : Gestion des produits > Identité du produit : copier `Package/Identity/Name`, `Package/Identity/Publisher` (`CN=...`) et `PublisherDisplayName`, puis les passer à `build-msix.ps1`. L'identifiant du produit (12 caractères, visible dans l'URL de la fiche) peut aller dans `EditionInfo.StoreProductId` ; sans lui, la fiche s'ouvre par le nom de famille de paquet.
4. **Tarification et disponibilité** : marchés (France et autres), **prix : Gratuit**, sans essai, date de publication, visibilité publique. 
5. **Propriétés** : catégorie **Productivité** ; politique de confidentialité : URL `https://selecto.span.observer/confidentialite/` (la page existe en modèle ; voir « À faire avant soumission ») ; site web `https://selecto.span.observer/` ; coordonnées de support `contact@span.observer` (à confirmer) ; « L'application accède à Internet » : oui.
6. **Classification par âge** : questionnaire IARC. Réponses attendues : pas de violence, de contenu sexuel, de jeux d'argent ni d'achats in-app ; l'application ne contient pas de contenu généré par les utilisateurs partagé avec d'autres ; elle permet cependant d'envoyer du texte à des services d'IA choisis par l'utilisateur : le dire honnêtement si le questionnaire pose la question.
7. **Soumission** (Packages) : envoyer `SelectoAI-<version>.msixbundle` (x64 + arm64). Déclarer la capacité restreinte **runFullTrust** (justification prête dans `store-listing-fr.md`, « Notes pour la certification »).
8. **Fiche Store** : une langue par fiche, **fr-FR** (`store-listing-fr.md`) et **en-US** (`store-listing-en.md`) : description, nouveautés, mots-clés, notes de certification. **Captures** (au moins une, 1366x768 ou plus ; 4 à 8 recommandées) depuis `selecto/captures/v2/` : `reglages-general.png`, `reglages-actions.png`, `reglages-ia.png`, `reglages-general-dark.png`, `assistant-bienvenue-2.png`. Attention : les anciennes captures montrent des écrans d'avant la 4.0.0 (badge, champ de clé) ; refaire celles de la page À propos et du flyout avec l'édition Store, par exemple `SELECTO_STORE_FAKE=pro`. Logos de la fiche : tuile 300x300 et 1080x1080 à produire depuis `branding/` (la fiche accepte celles du paquet si manquantes, mais en fournir de dédiées améliore l'affichage).
9. **Soumettre pour certification**. Délai habituel : de quelques heures à quelques jours ouvrés. En cas de refus, le rapport indique les politiques en cause ; corriger et renvoyer une nouvelle version du paquet.
10. **Mises à jour** : incrémenter la version (`<Version>` dans `SelectBar.csproj` ou `-Version`), reconstruire, créer une nouvelle soumission dans le même produit avec le nouveau bundle. Les utilisateurs reçoivent la mise à jour automatiquement via le Store.

## À faire avant soumission

- **Politique de confidentialité** : la page `selecto/site/confidentialite/` est un MODÈLE avec des blancs `[À COMPLÉTER]` (adresse, SIRET, base légale, autorité de contrôle) à compléter et faire valider ; elle ne doit plus parler de licences ni de ventes (le logiciel est gratuit). Elle affirme que l'application n'envoie aucune télémétrie d'usage ; le README de l'application ne le dit pas : ne pas reprendre cette affirmation dans la fiche Store tant qu'elle n'est pas vérifiée dans le code et assumée par le vendeur (c'est pourquoi les descriptions ne la contiennent pas).
- Renseigner `EditionInfo.StoreProductId` (facultatif) une fois le produit créé.

## Non testable hors Store

- La **tâche de démarrage** (`StartupTask`) : demande un paquet installé ; le comportement après « désactivé par l'utilisateur » (état `DisabledByUser`) ne peut pas être forcé par l'application.
- La **redirection de `%APPDATA%`/`%LOCALAPPDATA%`** et le fonctionnement d'Ollama depuis ce dossier.
- La **certification** elle-même (les contrôles du Store vont au-delà du Windows App Certification Kit).
