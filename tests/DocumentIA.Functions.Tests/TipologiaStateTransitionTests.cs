using Xunit;
using FluentAssertions;
using DocumentIA.Data.Entities;

namespace DocumentIA.Functions.Tests;

/// <summary>
/// Tests for TipologiaEntity state transitions and defaults.
/// Validates Draft → Published → Retired workflow.
/// </summary>
public class TipologiaStateTransitionTests
{
    [Fact]
    public void CreateDraft_HasDraftState()
    {
        // Arrange & Act
        var tipologia = TestFixtures.CreateDraftTipologia();

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Draft);
    }

    [Fact]
    public void CreatePublished_HasPublishedState()
    {
        // Arrange & Act
        var tipologia = TestFixtures.CreateValidTipologia();

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Published);
    }

    [Fact]
    public void CreateRetired_HasRetiredState()
    {
        // Arrange & Act
        var tipologia = TestFixtures.CreateRetiredTipologia();

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Retired);
    }

    [Fact]
    public void Transition_Draft_To_Published_Succeeds()
    {
        // Arrange
        var tipologia = TestFixtures.CreateDraftTipologia();

        // Act
        tipologia.Estado = EstadoTipologia.Published;
        tipologia.PublicadaEn = DateTime.UtcNow;

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Published);
        tipologia.PublicadaEn.Should().NotBeNull();
    }

    [Fact]
    public void Transition_Published_To_Retired_Succeeds()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        tipologia.Estado = EstadoTipologia.Retired;

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Retired);
    }

    [Fact]
    public void DefaultUmbral_ClasificacionIs085()
    {
        // Arrange & Act
        var tipologia = new TipologiaEntity { Codigo = "TST", Nombre = "Test" };

        // Assert
        tipologia.UmbralClasificacion.Should().Be(0.85);
    }

    [Fact]
    public void DefaultUmbral_ExtraccionIs080()
    {
        // Arrange & Act
        var tipologia = new TipologiaEntity { Codigo = "TST", Nombre = "Test" };

        // Assert
        tipologia.UmbralExtraccion.Should().Be(0.80);
    }

    [Fact]
    public void DefaultVersion_Is10()
    {
        // Arrange & Act
        var tipologia = new TipologiaEntity { Codigo = "TST", Nombre = "Test" };

        // Assert
        tipologia.Version.Should().Be("1.0");
    }

    [Fact]
    public void DefaultActiva_IsTrue()
    {
        // Arrange & Act
        var tipologia = new TipologiaEntity { Codigo = "TST", Nombre = "Test" };

        // Assert
        tipologia.Activa.Should().BeTrue();
    }

    [Fact]
    public void DefaultEstado_IsDraft()
    {
        // Arrange & Act
        var tipologia = new TipologiaEntity { Codigo = "TST", Nombre = "Test" };

        // Assert
        tipologia.Estado.Should().Be(EstadoTipologia.Draft);
    }
}
