using System.Net;
using DocumentIA.Admin.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

/// <summary>
/// AB#100233: la traduccion interna de TipoModelo a segmento de ruta lanza para
/// cualquier valor que no contemple, y el servicio no captura esa excepcion. Sube
/// hasta la pagina, asi que un miembro nuevo del enum no rompe solo su seccion:
/// tumba entera la pagina de Modelos y la de Configuracion. Paso al anadir Tarifas
/// y ningun test lo cogio porque no habia ninguno que recorriera el enum.
/// </summary>
public class TipoModeloSegmentTests
{
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public string UserName => "test-user";
        public bool IsAuthenticated => true;
    }

    private static TipologiaAdminService CreateService(List<string> rutasPedidas)
    {
        var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            rutasPedidas.Add(request.RequestUri!.AbsolutePath);
            return StubHttpMessageHandler.Json("[]");
        }))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        return new TipologiaAdminService(client, new FakeCurrentUser());
    }

    [Fact]
    public async Task TodoValorDelEnumSePuedeConsultarSinLanzar()
    {
        foreach (var tipo in Enum.GetValues<TipoModelo>())
        {
            var rutas = new List<string>();
            var servicio = CreateService(rutas);

            var accion = async () => await servicio.GetModelosByTipoAsync(tipo);

            await accion.Should().NotThrowAsync(
                $"TipoModelo.{tipo} debe tener segmento de ruta; sin el, las paginas " +
                "de Modelos y Configuracion dejan de cargar enteras");
            rutas.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task CadaTipoPideSuPropiaRuta()
    {
        var esperados = new Dictionary<TipoModelo, string>
        {
            [TipoModelo.Clasificacion] = "/api/management/modelos/clasificacion",
            [TipoModelo.Extraccion] = "/api/management/modelos/extraccion",
            [TipoModelo.Prompt] = "/api/management/modelos/prompt",
            [TipoModelo.Layout] = "/api/management/modelos/layout",
            [TipoModelo.Tarifas] = "/api/management/modelos/tarifas"
        };

        foreach (var (tipo, rutaEsperada) in esperados)
        {
            var rutas = new List<string>();
            await CreateService(rutas).GetModelosByTipoAsync(tipo);

            rutas.Single().Should().Be(rutaEsperada);
        }
    }

    [Fact]
    public async Task GetModeloByIdAsync_EncuentraModelosDeCualquierTipo()
    {
        // La busqueda por Id recorria una lista fija de cuatro tipos. Con Tarifas
        // fuera, editar esa fila respondia "no existe un modelo con Id X" para una
        // fila que si existe (AB#100233). Se comprueba con el ultimo valor del enum,
        // que es el que una lista escrita a mano tiende a dejarse.
        var ultimoTipo = Enum.GetValues<TipoModelo>().Last();
        var segmentoDelUltimo = ultimoTipo.ToString().ToLowerInvariant();

        var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            // Solo la ruta del ultimo tipo devuelve el modelo; el resto van vacias.
            var esUltimo = request.RequestUri!.AbsolutePath.EndsWith($"/modelos/{segmentoDelUltimo}");
            return StubHttpMessageHandler.Json(esUltimo
                ? """[{"id":42,"tipo":4,"key":"tarifas.ia","provider":"catalogo","activo":true,"configuracionJson":"{}"}]"""
                : "[]");
        }))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        var servicio = new TipologiaAdminService(client, new FakeCurrentUser());

        var modelo = await servicio.GetModeloByIdAsync(42);

        modelo.Should().NotBeNull(
            $"un modelo de tipo {ultimoTipo} debe poder editarse como cualquier otro");
        modelo!.Key.Should().Be("tarifas.ia");
    }

    [Fact]
    public async Task NingunTipoCompartRutaConOtro()
    {
        var rutasPorTipo = new List<string>();
        foreach (var tipo in Enum.GetValues<TipoModelo>())
        {
            var rutas = new List<string>();
            await CreateService(rutas).GetModelosByTipoAsync(tipo);
            rutasPorTipo.Add(rutas.Single());
        }

        rutasPorTipo.Should().OnlyHaveUniqueItems(
            "dos tipos con la misma ruta harian que una seccion mostrase los modelos de otra");
    }
}
