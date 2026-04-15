using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentIA.AssetResolver.Data.Entities;

/// <summary>
/// Entidad mapeada a la tabla DM_POSICION_AAII_TB de la base de datos ODS (dev) / ODS_DHW (pro).
/// Solo lectura — no se genera migración; la tabla es propiedad de otro sistema.
/// </summary>
[Table("DM_POSICION_AAII_TB")]
public class DmPosicionAAII
{
    // ── PK compuesta ──
    [Column("ID_ACTIVO_SAREB", TypeName = "decimal(16,0)")]
    public decimal IdActivoSareb { get; set; }

    [Column("FCH_CIERRE_DT")]
    public DateTime FchCierreDt { get; set; }

    // ── Fechas principales ──
    [Column("FCH_CIERRE")]
    public DateTime? FchCierre { get; set; }

    [Column("FCH_ALTA")]
    public DateTime? FchAlta { get; set; }

    [Column("FCH_BAJA")]
    public DateTime? FchBaja { get; set; }

    // ── Entidad ──
    [Column("ID_ENTIDD"), MaxLength(4)]
    public string? IdEntidd { get; set; }

    [Column("DES_ENTIDD"), MaxLength(255)]
    public string? DesEntidd { get; set; }

    [Column("ID_ENTIDD_ORIG"), MaxLength(4)]
    public string? IdEntiddOrig { get; set; }

    [Column("DES_ENTIDD_ORIG"), MaxLength(255)]
    public string? DesEntiddOrig { get; set; }

    // ── Adjudicación ──
    [Column("ID_ADJ001"), MaxLength(50)]
    public string? IdAdj001 { get; set; }

    [Column("ID_ADJ001_ORIG"), MaxLength(50)]
    public string? IdAdj001Orig { get; set; }

    [Column("ID_ADJ317"), MaxLength(50)]
    public string? IdAdj317 { get; set; }

    // ── Servicer ──
    [Column("COD_SERVICER"), MaxLength(4)]
    public string? CodServicer { get; set; }

    [Column("DES_SERVICER"), MaxLength(255)]
    public string? DesServicer { get; set; }

    // ── Identificación registral / catastral ──
    [Column("ID_REF_CATAST"), MaxLength(32)]
    public string? IdRefCatast { get; set; }

    [Column("DES_TOMO"), MaxLength(14)]
    public string? DesTomo { get; set; }

    [Column("DES_LIBRO"), MaxLength(14)]
    public string? DesLibro { get; set; }

    [Column("ID_IDUFIR"), MaxLength(14)]
    public string? IdIdufir { get; set; }

    [Column("ID_FIN_REG"), MaxLength(20)]
    public string? IdFinReg { get; set; }

    [Column("NUM_REG"), MaxLength(20)]
    public string? NumReg { get; set; }

    [Column("ID_SUBFIN_REG"), MaxLength(20)]
    public string? IdSubfinReg { get; set; }

    [Column("COD_REG_UNIVOC"), MaxLength(5)]
    public string? CodRegUnivoc { get; set; }

    [Column("COD_PROVNC_REG"), MaxLength(2)]
    public string? CodProvncReg { get; set; }

    [Column("DES_PROVNC_REG"), MaxLength(255)]
    public string? DesProvncReg { get; set; }

    [Column("COD_MUNICP_REG"), MaxLength(4)]
    public string? CodMunicpReg { get; set; }

    [Column("DES_MUNICP_REG"), MaxLength(255)]
    public string? DesMunicpReg { get; set; }

    // ── Tipo / Subtipo AAII ──
    [Column("COD_TIPO_AAII"), MaxLength(3)]
    public string? CodTipoAaii { get; set; }

    [Column("DES_TIPO_AAII"), MaxLength(255)]
    public string? DesTipoAaii { get; set; }

    [Column("COD_SUBTIPO_AAII"), MaxLength(2)]
    public string? CodSubtipoAaii { get; set; }

    [Column("DES_SUBTIPO_AAII"), MaxLength(255)]
    public string? DesSubtipoAaii { get; set; }

    // ── Localización ──
    [Column("COD_PAIS"), MaxLength(3)]
    public string? CodPais { get; set; }

    [Column("DES_PAIS"), MaxLength(255)]
    public string? DesPais { get; set; }

    [Column("COD_PROVNC"), MaxLength(2)]
    public string? CodProvnc { get; set; }

    [Column("DES_PROVNC"), MaxLength(255)]
    public string? DesProvnc { get; set; }

    [Column("COD_COMUNI_AUTO"), MaxLength(2)]
    public string? CodComuniAuto { get; set; }

    [Column("DES_COMUNI_AUTO"), MaxLength(255)]
    public string? DesComuniAuto { get; set; }

    [Column("COD_MUNICP"), MaxLength(4)]
    public string? CodMunicp { get; set; }

    [Column("DES_MUNICP"), MaxLength(255)]
    public string? DesMunicp { get; set; }

    [Column("DES_POBLCN"), MaxLength(64)]
    public string? DesPoblcn { get; set; }

    [Column("COD_TIPO_VIA"), MaxLength(2)]
    public string? CodTipoVia { get; set; }

    [Column("DES_TIPO_VIA"), MaxLength(255)]
    public string? DesTipoVia { get; set; }

    [Column("DES_NOMBRE_VIA"), MaxLength(255)]
    public string? DesNombreVia { get; set; }

    [Column("NUM_VIA"), MaxLength(20)]
    public string? NumVia { get; set; }

    [Column("DES_BLOQUE"), MaxLength(20)]
    public string? DesBloque { get; set; }

    [Column("DES_PUERTA"), MaxLength(20)]
    public string? DesPuerta { get; set; }

    [Column("NUM_COD_POSTAL"), MaxLength(20)]
    public string? NumCodPostal { get; set; }

    [Column("DES_PLANTA"), MaxLength(20)]
    public string? DesPlanta { get; set; }

    // ── Alquiler ──
    [Column("IND_ALQUILADO"), MaxLength(1)]
    public string? IndAlquilado { get; set; }

    [Column("FCH_INIVAL_ALQUI")]
    public DateTime? FchInivalAlqui { get; set; }

    [Column("FCH_FINVAL_ALQUI")]
    public DateTime? FchFinvalAlqui { get; set; }

    [Column("IMP_RENTA_ALQU", TypeName = "decimal(12,2)")]
    public decimal? ImpRentaAlqu { get; set; }

    [Column("FREQ_PAGO_ALQUI"), MaxLength(6)]
    public string? FreqPagoAlqui { get; set; }

    // ── Indicadores comerciales ──
    [Column("IND_PUBLICABLE"), MaxLength(1)]
    public string? IndPublicable { get; set; }

    [Column("IND_DISP_VENTA"), MaxLength(1)]
    public string? IndDispVenta { get; set; }

    [Column("IND_VARIAS_FINCAS"), MaxLength(1)]
    public string? IndVariasFincas { get; set; }

    [Column("IND_CARGAS"), MaxLength(1)]
    public string? IndCargas { get; set; }

    // ── Importes / Valoraciones ──
    [Column("IMP_PT", TypeName = "decimal(12,2)")]
    public decimal? ImpPt { get; set; }

    [Column("IMP_PT_CES", TypeName = "decimal(12,2)")]
    public decimal? ImpPtCes { get; set; }

    [Column("IMP_TASACION", TypeName = "decimal(12,2)")]
    public decimal? ImpTasacion { get; set; }

    [Column("IMP_VBC", TypeName = "decimal(12,2)")]
    public decimal? ImpVbc { get; set; }

    [Column("IMP_VBC_CES", TypeName = "decimal(12,2)")]
    public decimal? ImpVbcCes { get; set; }

    [Column("IND_PAGO_PEND"), MaxLength(1)]
    public string? IndPagoPend { get; set; }

    [Column("PRC_PROPIEDAD", TypeName = "decimal(5,2)")]
    public decimal? PrcPropiedad { get; set; }

    [Column("NUM_PLZAS_GARAJE", TypeName = "decimal(4,0)")]
    public decimal? NumPlzasGaraje { get; set; }

    [Column("NUM_TRASTE", TypeName = "decimal(4,0)")]
    public decimal? NumTraste { get; set; }

    // ── Estado construcción ──
    [Column("COD_ESTAD_CONSTR"), MaxLength(2)]
    public string? CodEstadConstr { get; set; }

    [Column("DES_ESTAD_CONSTR"), MaxLength(255)]
    public string? DesEstadConstr { get; set; }

    [Column("NUM_M2_CONSTR", TypeName = "decimal(18,2)")]
    public decimal? NumM2Constr { get; set; }

    [Column("NUM_M2_UTILES", TypeName = "decimal(18,2)")]
    public decimal? NumM2Utiles { get; set; }

    [Column("FCH_CONSTR")]
    public DateTime? FchConstr { get; set; }

    // ── Estado comercial ──
    [Column("COD_ESTAD_COMERC"), MaxLength(1)]
    public string? CodEstadComerc { get; set; }

    [Column("DESC_ESTAD_COMERC"), MaxLength(255)]
    public string? DescEstadComerc { get; set; }

    [Column("COD_MOTIV_NO_PUBLIC"), MaxLength(2)]
    public string? CodMotivNoPublic { get; set; }

    [Column("DES_MOTIV_NO_PUBLIC"), MaxLength(255)]
    public string? DesMotivNoPublic { get; set; }

    // ── Promoción ──
    [Column("ID_PROMCN_SAREB"), MaxLength(25)]
    public string? IdPromcnSareb { get; set; }

    [Column("IND_PROMCN_WIP"), MaxLength(1)]
    public string? IndPromcnWip { get; set; }

    [Column("FCH_CIERRE_OYV")]
    public DateTime? FchCierreOyv { get; set; }

    // ── Ofertas / Ventas ──
    [Column("NUM_OFERTAS", TypeName = "decimal(16,0)")]
    public decimal? NumOfertas { get; set; }

    [Column("COD_ESTADO_OFERT"), MaxLength(3)]
    public string? CodEstadoOfert { get; set; }

    [Column("DES_ESTADO_OFERT"), MaxLength(255)]
    public string? DesEstadoOfert { get; set; }

    [Column("IMP_PRECIO_ESCRIT", TypeName = "decimal(12,2)")]
    public decimal? ImpPrecioEscrit { get; set; }

    [Column("FCH_ESCRITURA")]
    public DateTime? FchEscritura { get; set; }

    [Column("IMP_PRECIO_ACEPT", TypeName = "decimal(12,2)")]
    public decimal? ImpPrecioAcept { get; set; }

    [Column("FCH_RESERVA")]
    public DateTime? FchReserva { get; set; }

    // ── Subsanación ──
    [Column("COD_TIPO_SUBSAN"), MaxLength(2)]
    public string? CodTipoSubsan { get; set; }

    [Column("DES_TIPO_SUBSAN"), MaxLength(255)]
    public string? DesTipoSubsan { get; set; }

    [Column("IMP_TRANSM_SUBSAN", TypeName = "decimal(12,2)")]
    public decimal? ImpTransmSubsan { get; set; }

    // ── Valoración Sareb ──
    [Column("FCH_VAL")]
    public DateTime? FchVal { get; set; }

    [Column("IMP_VAL_SAREB", TypeName = "decimal(12,2)")]
    public decimal? ImpValSareb { get; set; }

    [Column("FCH_VAL_PRICIN")]
    public DateTime? FchValPricin { get; set; }

    [Column("IMP_VAL_PRICIN_SAREB", TypeName = "decimal(12,2)")]
    public decimal? ImpValPricinSareb { get; set; }

    [Column("IMP_VAL_PRICIN_ALQUIL", TypeName = "decimal(12,2)")]
    public decimal? ImpValPricinAlquil { get; set; }

    [Column("FCH_COM_IMP")]
    public DateTime? FchComImp { get; set; }

    [Column("IMP_VAL_COM_MINIMO", TypeName = "decimal(12,2)")]
    public decimal? ImpValComMinimo { get; set; }

    [Column("IMP_VAL_COM_ALQUIL", TypeName = "decimal(12,2)")]
    public decimal? ImpValComAlquil { get; set; }

    [Column("IMP_VAL_COM_WEB", TypeName = "decimal(12,2)")]
    public decimal? ImpValComWeb { get; set; }

    // ── Cesión / Origen ──
    [Column("COD_TIPO_CES"), MaxLength(2)]
    public string? CodTipoCes { get; set; }

    [Column("DES_TIPO_CES"), MaxLength(255)]
    public string? DesTipoCes { get; set; }

    [Column("COD_TIPO_ORIGEN"), MaxLength(2)]
    public string? CodTipoOrigen { get; set; }

    [Column("DES_TIPO_ORIGEN"), MaxLength(255)]
    public string? DesTipoOrigen { get; set; }

    // ── Relación padre ──
    [Column("ID_ACTIVO_SAREB_PADRE", TypeName = "decimal(16,0)")]
    public decimal? IdActivoSarebPadre { get; set; }

    [Column("COD_TIPO_RELACN"), MaxLength(3)]
    public string? CodTipoRelacn { get; set; }

    [Column("DES_TIPO_RELACN_ACTIVO_ACTIVO"), MaxLength(255)]
    public string? DesTipoRelacnActivoActivo { get; set; }

    // ── Financieros ──
    [Column("IMP_CAPEX", TypeName = "decimal(12,2)")]
    public decimal? ImpCapex { get; set; }

    [Column("IMP_AMORT_ACUM", TypeName = "decimal(12,2)")]
    public decimal? ImpAmortAcum { get; set; }

    [Column("IND_GR"), MaxLength(1)]
    public string? IndGr { get; set; }

    // ── Registro ──
    [Column("COD_INSCRITO"), MaxLength(1)]
    public string? CodInscrito { get; set; }

    [Column("DES_INSCRITO"), MaxLength(255)]
    public string? DesInscrito { get; set; }

    [Column("ESCALERA"), MaxLength(128)]
    public string? Escalera { get; set; }

    // ── Ajuste contable ──
    [Column("FCH_AJUST_CONT")]
    public DateTime? FchAjustCont { get; set; }

    [Column("IMP_AJUST_CONT", TypeName = "decimal(12,2)")]
    public decimal? ImpAjustCont { get; set; }

    [Column("FCH_ULT_TASACN")]
    public DateTime? FchUltTasacn { get; set; }

    [Column("IND_ADJDCN"), MaxLength(1)]
    public string? IndAdjdcn { get; set; }

    // ── Situación comercial ──
    [Column("COD_SITUAC_COMERC"), MaxLength(2)]
    public string? CodSituacComerc { get; set; }

    [Column("DES_SITUAC_COMERC"), MaxLength(255)]
    public string? DesSituacComerc { get; set; }

    [Column("IMP_VACBE", TypeName = "decimal(12,2)")]
    public decimal? ImpVacbe { get; set; }

    [Column("FCH_VACBE")]
    public DateTime? FchVacbe { get; set; }

    [Column("IMP_TASACN_DD", TypeName = "decimal(12,2)")]
    public decimal? ImpTasacnDd { get; set; }

    [Column("DES_SECCION"), MaxLength(14)]
    public string? DesSeccion { get; set; }

    // ── Unidad / Segmento ──
    [Column("COD_UNIDAD_ACT"), MaxLength(1)]
    public string? CodUnidadAct { get; set; }

    [Column("DES_UNIDAD_ACT"), MaxLength(255)]
    public string? DesUnidadAct { get; set; }

    [Column("ID_SEG"), MaxLength(3)]
    public string? IdSeg { get; set; }

    [Column("DES_SEG"), MaxLength(255)]
    public string? DesSeg { get; set; }

    [Column("COD_TIPO_SEG"), MaxLength(2)]
    public string? CodTipoSeg { get; set; }

    [Column("DES_TIPO_SEG"), MaxLength(255)]
    public string? DesTipoSeg { get; set; }

    [Column("COD_SUBTIP_AAII_SAREB"), MaxLength(2)]
    public string? CodSubtipAaiiSareb { get; set; }

    [Column("DES_SUBTIP_AAII_SAREB"), MaxLength(255)]
    public string? DesSubtipAaiiSareb { get; set; }

    [Column("NUM_REG_FOLIO"), MaxLength(14)]
    public string? NumRegFolio { get; set; }

    [Column("NUM_SUPERF", TypeName = "decimal(10,0)")]
    public decimal? NumSuperf { get; set; }

    [Column("NUM_ANIO_CONSTR"), MaxLength(255)]
    public string? NumAnioConstr { get; set; }

    // ── Info jurídica ──
    [Column("COD_INFO_JURIDIC"), MaxLength(3)]
    public string? CodInfoJuridic { get; set; }

    [Column("DES_INFO_JURIDIC"), MaxLength(255)]
    public string? DesInfoJuridic { get; set; }

    [Column("COD_SB_ESTADO_COMERC"), MaxLength(2)]
    public string? CodSbEstadoComerc { get; set; }

    [Column("DES_SB_ESTADO_COMERC"), MaxLength(255)]
    public string? DesSbEstadoComerc { get; set; }

    [Column("FCH_MODIF_SAVES")]
    public DateTime? FchModifSaves { get; set; }

    [Column("IND_MEJORA_SAVES"), MaxLength(1)]
    public string? IndMejoraSaves { get; set; }

    // ── Auditoría ──
    [Column("COD_USU_ALTA"), MaxLength(7)]
    public string? CodUsuAlta { get; set; }

    [Column("COD_USU_MOD"), MaxLength(7)]
    public string? CodUsuMod { get; set; }

    [Column("IND_STATUS"), MaxLength(1)]
    public string? IndStatus { get; set; }

    [Column("TS_ALTA")]
    public DateTime? TsAlta { get; set; }

    // ── Uso domicilio / VPO ──
    [Column("COD_CL_USO_DOM"), MaxLength(2)]
    public string? CodClUsoDom { get; set; }

    [Column("DES_CL_USO_DOM"), MaxLength(255)]
    public string? DesClUsoDom { get; set; }

    [Column("IND_PROTEC"), MaxLength(1)]
    public string? IndProtec { get; set; }

    [Column("FCH_CALDEF_VPO")]
    public DateTime? FchCaldefVpo { get; set; }

    [Column("FCH_FIN_VPO")]
    public DateTime? FchFinVpo { get; set; }

    // ── Catastro / Registro nominal ──
    [Column("IND_CAT_NOM_SAREB"), MaxLength(1)]
    public string? IndCatNomSareb { get; set; }

    [Column("IND_REG_NOM_SAREB"), MaxLength(1)]
    public string? IndRegNomSareb { get; set; }

    [Column("DES_NOMBRE_REG"), MaxLength(50)]
    public string? DesNombreReg { get; set; }

    [Column("FCH_REG")]
    public DateTime? FchReg { get; set; }

    [Column("ID_ACTIVO_UNIREG"), MaxLength(25)]
    public string? IdActivoUnireg { get; set; }

    // ── Fechas complementarias ──
    [Column("FCH_ADJ")]
    public DateTime? FchAdj { get; set; }

    [Column("FCH_ALTA_BALANC")]
    public DateTime? FchAltaBalanc { get; set; }

    [Column("FCH_POS")]
    public DateTime? FchPos { get; set; }

    // ── Valoración mercado ──
    [Column("IMP_VALMER_ALQ", TypeName = "decimal(12,2)")]
    public decimal? ImpValmerAlq { get; set; }

    [Column("IMP_VALMER_VENTA", TypeName = "decimal(12,2)")]
    public decimal? ImpValmerVenta { get; set; }

    [Column("IMP_PVP", TypeName = "decimal(12,2)")]
    public decimal? ImpPvp { get; set; }

    // ── Gastos e impuestos ──
    [Column("IMP_IBI", TypeName = "decimal(12,2)")]
    public decimal? ImpIbi { get; set; }

    [Column("FCH_LIQVOL_IBI")]
    public DateTime? FchLiqvolIbi { get; set; }

    [Column("IMP_GASCOM", TypeName = "decimal(12,2)")]
    public decimal? ImpGascom { get; set; }

    [Column("IMP_GASLIM", TypeName = "decimal(12,2)")]
    public decimal? ImpGaslim { get; set; }

    [Column("IMP_GASMAN", TypeName = "decimal(12,2)")]
    public decimal? ImpGasman { get; set; }

    [Column("IMP_GASVIG", TypeName = "decimal(12,2)")]
    public decimal? ImpGasvig { get; set; }

    [Column("IMP_GASSUM", TypeName = "decimal(12,2)")]
    public decimal? ImpGassum { get; set; }

    [Column("IMP_TASA_BAS", TypeName = "decimal(12,2)")]
    public decimal? ImpTasaBas { get; set; }

    // ── Certificados / Licencias ──
    [Column("IND_CFO"), MaxLength(1)]
    public string? IndCfo { get; set; }

    [Column("IND_LPO"), MaxLength(1)]
    public string? IndLpo { get; set; }

    [Column("COD_CERTIF_ENERG"), MaxLength(1)]
    public string? CodCertifEnerg { get; set; }

    [Column("DES_CERTIF_ENERG"), MaxLength(255)]
    public string? DesCertifEnerg { get; set; }

    // ── Superficies ──
    [Column("NUM_SUP_PARCEL", TypeName = "decimal(8,2)")]
    public decimal? NumSupParcel { get; set; }

    [Column("NUM_SUP_TERRA", TypeName = "decimal(6,2)")]
    public decimal? NumSupTerra { get; set; }

    [Column("NUM_SUP_OFI", TypeName = "decimal(8,2)")]
    public decimal? NumSupOfi { get; set; }

    [Column("NUM_HABIT", TypeName = "decimal(4,0)")]
    public decimal? NumHabit { get; set; }

    [Column("NUM_BANOS", TypeName = "decimal(3,0)")]
    public decimal? NumBanos { get; set; }

    [Column("NUM_VIVIEN", TypeName = "decimal(4,0)")]
    public decimal? NumVivien { get; set; }

    // ── Estado local ──
    [Column("COD_ESTADO_LOCAL"), MaxLength(2)]
    public string? CodEstadoLocal { get; set; }

    [Column("DES_ESTADO_LOCAL"), MaxLength(255)]
    public string? DesEstadoLocal { get; set; }

    [Column("IND_EQUIP_TRAST"), MaxLength(1)]
    public string? IndEquipTrast { get; set; }

    [Column("FCH_INSTEC_VIG")]
    public DateTime? FchInstecVig { get; set; }

    [Column("FCH_PROX_INSTEC")]
    public DateTime? FchProxInstec { get; set; }

    [Column("IND_MEJORA_CATAST_REG"), MaxLength(1)]
    public string? IndMejoraCatastReg { get; set; }

    [Column("FCH_COM_ALQUIL")]
    public DateTime? FchComAlquil { get; set; }

    // ── Estado general ──
    [Column("COD_ESTADO_GEN"), MaxLength(2)]
    public string? CodEstadoGen { get; set; }

    [Column("DES_ESTADO_GEN"), MaxLength(255)]
    public string? DesEstadoGen { get; set; }

    [Column("COD_CATEG_SUELO"), MaxLength(2)]
    public string? CodCategSuelo { get; set; }

    [Column("DES_CATEG_SUELO"), MaxLength(255)]
    public string? DesCategSuelo { get; set; }

    [Column("IND_EDIFICIO"), MaxLength(1)]
    public string? IndEdificio { get; set; }

    // ── Método valoración / Uso complementario ──
    [Column("COD_MET_VAL"), MaxLength(2)]
    public string? CodMetVal { get; set; }

    [Column("DES_MET_VAL"), MaxLength(255)]
    public string? DesMetVal { get; set; }

    [Column("COD_CL_USO_COMP"), MaxLength(2)]
    public string? CodClUsoComp { get; set; }

    [Column("DES_CL_USO_COMP"), MaxLength(255)]
    public string? DesClUsoComp { get; set; }

    // ── Superficies sobre/bajo rasante ──
    [Column("NUM_SUP_SOBRTE", TypeName = "decimal(8,2)")]
    public decimal? NumSupSobrte { get; set; }

    [Column("NUM_SUP_BAJRTE", TypeName = "decimal(8,2)")]
    public decimal? NumSupBajrte { get; set; }

    [Column("COD_ANEJO_INDBLE"), MaxLength(1)]
    public string? CodAnejoIndble { get; set; }

    // ── Motivos trazabilidad ──
    [Column("COD_MOTIVO_TRZB_ALTA"), MaxLength(3)]
    public string? CodMotivoTrzbAlta { get; set; }

    [Column("COD_MOTIVO_TRZB_BAJA"), MaxLength(3)]
    public string? CodMotivoTrzbBaja { get; set; }

    [Column("COD_ORIGEN_ESTADO_ALTA"), MaxLength(2)]
    public string? CodOrigenEstadoAlta { get; set; }

    [Column("COD_ORIGEN_ESTADO_BAJA"), MaxLength(2)]
    public string? CodOrigenEstadoBaja { get; set; }

    [Column("COD_RIESGO_FISICO"), MaxLength(2)]
    public string? CodRiesgoFisico { get; set; }

    [Column("COD_TIPO_COMCAL"), MaxLength(2)]
    public string? CodTipoComcal { get; set; }

    [Column("COD_TIPO_VAL"), MaxLength(2)]
    public string? CodTipoVal { get; set; }

    [Column("DES_ANEJO_INDBLE"), MaxLength(255)]
    public string? DesAnejoIndble { get; set; }

    [Column("DES_MOTIVO_TRZB_ALTA"), MaxLength(255)]
    public string? DesMotivoTrzbAlta { get; set; }

    [Column("DES_MOTIVO_TRZB_BAJA"), MaxLength(255)]
    public string? DesMotivoTrzbBaja { get; set; }

    [Column("DES_ORIGEN_ESTADO_ALTA"), MaxLength(255)]
    public string? DesOrigenEstadoAlta { get; set; }

    [Column("DES_ORIGEN_ESTADO_BAJA"), MaxLength(255)]
    public string? DesOrigenEstadoBaja { get; set; }

    [Column("DES_RIESGO_FISICO"), MaxLength(255)]
    public string? DesRiesgoFisico { get; set; }

    [Column("DES_TIPO_COMCAL"), MaxLength(255)]
    public string? DesTipoComcal { get; set; }

    [Column("DES_TIPO_VAL"), MaxLength(255)]
    public string? DesTipoVal { get; set; }

    // ── Fechas adicionales ──
    [Column("FCH_CED_HABITA")]
    public DateTime? FchCedHabita { get; set; }

    [Column("FCH_CES")]
    public DateTime? FchCes { get; set; }

    [Column("FCH_CFO")]
    public DateTime? FchCfo { get; set; }

    [Column("FCH_FIN_CERTIF_ENERG")]
    public DateTime? FchFinCertifEnerg { get; set; }

    [Column("FCH_INI_COMERC")]
    public DateTime? FchIniComerc { get; set; }

    [Column("FCH_LPO")]
    public DateTime? FchLpo { get; set; }

    [Column("FCH_OBTENC_LICFUN_LOCAL")]
    public DateTime? FchObtencLicfunLocal { get; set; }

    [Column("FCH_OBTENC_LICFUN_NAVE")]
    public DateTime? FchObtencLicfunNave { get; set; }

    // ── Registro inscripción ──
    [Column("ID_REG_INSCRI", TypeName = "decimal(2,0)")]
    public decimal? IdRegInscri { get; set; }

    [Column("IMP_DER", TypeName = "decimal(12,2)")]
    public decimal? ImpDer { get; set; }

    [Column("IMP_IBI_BON", TypeName = "decimal(12,2)")]
    public decimal? ImpIbiBon { get; set; }

    // ── Equipamiento ──
    [Column("IND_AIRE_ACOND"), MaxLength(1)]
    public string? IndAireAcond { get; set; }

    [Column("IND_ALARMA"), MaxLength(1)]
    public string? IndAlarma { get; set; }

    [Column("IND_CED_HABITA"), MaxLength(1)]
    public string? IndCedHabita { get; set; }

    [Column("IND_EQUIP_CIRC_SEGUR"), MaxLength(1)]
    public string? IndEquipCircSegur { get; set; }

    [Column("IND_EQUIP_COCINA"), MaxLength(1)]
    public string? IndEquipCocina { get; set; }

    [Column("IND_EQUIP_COCIND"), MaxLength(1)]
    public string? IndEquipCocind { get; set; }

    [Column("IND_INSTAL_BASIC"), MaxLength(1)]
    public string? IndInstalBasic { get; set; }

    [Column("IND_LICFUN_LOCAL"), MaxLength(1)]
    public string? IndLicfunLocal { get; set; }

    [Column("IND_LICFUN_NAVE"), MaxLength(1)]
    public string? IndLicfunNave { get; set; }

    [Column("NUM_ASCENS", TypeName = "decimal(3,0)")]
    public decimal? NumAscens { get; set; }

    [Column("NUM_DOR", TypeName = "decimal(3,0)")]
    public decimal? NumDor { get; set; }

    [Column("NUM_REG_SECCN"), MaxLength(14)]
    public string? NumRegSeccn { get; set; }

    [Column("NUM_SUP_JAR", TypeName = "decimal(6,2)")]
    public decimal? NumSupJar { get; set; }

    [Column("NUM_TERRA", TypeName = "decimal(4,0)")]
    public decimal? NumTerra { get; set; }

    // ── Pricing alquiler ──
    [Column("FCH_VAL_PRICIN_ALQUIL")]
    public DateTime? FchValPricinAlquil { get; set; }

    [Column("IMP_VAL_SAREB_ALQUIL", TypeName = "decimal(12,2)")]
    public decimal? ImpValSarebAlquil { get; set; }

    [Column("FCH_VAL_SAREB_ALQUIL")]
    public DateTime? FchValSarebAlquil { get; set; }

    [Column("DES_NOM_TASAC_COMPL"), MaxLength(128)]
    public string? DesNomTasacCompl { get; set; }

    // ── PVP / PAP Sareb ──
    [Column("IMP_PVP_SAREB", TypeName = "decimal(12,2)")]
    public decimal? ImpPvpSareb { get; set; }

    [Column("FCH_PVP_SAREB")]
    public DateTime? FchPvpSareb { get; set; }

    [Column("IMP_PAP_SAREB", TypeName = "decimal(12,2)")]
    public decimal? ImpPapSareb { get; set; }

    [Column("FCH_PAP_SAREB")]
    public DateTime? FchPapSareb { get; set; }

    // ── Operaciones ──
    [Column("ID_OPERAC_ALTA"), MaxLength(20)]
    public string? IdOperacAlta { get; set; }

    [Column("ID_OPERAC_BAJA"), MaxLength(20)]
    public string? IdOperacBaja { get; set; }

    // ── Geolocalización ──
    [Column("NUM_GEO_X"), MaxLength(255)]
    public string? NumGeoX { get; set; }

    [Column("NUM_GEO_Y"), MaxLength(255)]
    public string? NumGeoY { get; set; }

    [Column("IND_DPI"), MaxLength(1)]
    public string? IndDpi { get; set; }

    // ── Servicer comercial / Gestión ──
    [Column("COD_SERVICER_COMERCIAL")]
    public int? CodServicerComercial { get; set; }

    [Column("DES_SERVICER_COMERCIAL"), MaxLength(255)]
    public string? DesServicerComercial { get; set; }

    [Column("COD_MODELO_GESTION")]
    public int? CodModeloGestion { get; set; }

    [Column("DES_MODELO_GESTION"), MaxLength(255)]
    public string? DesModeloGestion { get; set; }

    [Column("COD_PERIMETRO_OPERATIVO")]
    public int? CodPerimetroOperativo { get; set; }

    [Column("DES_PERIMETRO_OPERATIVO"), MaxLength(255)]
    public string? DesPerimetroOperativo { get; set; }

    [Column("COD_TERRITORIAL")]
    public int? CodTerritorial { get; set; }

    [Column("DES_TERRITORIAL"), MaxLength(255)]
    public string? DesTerritorial { get; set; }

    // ── Estado bloqueo / Unidad WIP ──
    [Column("COD_ESTADO_BLOQUEO"), MaxLength(2)]
    public string? CodEstadoBloqueo { get; set; }

    [Column("COD_ESTADO_COMERC_WIP", TypeName = "numeric(18,0)")]
    public decimal? CodEstadoComercWip { get; set; }

    [Column("COD_ESTADO_UNIDAD"), MaxLength(2)]
    public string? CodEstadoUnidad { get; set; }

    [Column("COD_PERIM_PRIOR"), MaxLength(8)]
    public string? CodPerimPrior { get; set; }

    // ── Obra / Promoción extendida ──
    [Column("DES_CAPITU_OBRA"), MaxLength(255)]
    public string? DesCapituObra { get; set; }

    [Column("DES_DENOM_COMERC"), MaxLength(255)]
    public string? DesDenomComerc { get; set; }

    [Column("DES_DIREC_OBRA"), MaxLength(255)]
    public string? DesDirecObra { get; set; }

    [Column("DES_DOC_TEC"), MaxLength(255)]
    public string? DesDocTec { get; set; }

    [Column("DES_EMP_CONSTR"), MaxLength(255)]
    public string? DesEmpConstr { get; set; }

    [Column("DES_ESTADO_BLOQUEO"), MaxLength(255)]
    public string? DesEstadoBloqueo { get; set; }

    [Column("DES_ESTADO_COMERC_WIP"), MaxLength(255)]
    public string? DesEstadoComercWip { get; set; }

    [Column("DES_ESTADO_UNIDAD"), MaxLength(255)]
    public string? DesEstadoUnidad { get; set; }

    [Column("DES_EXPDT_MUNICP"), MaxLength(50)]
    public string? DesExpdtMunicp { get; set; }

    [Column("DES_PERIM_PRIOR"), MaxLength(250)]
    public string? DesPerimPrior { get; set; }

    [Column("DES_REQUER_MUNICP"), MaxLength(255)]
    public string? DesRequerMunicp { get; set; }

    [Column("DES_SUP_PLANTA"), MaxLength(500)]
    public string? DesSupPlanta { get; set; }

    [Column("FCH_LIC_OBRAS")]
    public DateTime? FchLicObras { get; set; }

    [Column("FCH_PARLZ_OBRAS")]
    public DateTime? FchParlzObras { get; set; }

    [Column("IMP_VALOR_CATAST", TypeName = "decimal(12,2)")]
    public decimal? ImpValorCatast { get; set; }

    [Column("IMP_VALOR_RIESGO_VENTA", TypeName = "decimal(12,2)")]
    public decimal? ImpValorRiesgoVenta { get; set; }

    [Column("IND_BLOQUEO_VNC"), MaxLength(1)]
    public string? IndBloqueoVnc { get; set; }

    [Column("IND_CONVEN_COMERC"), MaxLength(1)]
    public string? IndConvenComerc { get; set; }

    [Column("IND_MULTI_SERV"), MaxLength(1)]
    public string? IndMultiServ { get; set; }

    [Column("IND_PUBLIC_WEB_SAREB"), MaxLength(1)]
    public string? IndPublicWebSareb { get; set; }

    [Column("IND_VENTA_COBRO_APLAZ"), MaxLength(1)]
    public string? IndVentaCobroAplaz { get; set; }

    [Column("NUM_CADUC_LIC", TypeName = "numeric(18,0)")]
    public decimal? NumCaducLic { get; set; }

    [Column("NUM_DIV_HOR", TypeName = "numeric(18,0)")]
    public decimal? NumDivHor { get; set; }

    [Column("NUM_DIV_HOR_PROMCN", TypeName = "numeric(18,0)")]
    public decimal? NumDivHorPromcn { get; set; }

    [Column("NUM_ESTADO_OBRAP", TypeName = "numeric(18,0)")]
    public decimal? NumEstadoObrap { get; set; }

    [Column("NUM_INCID_OCT", TypeName = "numeric(18,0)")]
    public decimal? NumIncidOct { get; set; }

    [Column("NUM_LOCALES", TypeName = "decimal(4,0)")]
    public decimal? NumLocales { get; set; }

    [Column("NUM_LOCALES_PROMCN", TypeName = "decimal(4,0)")]
    public decimal? NumLocalesPromcn { get; set; }

    [Column("NUM_OTROS", TypeName = "decimal(4,0)")]
    public decimal? NumOtros { get; set; }

    [Column("NUM_OTROS_PROMCN", TypeName = "decimal(4,0)")]
    public decimal? NumOtrosPromcn { get; set; }

    [Column("NUM_PLANTA_BAJRTE", TypeName = "decimal(4,0)")]
    public decimal? NumPlantaBajrte { get; set; }

    [Column("NUM_PLANTA_SOBRTE", TypeName = "decimal(4,0)")]
    public decimal? NumPlantaSobrte { get; set; }

    [Column("NUM_PLZAS_GARAJE_PROMCN", TypeName = "decimal(4,0)")]
    public decimal? NumPlzasGarajePromcn { get; set; }

    [Column("NUM_SUP_PARCEL_PROMCN", TypeName = "decimal(8,2)")]
    public decimal? NumSupParcelPromcn { get; set; }

    [Column("NUM_TIPO_CONSTR", TypeName = "numeric(18,0)")]
    public decimal? NumTipoConstr { get; set; }

    [Column("NUM_TRASTE_PROMCN", TypeName = "decimal(4,0)")]
    public decimal? NumTrastePromcn { get; set; }

    [Column("NUM_VIG_OBRA", TypeName = "numeric(18,0)")]
    public decimal? NumVigObra { get; set; }

    [Column("NUM_VIVIEN_PROMCN", TypeName = "decimal(4,0)")]
    public decimal? NumVivienPromcn { get; set; }

    [Column("PRC_CUOTA_PARTIC", TypeName = "decimal(5,2)")]
    public decimal? PrcCuotaPartic { get; set; }

    [Column("PRC_EJEC_OBRA", TypeName = "decimal(5,2)")]
    public decimal? PrcEjecObra { get; set; }

    [Column("PRC_PROTEC", TypeName = "decimal(5,2)")]
    public decimal? PrcProtec { get; set; }
}
