using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Wizard;

public class TipologiaWizardStateServiceFieldsTests
{
    [Fact]
    public void AddField_AppendsEmptyFieldToDraft()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Fields.Should().BeEmpty();

        svc.AddField();

        svc.Draft.Fields.Should().HaveCount(1);
        svc.Draft.Fields[0].Name.Should().BeEmpty();
        svc.Draft.Fields[0].Type.Should().Be("string");
        svc.Draft.Fields[0].Required.Should().BeFalse();
    }

    [Fact]
    public void AddField_MultipleTimesPreservesAll()
    {
        var svc = new TipologiaWizardStateService();

        svc.AddField();
        svc.AddField();
        svc.AddField();

        svc.Draft.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void RemoveFieldAt_ValidIndex_RemovesField()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();
        svc.Draft.Fields[0].Name = "AEliminar";

        svc.RemoveFieldAt(0);

        svc.Draft.Fields.Should().BeEmpty();
    }

    [Fact]
    public void RemoveFieldAt_NegativeIndex_IsNoOp()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();

        svc.RemoveFieldAt(-1);

        svc.Draft.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveFieldAt_IndexOutOfRange_IsNoOp()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();

        svc.RemoveFieldAt(5);

        svc.Draft.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void AddRule_ToValidField_AppendsRule()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();

        svc.AddRule(0);

        svc.Draft.Fields[0].Rules.Should().HaveCount(1);
        svc.Draft.Fields[0].Rules[0].RuleType.Should().BeEmpty();
        svc.Draft.Fields[0].Rules[0].Severity.Should().Be("Error");
    }

    [Fact]
    public void AddRule_ToInvalidFieldIndex_IsNoOp()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();

        svc.AddRule(99);

        svc.Draft.Fields[0].Rules.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_ValidIndices_RemovesRule()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();
        svc.AddRule(0);
        svc.AddRule(0);

        svc.RemoveRule(0, 0);

        svc.Draft.Fields[0].Rules.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRule_InvalidFieldIndex_IsNoOp()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();
        svc.AddRule(0);

        svc.RemoveRule(99, 0);

        svc.Draft.Fields[0].Rules.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRule_InvalidRuleIndex_IsNoOp()
    {
        var svc = new TipologiaWizardStateService();
        svc.AddField();
        svc.AddRule(0);

        svc.RemoveRule(0, 99);

        svc.Draft.Fields[0].Rules.Should().HaveCount(1);
    }
}
