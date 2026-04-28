using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Wizard;

public class TipologiaWizardStateServiceTemplateTests
{
    [Fact]
    public void ApplyTemplate_NotaSimple_FillsGdcFieldsWhenEmpty()
    {
        var svc = new TipologiaWizardStateService();

        svc.ApplyTemplate("notasimple");

        svc.Draft.TemplateCode.Should().Be("notasimple");
        svc.Draft.GdcTipoDocumento.Should().Be("NOTS");
        svc.Draft.GdcSerie.Should().Be("AI09");
        svc.Draft.SkipGdcUpload.Should().BeFalse();
        svc.Draft.EnableExtraction.Should().BeTrue();
        svc.Draft.ModeloExtraccionDI.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyTemplate_NotaSimple_DoesNotOverwriteExistingNombre()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Nombre = "Mi Nombre Personalizado";

        svc.ApplyTemplate("notasimple");

        svc.Draft.Nombre.Should().Be("Mi Nombre Personalizado");
    }

    [Fact]
    public void ApplyTemplate_NotaSimple_DoesNotOverwriteExistingGdcTipoDocumento()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.GdcTipoDocumento = "CUSTOM";

        svc.ApplyTemplate("notasimple");

        svc.Draft.GdcTipoDocumento.Should().Be("CUSTOM");
    }

    [Fact]
    public void ApplyTemplate_Tasacion_FillsAppropriateDefaults()
    {
        var svc = new TipologiaWizardStateService();

        svc.ApplyTemplate("tasacion");

        svc.Draft.TemplateCode.Should().Be("tasacion");
        svc.Draft.GdcTipoDocumento.Should().Be("TASA");
        svc.Draft.GdcSerie.Should().Be("AI05");
        svc.Draft.SkipGdcUpload.Should().BeFalse();
        svc.Draft.EnableExtraction.Should().BeTrue();
    }

    [Fact]
    public void ApplyTemplate_Generica_SetsSkipGdcTrue()
    {
        var svc = new TipologiaWizardStateService();

        svc.ApplyTemplate("generica");

        svc.Draft.TemplateCode.Should().Be("generica");
        svc.Draft.SkipGdcUpload.Should().BeTrue();
        svc.Draft.EnableExtraction.Should().BeFalse();
    }

    [Fact]
    public void ApplyTemplate_UnknownTemplate_OnlySetsTemplateCode()
    {
        var svc = new TipologiaWizardStateService();

        svc.ApplyTemplate("template-inexistente");

        svc.Draft.TemplateCode.Should().Be("template-inexistente");
        // No GDC or model fields set
        svc.Draft.GdcTipoDocumento.Should().BeEmpty();
    }
}
