using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Wizard;

public class TipologiaWizardStateServiceNavigationTests
{
    [Fact]
    public void NextStep_WhenAtFirstStep_IncrementsStep()
    {
        var svc = new TipologiaWizardStateService();
        svc.CurrentStep.Should().Be(1);

        svc.NextStep();

        svc.CurrentStep.Should().Be(2);
    }

    [Fact]
    public void NextStep_WhenAtLastStep_DoesNotExceedTotalSteps()
    {
        var svc = new TipologiaWizardStateService();
        for (var i = 0; i < 10; i++) svc.NextStep();

        svc.CurrentStep.Should().Be(svc.TotalSteps);
    }

    [Fact]
    public void PreviousStep_WhenAtFirstStep_StaysAtOne()
    {
        var svc = new TipologiaWizardStateService();

        svc.PreviousStep();

        svc.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void PreviousStep_WhenAtStepThree_GoesToStepTwo()
    {
        var svc = new TipologiaWizardStateService();
        svc.NextStep();
        svc.NextStep();
        svc.CurrentStep.Should().Be(3);

        svc.PreviousStep();

        svc.CurrentStep.Should().Be(2);
    }

    [Fact]
    public void GoToStep_ClampsToValidRange()
    {
        var svc = new TipologiaWizardStateService();

        svc.GoToStep(0);
        svc.CurrentStep.Should().Be(1);

        svc.GoToStep(100);
        svc.CurrentStep.Should().Be(svc.TotalSteps);

        svc.GoToStep(3);
        svc.CurrentStep.Should().Be(3);
    }

    [Fact]
    public void Reset_RestoresDefaultState()
    {
        var svc = new TipologiaWizardStateService();
        svc.NextStep();
        svc.NextStep();
        svc.Draft.Codigo = "test";
        svc.Draft.Nombre = "Test";

        svc.Reset();

        svc.CurrentStep.Should().Be(1);
        svc.Draft.Codigo.Should().BeEmpty();
        svc.Draft.Nombre.Should().BeEmpty();
        svc.Draft.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void TotalSteps_IsAlwaysFive()
    {
        var svc = new TipologiaWizardStateService();
        svc.TotalSteps.Should().Be(5);
    }
}
