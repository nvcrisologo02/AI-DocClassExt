using System.Text.RegularExpressions;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Reglas de sareb.css que Bootstrap 5.1 no deja ver en bUnit: colores fijos en
// los estados de los botones y la paginacion, texto blanco sobre los colores de
// la paleta y botones dentro de una cabecera verde (revision final de AB#101124).
public class SarebCssTests
{
    private static readonly string AdminDir = BuscarAdmin();

    private static string BuscarAdmin()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "src", "frontend", "DocumentIA.Admin");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encuentra src/frontend/DocumentIA.Admin");
    }

    // Selector normalizado -> declaraciones de todas las reglas que lo nombran.
    private static readonly Dictionary<string, string> Reglas = LeerReglas();

    private static Dictionary<string, string> LeerReglas()
    {
        var css = File.ReadAllText(Path.Combine(AdminDir, "wwwroot", "sareb.css"));
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var reglas = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            foreach (var selector in m.Groups[1].Value.Split(','))
            {
                var clave = Normalizar(selector);
                reglas[clave] = reglas.GetValueOrDefault(clave, "") + m.Groups[2].Value;
            }
        }

        return reglas;
    }

    private static string Normalizar(string selector) =>
        Regex.Replace(Regex.Replace(selector.Trim(), @"\s*([>+~])\s*", "$1"), @"\s+", " ");

    private static string Declaraciones(string selector) =>
        Reglas.TryGetValue(Normalizar(selector), out var d) ? Regex.Replace(d, @"\s+", " ") : "";

    [Theory]
    [InlineData(".badge.bg-secondary")]
    [InlineData(".badge.bg-success")]
    [InlineData(".badge.bg-warning")]
    [InlineData(".badge.bg-danger")]
    [InlineData(".badge.bg-info")]
    [InlineData(".btn-success")]
    [InlineData(".btn-danger")]
    public void TextoNegroSobreLosColoresDeEstado(string selector)
    {
        Declaraciones(selector).Should().Contain("color: #000");
    }

    [Theory]
    [InlineData(".btn-primary:disabled")]
    [InlineData(".btn-primary.disabled")]
    [InlineData(".btn-check:checked+.btn-primary")]
    [InlineData(".btn-success:hover")]
    [InlineData(".btn-success:disabled")]
    [InlineData(".btn-danger:hover")]
    [InlineData(".btn-danger:disabled")]
    [InlineData(".btn-warning:hover")]
    [InlineData(".btn-outline-primary.active")]
    [InlineData(".btn-outline-primary:active")]
    [InlineData(".btn-check:checked+.btn-outline-primary")]
    public void EstadosDeBotonConColorDeLaPaleta(string selector)
    {
        Declaraciones(selector).Should().Contain("background-color: var(--sareb-");
    }

    [Theory]
    [InlineData(".btn-outline-primary:disabled")]
    [InlineData(".page-link:hover")]
    [InlineData(".page-link:focus")]
    public void EstadosDeTextoConColorDeLaPaleta(string selector)
    {
        Declaraciones(selector).Should().Contain("color: var(--sareb-");
    }

    [Theory]
    [InlineData(".btn-check:focus+.btn")]
    [InlineData(".page-link:focus")]
    public void FocoConSombraVerde(string selector)
    {
        Declaraciones(selector).Should().Contain("rgba(var(--sareb-verde-rgb)");
    }

    [Theory]
    [InlineData(".card:has(>.table-responsive>.table)>.card-header .btn-outline-primary")]
    [InlineData(".card:has(>.table)>.card-header .btn-outline-primary")]
    [InlineData(".card:has(>.card-body .table)>.card-header .btn-outline-primary")]
    [InlineData(".card:has(>.card-body .table)>.card-header a:not(.btn)")]
    [InlineData(".card:has(>.table)>.card-header .text-muted")]
    [InlineData(".card:has(>.table-responsive>.table)>.card-header .text-muted")]
    public void ContenidoDeLaCabeceraVerdeEnBlanco(string selector)
    {
        Declaraciones(selector).Should().MatchRegex("color: (#fff|rgba\\(255, 255, 255)");
    }

    [Fact]
    public void NingunaPaginaPoneTextoBlancoSobreUnColorDeEstado()
    {
        var patron = new Regex(@"class=""[^""]*\bbg-(success|info|secondary|warning|danger)\b[^""]*\btext-white\b|class=""[^""]*\btext-white\b[^""]*\bbg-(success|info|secondary|warning|danger)\b");

        var culpables = Directory.EnumerateFiles(Path.Combine(AdminDir, "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (f, i, l)))
            .Where(x => patron.IsMatch(x.l))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToList();

        culpables.Should().BeEmpty();
    }
}
