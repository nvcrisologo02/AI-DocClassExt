using DocumentIA.Functions.Abstractions;

namespace DocumentIA.Functions.Mocks;

/// <summary>
/// Proveedor de datos de prueba (mocks) para la extraccion de documentos.
/// Se utilizara temporalmente hasta que se implemente el servicio real de Azure AI Document Intelligence.
/// </summary>
public class MockExtraerDataProvider : IExtraerDataProvider
{
    public Dictionary<string, object> ObtenerDatos(string tipologia)
    {
        return tipologia switch
        {
            "nota.simple.1_2" => ObtenerDatosNotaSimple12(),
            "nota.simple.1_3" => ObtenerDatosNotaSimple13(),
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
            ["Titular"] = "Juan Perez Garcia",
            ["NIF"] = "12345678A",
            ["DerechoTitularidad"] = "Pleno dominio",
            ["CuotaParticipacion"] = "1/1",
            ["TituloAdquisicion"] = "Compraventa",
            ["FechaInscripcion"] = "20/10/2024",
            ["TomoLibroFolio"] = "Tomo 5520, Libro 10245, Folio 125",
            ["VPO"] = false,
            ["LimitacionesAdministrativas"] = "Ninguna limitacion registrada",
            ["Ocupacion"] = "Libre",
            ["Observaciones"] = "Nota simple extraida correctamente del Registro de la Propiedad",
            ["Anejos"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["tipo"] = "Trastero",
                    ["descripcion"] = "Trastero en sotano del edificio",
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
            ["FincaRegistral"] = "107609",
            ["RegistroPropiedad"] = null,
            ["MunicipioRegistro"] = null,
            ["IDUFIR_CRU"] = "18019000836709",
            ["FechaDocumento"] = "02/02/2018",
            ["NumeroAsientoPresentacion"] = "1048 del Diario 68 (12/05/2016)",
            ["Direccion"] = "Avenida del Conocimiento 3",
            ["ReferenciaCatastral"] = "6313502VG4161C0000RL",
            ["CodigoPostal"] = "18001",
            ["Provincia"] = "Granada",
            ["Municipio"] = "Granada",
            ["TipologiaInmueble"] = "Suelo",
            ["superficie"] = 2130.12m,
            ["UnidadSuperficie"] = "m2_construidos",
            ["Anejos"] = null,
            ["Linderos"] = "Norte, de Oeste a Este: Espacio Libre ELSG-7 del PP-S2 del PGOU 2001 de Granada y Avenida del Conocimiento; Este, de Norte a Sur: Avenida del Conocimiento y resto de la finca registral 95336 y de la catastral de referencia 6313501VG4161C0001LB; Sur, de Oeste a Este: calle Menendez Pelayo y resto de la finca registral 95336 y de la parcela catastral 6313501VG4161C0001LB; y Oeste, de Norte a Sur: Espacio libre ELSG-7 del PP-S2 del PGOU 2001 de Granada y calle Menendez Pelayo. Condiciones urbanisticas.- Clasificacion: Suelo Urbano. Uso: Equipamiento Terciario complementario en Ordenacion abierta. Edificabilidad: 4.730,45 m2 construidos de techo. Representacion grafica georreferenciada: Es la resultante del archivo GML adjunto a la certificacion catastral descriptiva y grafica con CSV: DH34HC42TJHR5PAR.",
            ["CalificacionUrbanistica"] = "Suelo urbano",
            ["Titular"] = "AYUNTAMIENTO DE GRANADA",
            ["NIF"] = "P1808900C",
            ["DerechoTitularidad"] = "Pleno dominio",
            ["CuotaParticipacion"] = null,
            ["FechaAdquisicion"] = null,
            ["TituloAdquisicion"] = null,
            ["FechaInscripcion"] = "09/06/2016",
            ["TomoLibroFolio"] = "Tomo 2006, Libro 2023, Folio 33",
            ["Cargas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["tipo"] = "Afeccion fiscal",
                    ["descripcion"] = "AFECCION FISCAL: Afeccion fiscal durante el plazo de CINCO ANOS, al pago de la liquidacion o liquidaciones complementarias que, en su caso puedan girarse por el Impuesto de Transmisiones Patrimoniales y Actos Juridicos Documentados, habiendose declarado exenta, segun nota al margen de la inscripcion la de fecha 9 de Junio de 2016.",
                    ["importeMaxResponsabilidad"] = null,
                    ["fechaInscripcion"] = null,
                    ["acreedor"] = null
                }
            },
            ["VPO"] = null,
            ["LimitacionesAdministrativas"] = null,
            ["AfeccionesFiscales"] = "AFECCION FISCAL: Afeccion fiscal durante el plazo de CINCO ANOS, al pago de la liquidacion o liquidaciones complementarias que, en su caso puedan girarse por el Impuesto de Transmisiones Patrimoniales",
            ["Ocupacion"] = null,
            ["ArrendamientosInscritos"] = null,
            ["HistorialInscripciones"] = null,
            ["Observaciones"] = null,
            ["NotificacionesJudiciales"] = null,
            ["ValoracionCatastral"] = null,
            ["DeudaIBI"] = null,
            ["DeudaComunidad"] = null
        };
    }

    private Dictionary<string, object> ObtenerDatosGenerico()
    {
        return new Dictionary<string, object>
        {
            ["FechaDocumento"] = "24/10/2025",
            ["Emisor"] = "Tasadora Ejemplo S.L.",
            ["ValorTasado"] = 350000.00,
            ["Direccion"] = "Calle Alcala 45, 28014 Madrid",
            ["ReferenciaCatastral"] = "1234567890ABCDEFGH"
        };
    }
}
