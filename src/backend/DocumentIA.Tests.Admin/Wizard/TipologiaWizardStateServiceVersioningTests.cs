using DocumentIA.Admin.Services;
using DocumentIA.Data.Entities;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Wizard;

public class TipologiaWizardStateServiceVersioningTests
{
    [Fact]
    public void SuggestVersionedCodigoAndVersion_WhenNoConflict_ReturnsFalse()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "nueva-tipologia";
        svc.Draft.Version = "1.0.0";

        var result = svc.SuggestVersionedCodigoAndVersion(Array.Empty<TipologiaEntity>());

        result.HasChanges.Should().BeFalse();
        result.Codigo.Should().Be("nueva-tipologia");
    }

    [Fact]
    public void SuggestVersionedCodigoAndVersion_WhenCodigoEmpty_ReturnsFalse()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = string.Empty;

        var result = svc.SuggestVersionedCodigoAndVersion(new[] { new TipologiaEntity { Codigo = "otros", Version = "1.0.0" } });

        result.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void SuggestVersionedCodigoAndVersion_WhenExactCodigoCollides_SuggestsSuffix()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "nota-simple";
        svc.Draft.Version = "1.0.0";

        var existing = new[]
        {
            new TipologiaEntity { Codigo = "nota-simple", Version = "1.0.0" }
        };

        var result = svc.SuggestVersionedCodigoAndVersion(existing);

        result.HasChanges.Should().BeTrue();
        result.Codigo.Should().Be("nota-simple-v2");
    }

    [Fact]
    public void SuggestVersionedCodigoAndVersion_WhenSuffixedCodigoExists_IncrementsMaxSuffix()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "nota-simple";
        svc.Draft.Version = "1.0.0";

        var existing = new[]
        {
            new TipologiaEntity { Codigo = "nota-simple", Version = "1.0.0" },
            new TipologiaEntity { Codigo = "nota-simple-v2", Version = "1.1.0" }
        };

        var result = svc.SuggestVersionedCodigoAndVersion(existing);

        result.HasChanges.Should().BeTrue();
        result.Codigo.Should().Be("nota-simple-v3");
    }

    [Fact]
    public void SuggestVersionedCodigoAndVersion_WhenVersionCollidesWithFamily_SuggestsNextVersion()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "nuevo-tipo";
        svc.Draft.Version = "1.0.0";

        var existing = new[]
        {
            new TipologiaEntity { Codigo = "nuevo-tipo-v2", Version = "1.5.0" }
        };

        var result = svc.SuggestVersionedCodigoAndVersion(existing);

        result.HasChanges.Should().BeTrue();
        result.Version.Should().Be("1.5.1");
    }

    [Fact]
    public void EnsureVersionedCodigoAndVersion_WhenChangesDetected_AppliesThemToDraft()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "nota-simple";
        svc.Draft.Version = "1.0.0";

        var existing = new[]
        {
            new TipologiaEntity { Codigo = "nota-simple", Version = "1.0.0" }
        };

        var changed = svc.EnsureVersionedCodigoAndVersion(existing);

        changed.Should().BeTrue();
        svc.Draft.Codigo.Should().Be("nota-simple-v2");
    }

    [Fact]
    public void EnsureVersionedCodigoAndVersion_WhenNoChanges_ReturnsFalseAndKeepsDraft()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "unica-tipologia";
        svc.Draft.Version = "1.0.0";

        var changed = svc.EnsureVersionedCodigoAndVersion(Array.Empty<TipologiaEntity>());

        changed.Should().BeFalse();
        svc.Draft.Codigo.Should().Be("unica-tipologia");
    }
}
