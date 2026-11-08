using System;
using SelectBar.Localization;
using Xunit;

namespace SelectBar.Tests;

/// <summary>
/// Fixture partagée : force la culture de l'interface au français (référence) pour les tests qui comparent des libellés.
/// Les classes qui lisent <see cref="L"/> portent <c>[Collection("Localization")]</c> : elles s'exécutent en série,
/// car <see cref="L.Culture"/> est un état statique.
/// </summary>
public sealed class CultureFixture : IDisposable
{
    public CultureFixture() => L.Culture = UiCulture.Make("fr");

    public void Dispose() => L.Culture = UiCulture.Make("fr");
}

[CollectionDefinition("Localization")]
public class LocalizationCollection : ICollectionFixture<CultureFixture>
{
}
