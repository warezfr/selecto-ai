#if EDITION_STORE
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace SelectBar.Settings;

/// <summary>
/// Démarrage avec Windows de l'édition Store : la valeur HKCU\...\Run est virtualisée dans un MSIX et ne fonctionne pas ; on utilise la
/// tâche de démarrage <c>desktop:StartupTask</c> du manifeste (TaskId « SelectoStartup »). Hors paquet, les appels échouent et sont journalisés.
/// </summary>
internal static class StoreStartup
{
    public const string TaskId = "SelectoStartup";

    public static void Set(bool enabled) =>
        Task.Run(async () =>
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                var state = await task.RequestEnableAsync();
                if (state is not (StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy))
                    Log.Write("Démarrage auto (Store) : " + state + " (désactivé par l'utilisateur ou par une stratégie : Gestionnaire des tâches > Applications au démarrage).");
            }
            else if (task.State is StartupTaskState.Enabled) task.Disable();
        }).GetAwaiter().GetResult();

    public static bool IsEnabled() =>
        Task.Run(async () =>
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }).GetAwaiter().GetResult();
}
#endif
