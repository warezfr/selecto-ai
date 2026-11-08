# Selecto AI : notes de version

## Français

### 4.0.0

Selecto AI est maintenant **gratuit et libre (licence MIT)**. Le code est public : https://github.com/warezfr/selecto-ai

- **Plus d'essai, de clé ni de prix** : toutes les fonctions sont disponibles, sans limite de durée et sans compte. L'achat dans l'application, l'activation d'une clé, la vérification de licence en ligne et l'empreinte d'appareil ont disparu. Les réglages de la 3.x se chargent tels quels.
- **Une seule version** : plus de « Version démo » ni de « Pro ». Le badge d'état (barre de titre, menu de la zone de notification) disparaît.
- **Écran d'accueil** : le troisième écran devient « Logiciel libre et gratuit », avec le lien vers GitHub.
- **À propos** : ligne « Logiciel libre (MIT) », liens (site, confidentialité, code source), texte de la licence MIT (À propos › Licence) et « Rechercher une mise à jour » (hors Microsoft Store).
- **Installation** : `SelectoAI-Setup-x64-4.0.0.exe` et `SelectoAI-Setup-arm64-4.0.0.exe` (Inno Setup), `SelectoAI-x64-portable-4.0.0.zip` et `SelectoAI-arm64-portable-4.0.0.zip` (sans installation), `SelectoAI-4.0.0.0.msixbundle` (Microsoft Store, gratuit). La 4.0.0 remplace proprement une 3.x installée : même dossier, mêmes réglages.
- **Conservé** : « Signaler un problème », les extensions, la confirmation à la fermeture (Réglages › Général) et la mise à jour par le site.

### 3.2.1

Corrections de la 3.2.0, à lire en premier :

- **Raccourci Ctrl+Maj+Espace** : il affiche la barre sur le texte sélectionné, y compris après une sélection au clavier (Ctrl+A, Maj+flèches). Il est actif dès l'installation et se change dans Réglages › Général. Si vous aviez déjà choisi un raccourci, il est conservé ; il affiche désormais la barre au lieu d'activer ou de mettre en pause (la pause reste dans le menu de la zone de notification).
- **Essai sans réseau** : la page À propos indique « Hors ligne » et précise que les actions locales fonctionnent et que les actions en ligne reprendront avec le réseau.
- **Changement de langue** : le menu de la zone de notification passe dans la nouvelle langue sans redémarrage.
- **Réglages** : les pages À propos et Actions ont une barre de défilement visible ; la fenêtre ne dépasse plus un écran de petite hauteur.
- **Vocabulaire** : « Version démo » désigne l'édition, « essai » désigne les 14 jours (badge « Essai 4 j »).
- **Clé de licence refusée** : le message s'affiche en rouge sous le champ et la clé saisie est conservée pour la corriger.
- **Journal** : plus d'erreur au démarrage pour les anciens exemples d'extensions déjà renommés.

### 3.2.0

La 3.1.0 n'a jamais été publiée : cette version reprend tout son contenu (points 1 à 5 ci-dessous) et ajoute la bibliothèque d'extensions (points 6 à 8).

1. **Interface en quatre langues** : français, anglais, espagnol et arabe (écriture de droite à gauche). Réglages › Général › Langue ; « Automatique » suit la langue de Windows, sinon l'anglais. Arabe : traduction en cours de relecture.
2. **Menu de la zone de notification refondu** : état de la barre (active ou en pause) et badge de mise à jour (édition téléchargée depuis le site ; l'édition Microsoft Store se met à jour par le Store).
3. **Quitter demande confirmation**, sauf quand Windows s'arrête. **Page À propos** : liens utiles et état de la mise à jour.
4. **Extensions intégrées** (Réglages › Actions › Extensions intégrées) : Texte (MAJUSCULES, minuscules, Titre, Première lettre, supprimer les doubles espaces, joindre les lignes, trier les lignes), Compter (mots, caractères, temps de lecture) et Calculer (expressions, unités, devises). Désactivées par défaut, sauf Compter.
5. **API d'extension** : objet `selecto` (texte, modifiable, langue, copier, coller, ouvrir, afficher, résultat, calcul). Les extensions existantes continuent de fonctionner sans modification. En-tête : titres par langue (`@name:fr|en|es|ar`), `@parent`, `@enabled-by-default`, `@inline`.
6. **Bibliothèque de 22 extensions téléchargeables** (35 actions). « Télécharger d'autres extensions » (Réglages › Actions › Extensions) ouvre le catalogue du site, en français ou en anglais selon la langue de l'application. Les extensions de la bibliothèque sont téléchargées sur le site, elles ne sont pas incluses dans l'application.
7. **Ajouter une extension** accepte plusieurs fichiers à la fois, en `.js` ou en `.zip`. Un zip ne fournit que les fichiers `.js` placés à la racine (50 au plus, 256 Ko chacun, en-tête valide) ; les autres entrées sont ignorées avec un message.
8. **Réglages des extensions** : chaque extension affiche sa description, et la liste des extensions installées comporte un interrupteur pour l'activer ou la désactiver.
- **Corrections** : emoji corrigés dans Zigzag et Caviarder sous Jint.

### 3.1.0

- **Interface en quatre langues** : français, anglais, espagnol et arabe. Réglages › Général › Langue ; « Automatique » suit la langue de Windows, sinon l'anglais. Arabe : traduction en cours de relecture.
- **Menu de la zone de notification** : état de la barre (active ou en pause) et badge de mise à jour (édition téléchargée depuis le site ; l'édition Microsoft Store se met à jour par le Store).
- **Quitter demande confirmation**, sauf quand Windows s'arrête.
- **Page À propos** : liens utiles et état de la mise à jour.
- **Extensions intégrées** (Réglages › Actions › Extensions intégrées) : sous-menu Texte (MAJUSCULES, minuscules, Titre, Première lettre, supprimer les doubles espaces, joindre les lignes, trier les lignes), Compter (mots, caractères, temps de lecture) et Calculer (expressions, unités, devises). Désactivées par défaut, sauf Compter.
- **API d'extension** : objet `selecto` (texte, modifiable, langue, copier, coller, ouvrir, afficher, résultat, calcul). Les extensions existantes continuent de fonctionner sans modification. En-tête : titres par langue (`@name:fr|en|es|ar`), `@parent`, `@enabled-by-default`, `@inline`.
- **Corrections de la 3.0.1** : journal des erreurs fatales ; préchauffage de l'IA locale plus robuste.

### 3.0.0 : nouveautés et corrections

- **Installateur plus fiable** : l'installateur principal est désormais le programme Inno Setup. Les fichiers .exe portent des métadonnées de version complètes (éditeur, nom du produit, description, copyright). Le désinstallateur ne se relance plus depuis le dossier temporaire, et l'installateur attend que l'application soit fermée avant de s'installer.
- **Réglages** : après un clic sur « Appliquer », le bouton devient « Fermer » et un message « Réglages enregistrés » s'affiche brièvement. Dès qu'un réglage est modifié, « Appliquer » revient. Fermer la fenêtre avec des changements non appliqués demande s'il faut enregistrer, ne pas enregistrer ou annuler.
- **IA locale plus rapide sur les PC modestes** : les délais de démarrage sont plus longs (jusqu'à 90 s pour démarrer le moteur, jusqu'à 180 s pour le premier chargement du modèle), avec un bouton Annuler. Option « Préparer l'IA au démarrage » (activée par défaut) : le moteur et le modèle sont chargés en arrière-plan, puis libérés après 30 min d'inactivité. Le modèle recommandé tient compte de la mémoire, du nombre de cœurs et du jeu d'instructions AVX2. Le journal `selecto.log` enregistre les durées de démarrage.
- **Actions en ligne activées par défaut** : Traduire et Dictionnaire sont disponibles dès l'installation. L'écran 2 de l'assistant de bienvenue indique qu'ils envoient le texte sélectionné à des services tiers, avec un bouton pour les désactiver. Une installation existante conserve le choix déjà fait.
- **Vérification des mises à jour** (versions téléchargées depuis le site uniquement ; la version Microsoft Store est mise à jour par le Store) : Selecto AI vérifie `latest.json` 30 secondes après le démarrage puis toutes les 24 h. Une notification propose « Installer » ou « Plus tard ». Le bouton « Rechercher maintenant » est dans À propos. L'installateur téléchargé est vérifié par SHA-256 avant lancement, puis l'application est relancée après l'installation. Le réglage « Rechercher les mises à jour automatiquement » se désactive dans les réglages.

### Installation

- Téléchargez l'installateur correspondant à votre PC (x64 ou ARM64) sur https://selecto.span.observer ou sur GitHub (Releases), ou utilisez la version Microsoft Store (gratuite).
- Version portable : décompressez le ZIP et lancez `SelectoAI.exe` (voir LISEZ-MOI.txt).
- Vérifiez le SHA-256 du fichier téléchargé avec celui publié sur le site.

## English

### 4.0.0

Selecto AI is now **free and open source (MIT License)**. The code is public: https://github.com/warezfr/selecto-ai

- **No trial, key or price any more**: every feature is available, with no time limit and no account. In-app purchase, key activation, online license checks and the device fingerprint are gone. Your 3.x settings load as they are.
- **One version**: no more "Demo version" or "Pro". The status badge (title bar, notification area menu) is gone.
- **Welcome window**: the third screen is now "Free and open-source software", with the GitHub link.
- **About**: a "Free software (MIT)" line, links (website, privacy, source code), the MIT License text (About › License) and "Check for updates" (except in the Microsoft Store version).
- **Install**: `SelectoAI-Setup-x64-4.0.0.exe` and `SelectoAI-Setup-arm64-4.0.0.exe` (Inno Setup), `SelectoAI-x64-portable-4.0.0.zip` and `SelectoAI-arm64-portable-4.0.0.zip` (no installation), `SelectoAI-4.0.0.0.msixbundle` (Microsoft Store, free). 4.0.0 cleanly replaces an installed 3.x: same folder, same settings.
- **Kept**: "Report a problem", extensions, the quit confirmation (Settings › General) and updates from the website.

### 3.2.1

Fixes for 3.2.0, worth reading first:

- **Ctrl+Shift+Space shortcut**: it shows the bar on the selected text, including after a keyboard selection (Ctrl+A, Shift+arrows). It is on from installation and can be changed in Settings › General. A shortcut you had already chosen is kept; it now shows the bar instead of turning the app on or off (pause stays in the notification area menu).
- **Trial started without a network**: the About page says "Offline" and explains that local actions work and online actions will resume when the network is back.
- **Language change**: the notification area menu switches to the new language without a restart.
- **Settings**: the About and Actions pages have a visible scroll bar; the window no longer exceeds a short screen.
- **Wording**: "Demo version" names the edition, "trial" names the 14 days (badge "Trial 4 d").
- **Rejected license key**: the message appears in red under the field and the key you typed is kept so you can fix it.
- **Log**: no more startup error for old extension samples that were already renamed.

### 3.2.0

Version 3.1.0 was never published: this release includes all of its content (points 1 to 5 below) and adds the extension library (points 6 to 8).

1. **Interface in four languages**: French, English, Spanish and Arabic (right-to-left script). Settings › General › Language; "Automatic" follows the Windows display language, otherwise English. Arabic: translation under review.
2. **Redesigned notification area menu**: bar status (on or paused) and an update badge (edition downloaded from the website; the Microsoft Store edition is updated by the Store).
3. **Quitting asks for confirmation**, except when Windows is shutting down. **About page**: useful links and update status.
4. **Built-in extensions** (Settings › Actions › Built-in extensions): Text (UPPERCASE, lowercase, Title Case, Sentence case, remove double spaces, join lines, sort lines), Count (words, characters, reading time) and Calculate (expressions, units, currencies). Turned off by default, except Count.
5. **Extension API**: the `selecto` object (text, editable, locale, copy, paste, open, show, result, calculation). Existing extensions keep working without changes. Header: titles per language (`@name:fr|en|es|ar`), `@parent`, `@enabled-by-default`, `@inline`.
6. **Library of 22 downloadable extensions** (35 actions). "Download more extensions" (Settings › Actions › Extensions) opens the website catalogue, in French or English depending on the application language. Library extensions are downloaded from the website; they are not included in the application.
7. **Add extension** accepts several files at once, as `.js` or `.zip`. A zip contributes only the `.js` files at its root (at most 50, 256 KB each, with a valid header); other entries are skipped with a message.
8. **Extension settings**: each extension shows its description, and the installed extensions list has a switch to turn each one on or off.
- **Fixes**: emoji corrected in Zigzag and Caviarder under Jint.

### 3.1.0

- **Interface in four languages**: French, English, Spanish and Arabic. Settings › General › Language; "Automatic" follows the Windows display language, otherwise English. Arabic: translation under review.
- **Notification area menu**: bar status (on or paused) and an update badge (edition downloaded from the website; the Microsoft Store edition is updated by the Store).
- **Quitting asks for confirmation**, except when Windows is shutting down.
- **About page**: useful links and update status.
- **Built-in extensions** (Settings › Actions › Built-in extensions): Text submenu (UPPERCASE, lowercase, Title Case, Sentence case, remove double spaces, join lines, sort lines), Count (words, characters, reading time) and Calculate (expressions, units, currencies). Turned off by default, except Count.
- **Extension API**: the `selecto` object (text, editable, locale, copy, paste, open, show, result, calculation). Existing extensions keep working without changes. Header: titles per language (`@name:fr|en|es|ar`), `@parent`, `@enabled-by-default`, `@inline`.
- **Fixes from 3.0.1**: fatal error log; more robust local AI warm-up.

### 3.0.0: new features and fixes

- **More reliable installer**: the main installer is now built with Inno Setup. The .exe files carry complete version metadata (publisher, product name, description, copyright). The uninstaller no longer relaunches from the temporary folder, and the installer waits for the application to close before installing.
- **Settings**: after a successful "Apply", the button changes to "Close" and a brief "Settings saved" message appears. As soon as a setting changes, "Apply" comes back. Closing the window with unapplied changes asks whether to save, discard or cancel.
- **Faster local AI on modest PCs**: startup timeouts are longer (up to 90 s to start the engine, up to 180 s for the first model load), with a Cancel button. The option "Prepare the AI at startup" (on by default) loads the engine and model in the background and releases them after 30 minutes of inactivity. The recommended model takes memory, CPU core count and AVX2 support into account. The `selecto.log` file records startup durations.
- **Online actions enabled by default**: Translate and Dictionary are available from installation. Welcome wizard step 2 explains that they send the selected text to third-party services, with a button to turn them off. An existing installation keeps the choice already made.
- **Update check** (versions downloaded from the website only; the Microsoft Store version is updated by the Store): Selecto AI checks `latest.json` 30 seconds after startup, then every 24 hours. A notification offers "Install" or "Later". The "Check now" button is in About. The downloaded installer is verified by SHA-256 before it runs, and the application restarts after installation. The "Check for updates automatically" setting can be turned off.

### Installation

- Download the installer matching your PC (x64 or ARM64) from https://selecto.span.observer or from GitHub (Releases), or use the Microsoft Store version (free).
- Portable version: unzip the archive and run `SelectoAI.exe` (see LISEZ-MOI.txt).
- Check the downloaded file's SHA-256 against the value published on the website.
