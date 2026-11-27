using System.Text.RegularExpressions;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Todos los despliegues de la Function App usan el mismo método, <c>zipDeploy</c> (AB#101582).
/// Alternar métodos en un entorno rompe la configuración: el 802 desplegaba con run-from-package
/// (<c>WEBSITE_RUN_FROM_PACKAGE=1</c>) y dejaba congelado el <c>wwwroot</c> del file share; el
/// siguiente 799 (<c>zipDeploy</c>) quitaba el setting, la app arrancaba unos segundos sobre ese
/// <c>wwwroot</c> antiguo antes de extraer el zip y el seed de aquel código rellenaba los
/// <c>Endpoint</c> vacíos de <c>ModeloConfigs</c> con los de PRO (DEV, run 80288, 2026-11-27).
/// </summary>
public class FunctionsDeploymentMethodContractTests
{
    private static readonly Regex TareaFunctionApp = new(
        @"^(?<sangria>\s*)-\s*task:\s*AzureFunctionApp@\d+\s*$", RegexOptions.Compiled);

    private static readonly Regex MetodoZipDeploy = new(
        @"^\s*deploymentMethod:\s*['""]?zipDeploy['""]?\s*$", RegexOptions.Compiled);

    public static TheoryData<string> YamlConDespliegueDeFunctions()
    {
        var raiz = RaizRepo();
        var candidatos = Directory.GetFiles(raiz, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(raiz, "pipelines"), "*.yml", SearchOption.AllDirectories));

        var data = new TheoryData<string>();
        foreach (var fichero in candidatos.Where(f => File.ReadAllLines(f).Any(l => TareaFunctionApp.IsMatch(l))))
        {
            data.Add(Path.GetRelativePath(raiz, fichero).Replace('\\', '/'));
        }

        return data;
    }

    [Fact]
    public void HayYamlQueDespliegaLaFunctionApp()
    {
        YamlConDespliegueDeFunctions().Should().Contain(
            new[] { "azure-pipelines-functions.yml", "pipelines/templates/steps/deploy-functions.yml" });
    }

    [Theory]
    [MemberData(nameof(YamlConDespliegueDeFunctions))]
    public void CadaDespliegueDeFunctionsDeclaraZipDeploy(string rutaRelativa)
    {
        var lineas = File.ReadAllLines(Path.Combine(RaizRepo(), rutaRelativa));

        for (var i = 0; i < lineas.Length; i++)
        {
            var tarea = TareaFunctionApp.Match(lineas[i]);
            if (!tarea.Success)
            {
                continue;
            }

            var sangria = tarea.Groups["sangria"].Value.Length;
            var bloque = lineas.Skip(i + 1)
                .TakeWhile(l => string.IsNullOrWhiteSpace(l) || l.Length - l.TrimStart().Length > sangria);

            bloque.Should().Contain(
                l => MetodoZipDeploy.IsMatch(l),
                $"la tarea de la línea {i + 1} de {rutaRelativa} debe fijar deploymentMethod: zipDeploy, igual que el resto de despliegues de la Function App");
        }
    }

    private static string RaizRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "azure-pipelines.yml")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("los tests se ejecutan dentro del repositorio");
        return dir!.FullName;
    }
}
