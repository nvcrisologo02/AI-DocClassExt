using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentIA.Functions.Triggers;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Un trigger con expresión "%Setting%" no se indexa si el app setting no existe en Azure:
/// appsettings.json no se carga en Functions. Cada setting así debe estar en el contrato de
/// settings y lo deben crear los pipelines que despliegan Functions (AB#100983).
/// </summary>
public class TriggerSettingsContractTests
{
    private static readonly Regex SettingExpression = new("^%(?<name>[^%]+)%$", RegexOptions.Compiled);

    public static TheoryData<string> SettingsDeTriggers()
    {
        var data = new TheoryData<string>();
        var nombres = typeof(BlobCleanupTimerTrigger).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(m => m.GetParameters())
            .Select(p => p.GetCustomAttribute<TimerTriggerAttribute>()?.Schedule)
            .Where(s => s is not null)
            .Select(s => SettingExpression.Match(s!))
            .Where(m => m.Success)
            .Select(m => m.Groups["name"].Value)
            .Distinct();
        foreach (var nombre in nombres)
        {
            data.Add(nombre);
        }

        return data;
    }

    [Fact]
    public void HayAlMenosUnTriggerConSettingDeEntorno()
    {
        SettingsDeTriggers().Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(SettingsDeTriggers))]
    public void ElContratoExigeElSettingDelTrigger(string setting)
    {
        using var contrato = JsonDocument.Parse(File.ReadAllText(RutaRepo("scripts", "config", "azure-appsettings-contract.json")));
        var functions = contrato.RootElement.GetProperty("apps").EnumerateArray()
            .Single(a => a.GetProperty("id").GetString() == "functions");
        var requeridos = functions.GetProperty("requiredSettings").EnumerateArray()
            .Select(s => s.GetProperty("name").GetString());

        requeridos.Should().Contain(setting);
    }

    [Theory]
    [MemberData(nameof(SettingsDeTriggers))]
    public void LosPipelinesDeFunctionsCreanElSettingDelTrigger(string setting)
    {
        foreach (var pipeline in new[] { "azure-pipelines-functions.yml", "azure-pipelines.yml" })
        {
            File.ReadAllText(RutaRepo(pipeline)).Should().Contain($"\"{setting}=", $"{pipeline} debe asegurar {setting}");
        }
    }

    private static string RutaRepo(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "azure-pipelines.yml")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("los tests se ejecutan dentro del repositorio");
        return Path.Combine(new[] { dir!.FullName }.Concat(partes).ToArray());
    }
}
