# Fiche Microsoft Store : français (fr-FR)

Texte à coller dans le Partner Center (Fiche Store > fr-FR). Chaque affirmation est tirée du comportement documenté de l'application (README racine) ; ne rien ajouter qui ne soit pas vérifiable.

## Titre du produit
Selecto AI

## Courte description (100 caractères au plus)
Une barre d'actions qui apparaît quand tu sélectionnes du texte, dans n'importe quelle application.

## Description longue
Sélectionne du texte dans n'importe quelle application Windows : une petite barre flottante apparaît juste au-dessus et te propose l'action utile, sans changer de fenêtre.

Ce que fait la barre
• Rechercher la sélection dans le moteur de ton choix.
• Ouvrir un lien, afficher un fichier ou un dossier dans l'Explorateur, écrire à une adresse e-mail : l'action s'adapte à ce que tu sélectionnes.
• Couper, copier et coller, quand le champ est modifiable.
• Dictionnaire, traduction et assistant IA (résumer, expliquer, reformuler, corriger) : la réponse s'affiche dans un petit popup près de la sélection, avec « Remplacer » et « Copier ».
• Tes propres extensions : de simples fichiers JavaScript.

L'IA, à ta façon
• Actions en ligne (traduction, dictionnaire, IA) prêtes dès l'installation, et désactivables à tout moment dans Réglages > IA. Le texte n'est envoyé qu'au moment où tu lances une action.
• IA locale facultative : Selecto AI peut installer le moteur Ollama et un modèle sur ton PC, en un clic et sans droits administrateur ; le moteur répond sur une adresse locale de ton ordinateur.
• Ou ton propre fournisseur : tu saisis ta clé API (OpenAI, Anthropic, Google, Mistral et d'autres) ; elle est chiffrée avec Windows (DPAPI) sur ton PC.

Fait pour Windows 11
• Interface Fluent : thème clair ou sombre et couleur d'accent suivent ceux de Windows.
• Se règle en quelques clics : raccourci clavier global, applications et sites où la barre ne doit pas apparaître, taille et position de la barre.
• Fonctionne sur Windows 10 et 11, processeurs Intel/AMD (x64) et ARM64.

Gratuit et open source
Selecto AI est gratuit, sans limite de durée et sans compte. Son code est public sous licence MIT : https://github.com/warezfr/selecto-ai

Bon à savoir
• Selecto AI lit le texte que tu sélectionnes pour proposer des actions. Pour cela il utilise l'accessibilité de Windows (UI Automation) et, en repli, une copie temporaire dans le presse-papiers qu'il restaure ensuite.
• Quand tu utilises la traduction, le dictionnaire ou l'IA en ligne, le texte sélectionné est envoyé au service que tu as choisi. Détails : page Confidentialité du site.
• Un souci ? « Signaler un problème » (menu de l'icône ou page À propos) t'envoie directement à l'éditeur, avec une capture si tu le souhaites ; les journaux ne sont joints que si tu coches la case, après les avoir vus.
• L'application surveille la souris et le clavier pour détecter une sélection (crochet global Windows) : c'est la raison de la capacité « runFullTrust » déclarée par l'application.

## Nouveautés de cette version
Version 4.0.0 : Selecto AI est maintenant gratuit et libre (licence MIT). Plus d'essai ni de clé : toutes les fonctions sont disponibles. Le code est sur GitHub : https://github.com/warezfr/selecto-ai

## Mots-clés (7 au plus)
sélection de texte
barre d'actions
traduction
dictionnaire
assistant IA
productivité
raccourci

## Notes pour la certification (champ réservé au testeur, anglais accepté)
Selecto AI uses a global low-level mouse/keyboard hook (runFullTrust) to detect text selection in other applications and show a floating action bar. No account is required. The app is free and open source (MIT); all features are enabled. To test: select text in any application (e.g. Notepad); the bar appears above the selection. Online actions (translation, dictionary, AI) are on by default and can be turned off in Settings > IA. Selected text is sent to the chosen services (MyMemory, Wiktionary, Wikipedia, or your AI model) only when you trigger an action.
