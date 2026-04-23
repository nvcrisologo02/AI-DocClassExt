using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.AssetResolver.Data.Entities;

/// <summary>
/// Entidad mapeada a la tabla DM_POSICION_AACC_TB de la base de datos ODS (dev) / ODS_DHW (pro).
/// Solo lectura; la tabla es propiedad de otro sistema.
/// </summary>
[Table("DM_POSICION_AACC_TB")]
public class DmPosicionAACC
{
    [Column("ID_ACTIVO_SAREB", TypeName = "decimal(16,0)")]
    public decimal IdActivoSareb { get; set; }

    [Column("FCH_CIERRE_DT")]
    public DateTime FchCierreDt { get; set; }

    [Column("FCH_CIERRE")]
    public DateTime? FchCierre { get; set; }

    [Column("FCH_ALTA")]
    public DateTime? FchAlta { get; set; }

    [Column("FCH_BAJA")]
    public DateTime? FchBaja { get; set; }

    [Column("DES_SERVICER"), MaxLength(255)]
    public string? DesServicer { get; set; }

    [Column("ID_IDUFIR"), MaxLength(14)]
    public string? IdIdufir { get; set; }

    [Column("ID_REF_CATAST"), MaxLength(32)]
    public string? IdRefCatast { get; set; }

    [Column("DES_NOMBRE_VIA"), MaxLength(255)]
    public string? DesNombreVia { get; set; }

    [Column("NUM_VIA"), MaxLength(20)]
    public string? NumVia { get; set; }

    [Column("DES_MUNICP"), MaxLength(255)]
    public string? DesMunicp { get; set; }

    [Column("NUM_COD_POSTAL"), MaxLength(20)]
    public string? NumCodPostal { get; set; }

    [Column("DES_PAIS"), MaxLength(255)]
    public string? DesPais { get; set; }

    [Column("DES_PROVNC"), MaxLength(255)]
    public string? DesProvnc { get; set; }

    [Column("DES_COMUNI_AUTO"), MaxLength(255)]
    public string? DesComuniAuto { get; set; }

    [Column("DES_POBLCN"), MaxLength(64)]
    public string? DesPoblcn { get; set; }

    [Column("DES_TIPO_VIA"), MaxLength(255)]
    public string? DesTipoVia { get; set; }

    [Column("DES_BLOQUE"), MaxLength(20)]
    public string? DesBloque { get; set; }

    [Column("DES_PUERTA"), MaxLength(20)]
    public string? DesPuerta { get; set; }

    [Column("DES_PLANTA"), MaxLength(20)]
    public string? DesPlanta { get; set; }

    [Column("IND_STATUS"), MaxLength(1)]
    public string? IndStatus { get; set; }
}
