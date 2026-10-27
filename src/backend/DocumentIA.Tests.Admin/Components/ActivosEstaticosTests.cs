using System.Text.RegularExpressions;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Ficheros estaticos del Admin (revision final de AB#101124): ninguna imagen sin
// uso y ningun color fuera de SarebPaleta y sareb.css.
public class ActivosEstaticosTests
{
    private static readonly string AdminDir = SarebCssTests.AdminDir;

    [Fact]
    public void Imagenes_TodasSeUsanDesdeComponentesOEstilos()
    {
        var referencias = Directory.EnumerateFiles(Path.Combine(AdminDir, "Components"), "*.razor*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(AdminDir, "wwwroot"), "*.css", SearchOption.TopDirectoryOnly))
            .Select(File.ReadAllText)
            .ToList();

        var huerfanas = Directory.EnumerateFiles(Path.Combine(AdminDir, "wwwroot", "img"))
            .Select(Path.GetFileName)
            .Where(nombre => !referencias.Any(texto => texto.Contains(nombre!)))
            .ToList();

        huerfanas.Should().BeEmpty();
    }

    [Fact]
    public void ApexchartsInterop_NoFijaNingunColor()
    {
        var js = File.ReadAllText(Path.Combine(AdminDir, "wwwroot", "js", "apexcharts-interop.js"));

        Regex.Matches(js, @"#[0-9A-Fa-f]{3,8}\b").Select(m => m.Value).Should().BeEmpty(
            "los colores de los graficos salen de SarebPaleta, no del interop");
    }
}
