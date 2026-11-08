using SelectBar.UI;
using Xunit;

namespace SelectBar.Tests;

public class QuitPolicyTests
{
    [Fact]
    public void Demande_quand_le_reglage_est_actif_et_sans_exception()
        => Assert.True(QuitPolicy.ShouldAsk(confirmQuitSetting: true, sessionEnding: false, captureMode: false, externalCloseRequest: false));

    [Fact]
    public void Pas_de_demande_quand_le_reglage_est_coupe()
        => Assert.False(QuitPolicy.ShouldAsk(confirmQuitSetting: false, sessionEnding: false, captureMode: false, externalCloseRequest: false));

    [Fact]
    public void Pas_de_demande_quand_windows_s_arrete()
        => Assert.False(QuitPolicy.ShouldAsk(confirmQuitSetting: true, sessionEnding: true, captureMode: false, externalCloseRequest: false));

    [Fact]
    public void Pas_de_demande_en_mode_capture()
        => Assert.False(QuitPolicy.ShouldAsk(confirmQuitSetting: true, sessionEnding: false, captureMode: true, externalCloseRequest: false));

    [Fact]
    public void Pas_de_demande_pour_une_fermeture_externe()
        => Assert.False(QuitPolicy.ShouldAsk(confirmQuitSetting: true, sessionEnding: false, captureMode: false, externalCloseRequest: true));
}
