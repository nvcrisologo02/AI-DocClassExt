using DocumentIA.Functions.Abstractions;

namespace DocumentIA.Functions.Mocks;

/// <summary>
/// Proveedor de datos de prueba (mocks) para la extracción de documentos.
/// Se utilizará temporalmente hasta que se implemente el servicio real de Azure AI Document Intelligence.
/// </summary>
public class MockExtraerDataProvider : IExtraerDataProvider
{
    public Dictionary<string, object> ObtenerDatos(string tipologia)
    {
        return tipologia switch
        {
            "notasimple1.2" => ObtenerDatosNotaSimple12(),
            "notasimple1.3" => ObtenerDatosNotaSimple13(),
            _ => ObtenerDatosGenerico()
        };
    }

    private Dictionary<string, object> ObtenerDatosNotaSimple12()
    {
        return new Dictionary<string, object>
        {
            ["FincaRegistral"] = "45689",
            ["RegistroPropiedad"] = "Registro de la Propiedad No. 15 de Madrid",
            ["MunicipioRegistro"] = "Madrid",
            ["IDUFIR_CRU"] = "28001A001001234",
            ["FechaDocumento"] = "15/11/2024",
            ["NumeroAsientoPresentacion"] = "ASI-2024-00156",
            ["Direccion"] = "Calle Mayor 123, planta 4, 28001 Madrid",
            ["ReferenciaCatastral"] = "2800701UJ2001200UN",
            ["TipologiaInmueble"] = "Vivienda",
            ["superficie"] = 120.50m,
            ["UnidadSuperficie"] = "m2_construidos",
            ["Titular"] = "Juan Pérez García",
            ["NIF"] = "12345678A",
            ["DerechoTitularidad"] = "Pleno dominio",
            ["CuotaParticipacion"] = "1/1",
            ["TituloAdquisicion"] = "Compraventa",
            ["FechaInscripcion"] = "20/10/2024",
            ["TomoLibroFolio"] = "Tomo 5520, Libro 10245, Folio 125",
            ["VPO"] = false,
            ["LimitacionesAdministrativas"] = "Ninguna limitación registrada",
            ["Ocupacion"] = "Libre",
            ["Observaciones"] = "Nota simple extraída correctamente del Registro de la Propiedad",
            ["Anejos"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["tipo"] = "Trastero",
                    ["descripcion"] = "Trastero en sótano del edificio",
                    ["superficie"] = 8.5m
                }
            },
            ["Cargas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["tipo"] = "Hipoteca",
                    ["descripcion"] = "Hipoteca en favor de Banco Popular",
                    ["importeMaxResponsabilidad"] = 150000m,
                    ["fechaInscripcion"] = "20/10/2024"
                }
            }
        };
    }

    private Dictionary<string, object> ObtenerDatosNotaSimple13()
    {
        return new Dictionary<string, object>
        {
            ["FincaRegistral"] = "78901",
            ["RegistroPropiedad"] = "Registro de la Propiedad No. 7 de Barcelona",
            ["MunicipioRegistro"] = "Barcelona",
            ["IDUFIR_CRU"] = "08019A002002567",
            ["FechaDocumento"] = "22/01/2025",
            ["NumeroAsientoPresentacion"] = "ASI-2025-00089",
            ["Direccion"] = "Paseo de Gracia 456, planta 8, puerta A, 08008 Barcelona",
            ["ReferenciaCatastral"] = "0801904DJ3045300MT",
            ["TipologiaInmueble"] = "Local",
            ["superficie"] = 85.75m,
            ["UnidadSuperficie"] = "m2_utiles",
            ["Titular"] = "María González López y Carlos Redondo Fernández",
            ["NIF"] = "87654321B",
            ["DerechoTitularidad"] = "Copropiedad",
            ["CuotaParticipacion"] = "1/2",
            ["TituloAdquisicion"] = "Herencia",
            ["FechaInscripcion"] = "10/01/2025",
            ["TomoLibroFolio"] = "Tomo 3340, Libro 8765, Folio 234",
            ["VPO"] = false,
            ["LimitacionesAdministrativas"] = "Restricción para actividades comerciales",
            ["Ocupacion"] = "Arrendado inscrito",
            ["Observaciones"] = "Nota simple v1.3 con datos de copropiedad en actividad comercial",
            ["Anejos"] = new object[]{
                new Dictionary<string, object>
                {
                    ["tipo"] = "Parking",
                    ["descripcion"] = "Dos plazas de parking en sótano -2",
                    ["superficie"] = 25.0m
                },
                new Dictionary<string, object>
                {
                    ["tipo"] = "Almacén",
                    ["descripcion"] = "Almacén anexo al local",
                    ["superficie"] = 12.3m
                }
            },
            ["Cargas"] = new object[]{
                new Dictionary<string, object>
                {
                    ["tipo"] = "Hipoteca",
                    ["descripcion"] = "Hipoteca en favor de CaixaBank",
                    ["importeMaxResponsabilidad"] = 75000m,
                    ["fechaInscripcion"] = "10/01/2025"
                },
                new Dictionary<string, object>
                {
                    ["tipo"] = "Embargo",
                    ["descripcion"] = "Embargo preventivo por sentencia judicial",
                    ["importeMaxResponsabilidad"] = 5000m,
                    ["fechaInscripcion"] = "15/02/2025"
                }
            }
        };
    }

    private Dictionary<string, object> ObtenerDatosGenerico()
    {
        return new Dictionary<string, object>
        {
            ["FechaDocumento"] = "24/10/2025",
            ["Emisor"] = "Tasadora Ejemplo S.L.",
            ["ValorTasado"] = 350000.00,
            ["Direccion"] = "Calle Alcalá 45, 28014 Madrid",
            ["ReferenciaCatastral"] = "1234567890ABCDEFGH"
        };
    }
}
