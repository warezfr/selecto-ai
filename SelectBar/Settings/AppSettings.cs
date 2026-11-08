using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SelectBar.Settings;

public sealed class AppSettings
{
    /// <summary>Version du schéma des réglages. 0 si absent du fichier (v2 ou plus ancien) ; voir <see cref="SettingsMigration"/>.</summary>
    public int SettingsSchema { get; set; }

    /// <summary>Schéma 3 (v3) : les actions en ligne sont activées par défaut ; les fichiers plus anciens sont migrés une fois.</summary>
    /// <remarks>Schéma 4 (v3.1) : langue de l'interface (<see cref="UiLanguage"/>), sans conversion des autres réglages.</remarks>
    /// <remarks>Schéma 5 (v3.2.1) : raccourci Ctrl+Maj+Espace posé par défaut pour les réglages qui n'en avaient pas (voir <see cref="SettingsMigration"/>).</remarks>
    public const int CurrentSchema = 5;

    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public string SearchEngineId { get; set; } = "google";
    public string? CustomSearchUrl { get; set; }
    public List<string> DisabledActions { get; set; } = new();
    public List<string> ActionOrder { get; set; } = new();
    public bool HotkeyEnabled { get; set; }
    /// <summary>Raccourci global : affiche la barre sur la sélection en cours (utile après une sélection au clavier).</summary>
    public string Hotkey { get; set; } = HotkeyDefaults.Shortcut;

    // --- v3.1 ---
    /// <summary>Activation des extensions (identifiant -> actif). Absent : valeur par défaut de l'extension (voir ExtensionCatalog).</summary>
    public Dictionary<string, bool> ExtensionEnabled { get; set; } = new();

    /// <summary>Langue de l'interface : "auto" (langue Windows si prise en charge, sinon anglais), "fr", "en", "es" ou "ar".</summary>
    public string UiLanguage { get; set; } = "auto";

    /// <summary>La fenêtre « Bienvenue » du premier lancement a été terminée ou passée : elle ne revient plus.</summary>
    public bool FirstRunCompleted { get; set; }

    // --- v2 ---
    public bool ShowTrayIcon { get; set; } = true;
    /// <summary>Demander confirmation avant de quitter (case « Ne plus demander » de la boîte de sortie).</summary>
    public bool ConfirmQuit { get; set; } = true;
    /// <summary>Taille de la barre en % (80 à 140).</summary>
    public int BarScalePercent { get; set; } = Appearance.DefaultScale;
    /// <summary>"auto", "light" ou "dark".</summary>
    public string BarStyle { get; set; } = Appearance.Auto;
    /// <summary>"above" ou "below" le texte.</summary>
    public string BarPosition { get; set; } = Appearance.Above;
    /// <summary>Langue du correcteur orthographique (ex. fr-FR).</summary>
    public string SpellLanguage { get; set; } = "fr-FR";
    /// <summary>Action Copilot (la sélection part vers un service externe) : désactivée par défaut.</summary>
    public bool CopilotEnabled { get; set; }
    /// <summary>Ouvrir l'application Copilot (ms-copilot:) si elle est installée, sinon le web.</summary>
    public bool CopilotUseApp { get; set; } = true;

    // --- actions en ligne (popup de résultat) ---
    /// <summary>Dictionnaire, Traduire et IA envoient le texte sélectionné à des services tiers : activé par défaut (v3), désactivable.</summary>
    public bool OnlineActionsEnabled { get; set; } = true;
    /// <summary>Session seulement (jamais enregistré) : la migration vers le schéma 3 vient d'activer les actions en ligne ; l'annonce est à afficher une fois.</summary>
    [JsonIgnore]
    public bool OnlineActionsJustEnabled { get; set; }
    /// <summary>Langue cible de la traduction (code à 2 lettres).</summary>
    public string TargetLanguage { get; set; } = "fr";
    /// <summary>Langue utilisée quand le texte est déjà dans la langue cible.</summary>
    public string FallbackLanguage { get; set; } = "en";
    /// <summary>"auto", "fr" ou "en".</summary>
    public string DictionaryLanguage { get; set; } = "auto";
    /// <summary>E-mail facultatif transmis à MyMemory pour relever la limite quotidienne (donnée personnelle : vide par défaut).</summary>
    public string? MyMemoryEmail { get; set; }

    // --- modèle IA ---
    /// <summary>"none", "ollama", un fournisseur en ligne (openai, anthropic, gemini, mistral, groq, deepseek, xai, together, openrouter) ou "custom".</summary>
    public string LlmProvider { get; set; } = "none";
    public string LlmBaseUrl { get; set; } = "http://localhost:11434/v1";
    public string LlmModel { get; set; } = "";
    /// <summary>Clé API chiffrée (DPAPI, base64) du fournisseur actif, jamais en clair.</summary>
    public string? LlmApiKeyProtected { get; set; }
    /// <summary>Une clé chiffrée (DPAPI, base64) par fournisseur en ligne : fournisseur -> clé.</summary>
    public Dictionary<string, string> LlmApiKeys { get; set; } = new();
    /// <summary>Charge le moteur et le modèle IA locaux en arrière-plan au démarrage (si IA locale choisie) : première réponse plus rapide.</summary>
    public bool PrepareAiAtStartup { get; set; } = true;

    // --- mises à jour (édition téléchargée sur le site ; V3-07) ---
    /// <summary>Vérification automatique au démarrage puis toutes les 24 h. Sans effet en édition Store ou en mode capture.</summary>
    public bool AutoCheckUpdates { get; set; } = true;
    /// <summary>Dernière tentative de vérification (UTC) : évite de relancer le contrôle avant 24 h.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }
    /// <summary>Version pour laquelle « Plus tard » a été choisi (ne réaffiche pas la notification avant 24 h).</summary>
    public string? DismissedUpdateVersion { get; set; }
    /// <summary>Moment (UTC) du choix « Plus tard ».</summary>
    public DateTime? DismissedUpdateAtUtc { get; set; }
}
