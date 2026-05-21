using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCatalogoTdn1Tdn2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CatalogoTdn1",
                columns: new[] { "Id", "Codigo", "Descripcion", "Nombre" },
                values: new object[,]
                {
                    { 1, "ACTE", "Documentos administrativos oficiales específicos de un procedimiento expropiatorio en el que se deja asiento de la descripción de un bien, la titularidad y ocupación, el importe de valoración y su abono o consignación, entre otros.Dentro de cada TDN2 se encuentran embebidos: Actas complementarias y otros documentos relacionados", "Actas de expropiación" },
                    { 2, "ACTR", "Testimonios, asientos o constancia oficial de lo tratado o acordado en una junta, consejo, asamblea, comite, equipo de trabajo, etc.Dentro de cada TDN2 se encuentran embebidos: Borradores, versiones, modificaciones, validaciones y rectificaciones, orden del día", "Actas de reunión" },
                    { 3, "ACTT", "Testimonios, asientos o constancia oficial del nombramiento de un técnico o de un hecho emitido por técnico competente (dirección facultativa, coordinador de seguridad y salud, órgano de control técnico, técnico múnicipal..) durante la ejecución de una obra, tanto de urbanización, como edificación o mantenimiento.Dentro de cada TDN2 se encuentran embebidos: Borradores, versiones, modificaciones, validaciones, rectificaciones y subsanaciones así como otros documentos relacionados", "Actas técnicas" },
                    { 4, "ACUE", "Escritos y acuerdos de un ógano distinto a Sareb (y nunca Adm. Pública) por el que se consiente o no algún acto normamente solicitado. Se caracteriza por su unilateralidad.Dentro de cada TDN2 se encuentran embebidos: Borradores, documentos firmados, denegaciones y rechazos así como escritos de contestación u observación de las mismas y/o adopción de medidas.", "Acuerdos, aprobaciones, desistimientos y autorizaciones comités y órganos externos" },
                    { 5, "ACUI", "Escritos y acuerdos de los órganos de decisión de Sareb por los que se consiente o no algún acto solicitado. Se caracteriza por la unilateralidad.Dentro de cada TDN2 se encuentran embebidos. Borradores, documentos firmados, denegaciones y rechazos así como escritos de contestación u observación de las mismas y/o adopción de medidas.", "Acuerdos, aprobaciones, desistimientos y autorizaciones internas Sareb" },
                    { 6, "ALEG", "Documentos por los que el interesado en un procedimiento argumenta hechos y derechos en defensa de su causa. Dentro de cada TDN2 se encuentran embebidos: contestaciones a las alegaciones y anexos cuando se considere que no tienen entidad suficiente para ser catalogados de manera independiente, así como la notificación o comunicación de este tipo de documentos y los medios utilizados (ejemplo: burofax, correo certificado etc.).", "Alegaciones" },
                    { 7, "ANEX", "Información que acompaña a cualquier tipo documental y que por sus características e importancia debe ser considerado como un tipo documental independiente.", "Anexos" },
                    { 8, "AUVI", "Documento cuyo contenido visual y/o sonoro está incorporado en un soporte y tiene una duración lineal.", "Documento audiovisual" },
                    { 9, "AYSU", "Documento por el que se acredita la concesión a una persona, una entidad o una institución de una ayuda económica para realizar una obra o actividad, especialmente la que se recibe del Estado o de un organismo oficial.Dentro de cada TDN2 se encuentran embebidos: solicitudes, requerimientos, concesiones, denegaciones y modificaciones, rescisiones, etc..", "Ayudas y subvenciones" },
                    { 10, "CEDU", "Documentos o certificados administrativos municipales acreditativos de las circunstancias urbanísticas que concurran en las fincas, solares o parcelasDentro de cada TDN2 pueden encuentrarse embebidos: solicitudes, denegaciones, escritos observaciones, etc", "Cédulas urbanísticas" },
                    { 11, "CERA", "Documentos en los que se justifica y acredita cualquier actividad de pago y/o cobro ya sea por impuestos, tasas, contribuciones, abonos por contratos, ventas, alquileres, etc.Los medios de pago/cobro tales como transferencia bancaria, cheque, efectivo se engloban en el término \"justificante abono\".", "Certificados, autoliquidaciones, justificantes y recibos de pago / cobro" },
                    { 12, "CERJ", "Documentos que acreditan, justifican o aseguran la veracidad de un hecho cuando no sean ni de carácter técnico ni manifiesten la realización de un cobro y/o pago.Incluye cualquier tipo de materia incluyendo la económica, siempre que esta no tenga relación con la justificación de un pago/cobro.", "Certificados, justificantes y recibos" },
                    { 13, "CERT", "Documentos que acreditan y aseguran la veracidad de un hecho relacionado con calquier aspecto técnico de un activo inmobiliario o de la fase en la que se encuentre.", "Certificados técnicos" },
                    { 14, "CNCV", "Pactos escritos entre partes que se obligan de manera onerosa o gratuita sobre materia o cosa determinada, y a cuyo cumplimiento pueden ser compelidas. Se caraceriza por su bilateralidad. Dentro de cada TDN2 se encuentran embebidos: Borradores, documentos firmados, denegaciones y rechazos así como escritos de contestación u observación de las mismas y/o adopción de medidas.", "Contratos y convenios" },
                    { 15, "COMU", "Documentos en los que se recogen las transmisiones e intercambio de información entre distintos intervinientes.Los medios de comunicación como emal, burofax, etc..cuando tengan un TDN2 específico en razón de la materia, se engloban en el término \"\"comunicación\"\". En este TDN1 se incluyen los requerimientos, solicitudes, notificaciones, etc.", "Comunicaciones" },
                    { 16, "CONV", "Anuncio o escrito con el cual se convoca a un determinado evento o acontecimiento que se producirá.Dentro de cada TDN2 se encuentran embebidos las modificaciones, suspensiones y contestaciones a las convocatorias", "Convocatorias" },
                    { 17, "CORR", "Medio de comunicación que un emisor (remitente) envía a su receptor por medio de correo ordinario o electrónico", "Correspondencia" },
                    { 18, "CUAD", "Conjuntos de nombres, cifras u otros datos presentados gráficamente, de manera que se advierta la relación existente entre ellos. En este TDN1 se incluyen los plannigs", "Cuadros" },
                    { 19, "CULC", "Pliegos compuestos de varias partidas económicas , que tras operaciones de sumas y/o restas ofrecen un resultado. En este TDN1 se incluyen los estados financieros y los balances de situación", "Cuentas y libros contables" },
                    { 20, "DEAC", "Decisiones o resoluciones de una autoridad pública de carácter reglamentario o administrativo, que influye de manera directa o indirecta en el estado o gestión de un activo. (se caracteriza por la unilateralidad)", "Decretos y acuerdos de la Administración Pública" },
                    { 21, "DECL", "Se incluyen en este TDN1 las declaraciones de aquellos impuestos y tasas cuyo pago es periódico. (Los impuestos y tasas cuyo pago se realice una única vez, se incluirán en el TDN1 Certificados, autoliquidaciones, justificantes y recibos de pago / cobro, ya que lo relenvante en estos casos es la justificación del pago en sí.)", "Declaraciones de impuestos, tasas, Seguridad Social, Catastro" },
                    { 22, "DOCA", "Documentos que dan soporte a los actos de decisión (resoluciones y acuerdos) de la Administración pública En este TDN1 se incluyen, oficios, notas interiores, cartas, actas, certificados, diligencias etc.", "Documentos administrativos" },
                    { 23, "DOCI", "Documento legales y oficiales de identificación de cada persona (física o jurídica) para todos los actos civiles, administrativos, legales y en general, para todos los actos en que, por ley, la persona deba identificarse.", "Documentos identificativos" },
                    { 24, "DOCJ", "Diferentes tipos de escritos emitidos por los intervenientes en algún proceso que se atienden en los tribunales. Se excluyen: Los emitidos o promulgados por los juzgados y tribunales", "Documentos judiciales" },
                    { 25, "DOCN", "Documentos que tienen como contenido la constatación de hechos o la percepción que de los mismos tenga el notario,así como sus juicios y calificaciones. En este TDN1 se incluyen, entre otros, las actas de presencia, de manifestaciones, de remisión de documentos por correo, de notificación y requerimiento, de exhibición de cosas o documentos, de notoriedad, de protocolización, de depósito ante notario, de subasta (como va a ser la subasta) y de sorteo.", "Documentos notariales" },
                    { 26, "DOCT", "Todos los documentos que no teniendo su propia catalogación a nivel TDN1 ( ejemplo, normas, memorias, planos..) definen y determinan las exigencias técnicas de un instrumento urbanístico o de arquitectura.", "Documentación técnica" },
                    { 27, "DOSS", "Conjunto de documentos o informes acerca de un determinado asunto.", "Dossieres" },
                    { 28, "ESCR", "Documentos en los que se hace constar un determinado acontecimiento (declaraciones de voluntad, los actos jurídicos que impliquen prestación de consentimiento, los contratos y negocios jurídicos de todas clases) ante la presencia de un notario público, quien da fe de la capacidad jurídica del contenido y de la fecha en la que se llevó acabo y que firma junto con el otorgante u los otorgantes.Dentro de cada TDN2 se encuentran embebidos los originales, las copias simples y las copias autorizadas, y las subsanaciones.", "Escrituras" },
                    { 29, "ESIN", "Descripciones escritas, de las características y circunstancias de un suceso o asunto relacionado con la situación o gestión de un activo o una persona.Incluye preliminares y definitivos", "Estudios e informes" },
                    { 30, "ESTA", "Forma acordada por los socios o los fundadores de una sociedad, entidad urbanística, comunidad de propietarios, etc que regula el funcionamiento de una persona jurídica.Dentro de cada TDN2 se encuentran embebidos: Borradores, versiones, modificaciones, validaciones y rectificaciones, etc.", "Estatutos" },
                    { 31, "ESTT", "Obras técnicas en que un técnico cualificado estudia y dilucida una cuestión o materia determinada que influye de manera directa en la correcta realización de una obra de arquitectura o ingeniería o en el propio estado o gestión de un activo.", "Estudios técnicos" },
                    { 32, "FACT", "Cuentas económicas detalladas de una operación de venta, por servicios prestados u obras. Se incluyen tanto las facturas comerciales como aquellas derivadas de los pagos a profesionales y organismos.Dentro de cada TDN2 se encuentran embebidos las facturas pro-forma.", "Facturas" },
                    { 33, "FIAV", "Prendas que da el contratante en seguridad del buen cumplimiento de su obligación.El aval / fianza abarca el aval bancario y el personal", "Fianzas, avales y depósitos" },
                    { 34, "FICH", "Folios en el que se anotan de manera esquemática datos generales, sobre un activo, operación, persona, etc.", "Fichas" },
                    { 35, "FOTO", "Imágenes capturadas del estado del activo", "Fotografías" },
                    { 36, "GARA", "Documentos del fabricante o vendedor de un bien, por el que se obliga a reparar gratuitamente algo vendido en caso de avería.", "Garantías" },
                    { 37, "HOJA", "Documentos que deben presentar los propietarios afectados por un procedimiento de expropiación para aportar elementos de juicio que contribuyan a determinar el justiprecio del valor del bien objeto de expropiación.", "Hojas de aprecio" },
                    { 38, "INLI", "Conjunto de datos ordenados siguiendo algún tipo de características particulares a fin de enumerarlos y organizarlos.", "Inventarios y listados" },
                    { 39, "INRG", "Extensión, suspensión o denegación por parte del registrador de una inscripción, anotación, nota marginal o cancelación solicitada.Dentro de cada TDN2 se encuentran embebidos: solicitudes, requerimientos, subsanaciones, notificaciones, entre otros.", "Inscripciones y/o calificaciones registros" },
                    { 40, "INSS", "Documentos acreditativos de registro previo para cumplir con una determinada obligación tributaria", "Inscripciones, altas de impuestos, tasas y Seguridad Social" },
                    { 41, "LICM", "Documentos que, entendidos como compendio o colección de textos seleccionados y fácilmente localizables, contienen información válida y clasificada sobre una determinada materia, tratándose como una guía.Se incluye en este TDN1 los documentos que reunen el historial de un conjunto de hechos o acciones tomadas durante la realización de las obras, complementando en su caso las determinaciones de los instrumentos de planeamiento relativas a la conservación, protección o mejora del patrimonio urbanístico, arquitectónico, histórico, cultural, natural o paisajístico.", "Libros, catálogos y manuales" },
                    { 42, "LIPR", "Resoluciones de la Administración por la que se autoriza una determinada actividad relacionada con el desarrollo inmobiliario y gestión de un activo.Dentro de cada TDN2 se encuentran embebidos: solicitudes, requerimientos, subsanaciones, notificaciones, entre otros.", "Licencias y permisos" },
                    { 43, "MEMO", "Documentos en que se rinden cuentas de una actividad realizada durante cierto tiempo, casi siempre para justificar su aprovechamiento. Por lo general consta de tres partes: resumen de lo que se ha venido haciendo, crítica de lo que se ha hecho y propuesta de mejora para el futuro.", "Memorias" },
                    { 44, "NORM", "Contenido de un texto jurídico, sea éste de rango constitucional, legal o reglamentario y, en general, de cualquier disposición de la autoridad con eficacia jurídica que genere obligaciones y derechos.", "Normas" },
                    { 45, "NOTS", "Extractos sucintos del contenido de los asientos registrales relativos a la finca, donde conste la identificación de la misma, la identidad del titular o titulares de los derechos inscritos sobre la misma, y la extensión, naturaleza y limitaciones de éstos. Su valor es puramente informativo.", "Notas simples" },
                    { 46, "NOVA", "Documentos públicos o privados, en los que se hace constar un determinado acontecimientopor la que se modifica alguna de las condiciones u obligaciones de un contrato de financiación.", "Novaciones financieras" },
                    { 47, "OFER", "Propuestas económicas presentadas para alcanzar un acuerdo mercantil.Dentro de cada TDN2 se encuentran embebidos: las propuestas vinculantes y las no vinculantes.", "Ofertas" },
                    { 48, "PBLO", "Medios de comunicación escritos (boletines, diarios oficiales y/o tablón de anuncios) que una Administración o Entidad Pública utiliza para publicar sus normas jurídicas y otros actos de naturaleza pública.", "Publicaciones oficiales" },
                    { 49, "PLAO", "Representaciones gráficas esquemáticas, en dos dimensiones y a determinada escala, de un terreno, ámbito, población,etc.", "Planos" },
                    { 50, "PLAP", "Documentos en los que se detalla el modo y conjunto de medios necesarios para llevar a cabo un negocio, proyecto, acción o planificación económica y de gestión.", "Planes, planificaciones, proyectos y directrices de actuación" },
                    { 51, "PLAT", "Documentos elaborados por técnicos para ordenar el suelo o la previsión y ejecución de sus respectivas obras.Dentro de cada TDN2 se encuentran embebidos las variantes de cada documento en cuanto a modificaciones, revisiones, etc.", "Planes técnicos" },
                    { 52, "PRES", "Cómputo anticipado del coste de una obra o de los gastos y rentas de una corporación.", "Presupuestos" },
                    { 53, "PRPE", "Idea, manifestación o pregunta realizada por persona/ órgano distinto a Sareb por la que se pide al interesado que exprese y declare su actitud o su respuesta.En caso de que se adjunte documentación para su análisis por el receptor, ésta se clasificará en el tipo documental y unidad documental que proceda.", "Propuestas / solicitudes / requerimientos órganos externos" },
                    { 54, "PRPI", "Idea, manifestación o pregunta realizada por persona/ órgano de Sareb por la que se pide al interesado que exprese y declare su actitud o su respuesta.En caso de que se adjunte documentación para su análisis por el receptor, ésta se clasificará en el tipo documental y unidad documental que proceda.", "Propuestas/ solicitudes/ requerimientos internos Sareb" },
                    { 55, "PRYT", "Conjunto de escritos, cálculos y dibujos que se hacen para dar idea de cómo ha de ser y lo que ha de costar una obra de arquitectura o de ingeniería.", "Proyectos técnicos" },
                    { 56, "PUBM", "Divulgación en los medios de comunicación, de los distintos proyectos, planes y actuaciones urbanísticas, así como de las noticias, artículos, anuncios, etc aparecidos en los medios de comunicación que estén relacionadas con un activo.", "Publicaciones en medios" },
                    { 57, "RETR", "Salarios o sueldos que se pagan al trabajador en dinero o en especie por el empresario privado o público, dependiendo de lo establecido contractualmente y dentro de las exigencias legales que el derecho laboral del país marque. También se entienden como retribuciones, las cantidades recibidas con concepto de prestaciones por despempleo o jubilación.", "Retribuciones (nominas, pensiones e indemnizaciones por desempleo)" },
                    { 58, "SEGU", "Acuerdos por los cuales una de las partes (el asegurador), se obliga a resarcir de un daño o a pagar una suma de dinero a la otra parte (tomador), al verificarse la eventualidad prevista en el contrato, a cambio del pago de un precio, denominado prima.Dentro de cada TDN2 se encuentran embebidos: las altas, bajas, modificaciones etc.", "Seguros" },
                    { 59, "SERE", "Actos procesales provenientes de un tribunal o juzgado, mediante el cual resuelve las peticiones de las partes, o autoriza u ordena el cumplimiento de determinadas medidas.", "Sentencias y resoluciones judiciales" },
                    { 60, "TASA", "Documentos de estimación del valor de un activo.", "Tasaciones y Valoraciones" }
                });

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 21, 13, 56, 15, 210, DateTimeKind.Utc).AddTicks(3170), new DateTime(2026, 5, 21, 13, 56, 15, 210, DateTimeKind.Utc).AddTicks(3161) });

            migrationBuilder.InsertData(
                table: "CatalogoTdn2",
                columns: new[] { "Id", "Codigo", "CodigoTdn1", "Descripcion", "Nombre", "Tdn1Id" },
                values: new object[,]
                {
                    { 1, "ACTE-01", "ACTE", "Acuerdo alcanzado entre las partes sobre el valor indemnizatorio de los bienes y derechos expropiados", "Expropiación: acta de mutuo acuerdo", 1 },
                    { 2, "ACTE-02", "ACTE", "Título bastante para que en los diferentes Registros Públicos se inscriba o tome razón de la transmisión de dominio del bien y se verifique, en su caso, la cancelación de las cargas, gravámenes y demás derechos reales sobre el inmueble expropiado.", "Expropiación: acta de ocupación", 1 },
                    { 3, "ACTE-03", "ACTE", "Documento en el que se debe dejar constancia, por ambas partes, de las características de los bienes y derechos afectados en la expropiación, así como los perjuicios por rápida ocupación.", "Expropiación: acta previa a la ocupación", 1 },
                    { 4, "ACTE-04", "ACTE", "Documento privado en el que   se manifiesta y se deja constancia de  la ausencia de indicios de ocupación del inmueble.", "Acta de manifestación posesión", 1 },
                    { 5, "ACTR-01", "ACTR", null, "Asamblea y/o consejo: acta", 2 },
                    { 6, "ACTR-02", "ACTR", "Certificación en que consta el resultado de la elección de una persona o varias  para el desempeño de un cargo o función, públicos o privados dentro de una unidad de actuación, urbanización, complejo inmobiliario o edificio.unidad para realizar todas las gestiones y trabajos encaminados a la gestión y conservación del inmueble del que son propietarios.", "Constitución: acta", 2 },
                    { 7, "ACTR-03", "ACTR", "Documento en el que los propietarios de terrenos sometidos a un proceso de transformación de suelo por sistema de compensación se constituyen como una unidad para realizar todas las gestiones y trabajos encaminados al desarrollo urbanístico del Sector hasta la constitución de la Junta de Compensación o Agrupación de interés urbanístico.", "Constitución: acta comisión gestora", 2 },
                    { 8, "ACTR-04", "ACTR", "Relación escrita de lo sucedido, tratado o acordado en una junta de acreedores en procedimiento concursal.", "Convenio acreedores: acta junta", 2 },
                    { 9, "ACTR-05", "ACTR", "Resumen de lo acaecido en reuniones específicas sobre seguridad y salud, en la que los agentes intervinientes durante la ejecución de las obras, exponen, debaten y acuerdan acciones relacionadas con el estado de seguridad de la obra.", "Seguridad y salud: acta reunión", 2 },
                    { 10, "ACTR-06", "ACTR", "Documento en el que se registra el acuerdo de los socios o partícipes referente a la disolución de la entidad jurídica correspondiente.", "Disolución: acuerdo", 2 },
                    { 11, "ACTR-10", "ACTR", "Documento en el que se especifican los temas tratados y los acuerdos alcanzados en las reuniones mantenidas con organismos e instituciones externas.", "Organismos e instituciones externas: acta", 2 },
                    { 12, "ACTT-01", "ACTT", "Documento por el se aprueban las obras de reparación/mantenimiento realizadas.", "Acta conformidad", 3 },
                    { 13, "ACTT-02", "ACTT", "Documento, favorable o desfavorable, firmado por el técnico competente en el que tras la inspección pertinente realizada al inmueble, en función de su catalogación o edad, acredita la existencia o no de deficiencias y, llegado el caso, las medidas recomendadas para acometer las actuaciones necesarias para su subsanación.", "Acta Inspección Técnica de Edificios (ITE)", 3 },
                    { 14, "ACTT-03", "ACTT", "Documento que acredita la aprobación por parte de la dirección facultativa el plan de residuos, el cual es, así mismo, aceptado por el promotor y firmado como enterado por el constructor y pasará a formar parte de los documentos contractuales de la obra.", "Gestión de residuos: acta aprobación plan", 3 },
                    { 15, "ACTT-04", "ACTT", "Documento, favorable o desfavorable, firmado por el técnico competente en el que tras la inspección pertinente realizada al inmueble, en función de su catalogación o edad, acredita la existencia o no de deficiencias y, llegado el caso, las medidas recomendadas para acometer las actuaciones necesarias para su subsanación.", "Inspección Técnica de Edificios (ITE): acta", 3 },
                    { 16, "ACTT-05", "ACTT", "Documento que acredita el depósito del libro del edificio, por parte del promotor y una vez terminada la obra, en la notaría junto con la certificación del arquitecto director de la obra que acredita que es el libro correspondiente a la obra en cuestión .", "Libro del edificio: acta depósito", 3 },
                    { 17, "ACTT-06", "ACTT", "Certificación, necesaria para la autorización por parte del Notario de la declaración de Obra Nueva, emitida por la dirección facultativa acreditativa del ajuste de la descripción de la obra nueva reflejada en la escritura al proyecto básico y/o de ejecución redactado.", "Obra: acta declaración de obra nueva", 3 },
                    { 18, "ACTT-07", "ACTT", "Certificado mediante el cual el promotor y el coordinador de seguridad y salud acuerdan la finalización de las tareas de coordinación en materia de seguridad y salud durante la ejecución de la obra cesando, a partir de la fecha de la certificación, las responsabilidades derivadas de la coordinación.", "Obra: acta finalización coordinación", 3 },
                    { 19, "ACTT-08", "ACTT", "Documento por el que el promotor nombra a un director de obra y a un director de ejecución material de la obra y que, posteriormente, es comunicado a cada colegio profesional y a la Administración Pública competente.", "Obra: acta nombramiento dirección facultativa", 3 },
                    { 20, "ACTT-09", "ACTT", "Documento por el que el promotor indica la decisión de paralización de las obras de ejecución y consiguiente suspensión de la coordinación y dirección de los distintos técnicos implicados. Dicha acta habitualmente es firmada por el promotor, el constructor, el director de obra, el director de ejecución de obra y el coordinador de seguridad y salud y posteriormente se deja constancia en los libros de órdenes y de incicencias de la obra, se comunica a las autoridades competentes (laborales y municipales) y a los colegios profesionales y se procede al cierre del centro de trabajo.", "Obra: acta paralización", 3 },
                    { 21, "ACTT-10", "ACTT", "Documento que acredita la recepción por parte del ayuntamiento y/o promotor, de las obras por haberse ejecutado conforme al proyecto presentado. En el caso de las obras de urbanización, supone la entrega de los derechos y obligaciones de su gestión y/o conservación.", "Obra: acta recepción definitiva", 3 },
                    { 22, "ACTT-11", "ACTT", "Documento que acredita por parte del ayuntamiento y/o promotor de las obras la recepción de fases de la obra terminadas (recepción parcial) sujeta a posterior recepción definitiva (provisional) o recepción con determinadas deficiencias pendientes de subsanar (con reservas)", "Obra: acta recepción parcial, provisional o con reserva", 3 },
                    { 23, "ACTT-12", "ACTT", "Documento que acredita el rechazo expreso a la recepción de las obras por considerarse que no está terminada o no se adecua a las condiciones contractuales.", "Obra: acta rechazo", 3 },
                    { 24, "ACTT-13", "ACTT", "Documento por el que el promotor indica la decisión de reinicio de las obras de ejecución y consiguiente reinicio de la coordinación y dirección de los distintos técnicos implicados. Dicha acta habitualmente es firmada por el promotor, el constructor, el director de obra, el director de ejecución de obra y el coordinador de seguridad y salud y posteriormente se deja constancia en los libros de órdenes y de incicencias de la obra, se comunica a las autoridades competentes (laborales y municipales) y a los colegios profesionales y se procede a la apertura del centro de trabajo.", "Obra: acta reinicio", 3 },
                    { 25, "ACTT-14", "ACTT", "Documento firmado por el Constructor, el Director de Obra, Los Directores de Ejecución de Obra, el Coordinador de Seguridad y Salud y el Promotor en el que, entre otros, por una parte se manifiesta que el constructor ha realizado el replanteo del perímetro de la edificación proyectada, el cual, una vez comprobado por el Director de Ejecución de Obra, resulta ajustado a las características del sola, y por otra, que la Dirección Facultativa, de acuerdo con el Promotor, autoriza el inmediato comienzo de los trabajos.", "Obra: acta replanteo e inicio", 3 },
                    { 26, "ACTT-15", "ACTT", "Documento que manifiesta los acuerdos que se van tomando en las reuniones celebradas durante la ejecución de las obras.", "Obra: acta seguimiento", 3 },
                    { 27, "ACTT-16", "ACTT", "Documento que acredita la subsanación de los defectos que hubieran quedado denunciados y consignados en el acta de rechazo o de recepción parcial o con reserva de las obras.", "Obra: acta subsanación defectos", 3 },
                    { 28, "ACTT-17", "ACTT", "Acta a través del cual la compañía suministradora da la conformidad técnida, de acuerdo a lo proyectado y loss estándares definidos por la compañía, respecto a los trabajos e instalaciones realizados y recepciona las instalaciones ejecutadas.", "Obra: actas de conformidad y recepción compañías suministradoras", 3 },
                    { 29, "ACTT-18", "ACTT", "Documento, firmado por el coordinador de seguridad y salud, a través del cual se procede a la aprobación del plan aplicable a la obra", "Seguridad y salud: acta aprobación plan", 3 },
                    { 30, "ACTT-19", "ACTT", "Documento en el que el coordinador de seguridad y salud durante la fase de ejecución, certifica a la propiedad de las obras, la finalización de su actuación.", "Seguridad y salud: acta final de obra", 3 },
                    { 31, "ACTT-20", "ACTT", "Documento por el que el promotor nombra a un coordinador de seguridad y salud para gestionar temas de prevención de riesgos laborales durante la ejecución de la obra.", "Seguridad y salud: acta nombramiento coordinador", 3 },
                    { 32, "ACTT-21", "ACTT", "Documento que manifiesta lo sucedido en la visita del coordinador de seguridad y salud en su visita a la obra", "Seguridad y salud: acta visita coordinador", 3 },
                    { 33, "ACUE-01", "ACUE", "Resolución de aprobación o denegación de una operación concreta por parte de una cedente o un servicer. *Para aprobaciones o denegaciones de operaciones por Sareb use: OP-03-ACUI-02", "Aprobación / denegación operación financiera por órgano externo", 4 },
                    { 34, "ACUE-02", "ACUE", "Comunicación por parte de la CNMV de la autorización para operar como un FAB.", "Constitución FAB: autorización de la CNMV", 4 },
                    { 35, "ACUE-03", "ACUE", "Autorización por parte del comité correspondiente para adherirse al convenio de acreedores propuesto.", "Convenio acreedores: aprobación adhesión", 4 },
                    { 36, "ACUE-04", "ACUE", "Documento por el que una agrupación de propietarios, a la vista de las cuentas aprobadas por la junta, y tras comprobar que un propietario ha dejado de abonar los recibos, aprueba la liquidación de la deuda, facultando al presidente para que, en el caso de que no se ponga la cantidad adeudada a disposición de la comunidad, proceda judicialmente.", "Derramas y cuotas: acta liquidación deuda", 4 },
                    { 37, "ACUE-05", "ACUE", "Documento por el que una agrupación de propietarios, concede al propietario un aplazamiento del abono de las cuotas al que viene obligado.", "Derramas y cuotas: autorización aplazamiento pago", 4 },
                    { 38, "ACUE-06", "ACUE", "Autorización por parte del comité correspondiente de la actuación a seguir en la subasta.", "Subasta: aprobación estrategia", 4 },
                    { 39, "ACUE-07", "ACUE", "Conjuto de documentos surgidos de la solicitud, ante la administración competente, de la autorización de venta de una vivienda sujeta a protección pública. En este tipo documental se incluirán la solicitud, el pago de las tasas, el certificado emitido por la administración,..", "Vivienda de protección: autorización venta", 4 },
                    { 40, "ACUE-08", "ACUE", "Documento en el que consta la voluntad de las partes para extinguir el derecho de prenda", "Acuerdo despignoración", 4 },
                    { 41, "ACUE-09", "ACUE", "Documento por el cual el acreditado autoriza la realización de un determinado acto o negocio.", "Autorización firmada por el acreditado", 4 },
                    { 42, "ACUE-10", "ACUE", "Documento en el que consta la voluntad de las partes para utilizar bienes de su propiedad como garantía prendaria.", "Autorización firmada titulares garantías prendarias", 4 },
                    { 43, "ACUE-11", "ACUE", "Documento firmado por el propietario del derecho, a través del cual, autoriza a un tercero que represente sus intereses en un asamblea y/o consejo.", "Asamblea y/o consejo: Autorización representación y otros", 4 },
                    { 44, "ACUE-12", "ACUE", "Documento en el que se detalla el acuerdo alcanzado respecto a la forma y plazos de pago de cantidades pendientes acordado entre el arredatario y el arrendador", "Alquiler: plan de pagos", 4 },
                    { 45, "ACUE-13", "ACUE", "Documento por el cual los acreedores dan su conformidad y se adhieren a la propuesta de convenio que se hubiera formulado en la fase de convenio de acreedores", "Convenio acreedores: adhesion", 4 },
                    { 46, "ACUE-14", "ACUE", "Acuerdo por el cual  las partes resuelven sobre el hecho litigioso y evitan/ponen fin al proceso judicial", "Ejecucion: Acuerdo transaccional", 4 },
                    { 47, "ACUE-15", "ACUE", "Documento en el que las partes pactan hacer efectivo el derecho del acreedor a enajenar el bien hipotecado y obtener la satisfacción de su crédito ante notario", "Ejecución: Acuerdo de venta extrajudicial del bien hipotecado", 4 },
                    { 48, "ACUI-01", "ACUI", "Autorización de SAREB del departamento de Refinanciación y Reestructuración a la disposición total o parcial de una financiación concedida.", "Acuerdo Sareb de R&R", 5 },
                    { 49, "ACUI-02", "ACUI", "Resolución de aprobación o denegación de una operación concreta por parte de Sareb. *Para las aprobaciones o denegaciones realizadas por el servicer o la cedente utilice ACUE-01", "Aprobación / denegación operación por Sareb", 5 },
                    { 50, "ACUI-03", "ACUI", "Autorización/ rechazo por parte del comité correspondiente a la firma de un contrato", "Autorización firma contrato", 5 },
                    { 51, "ACUI-04", "ACUI", "Autorización/ rechazo por parte del comité correspondiente a la firma de una Escritura", "Autorización firma escritura", 5 },
                    { 52, "ACUI-05", "ACUI", "Autorización o denegación de pago de las cuotas o derramas giradas por una entidad de propietarios", "Derramas y cuotas: autorización / denegación gastos abono", 5 },
                    { 53, "ACUI-06", "ACUI", "Documento originado por el departamento de Prevención de blanqueo de capitales en el que se manifiesta la conformidad con la realización de una operación.", "PBC: acuerdo aprobación operación", 5 },
                    { 54, "ACUI-07", "ACUI", "Documento por el cual se da respuesta por parte de Sareb a cualquier consulta ya sea para toma de decisiones, planteamiento de operaciones, tributación…...", "07-Respuestas/decisiones Sareb", 5 },
                    { 55, "ALEG-01", "ALEG", "Incidente concursal, interpuesto por interesado en el proceso, para la impugnación total o parcial del Inventario o Lista de Acreedores", "Administración concursal: impugnación lista bienes y obligaciones", 6 },
                    { 56, "ALEG-02", "ALEG", "Escritos de alegaciones o recursos presentados contra los acuerdos de la Asamblea y/o Consejo", "Asamblea y/o consejo: alegaciones/recursos", 6 },
                    { 57, "ALEG-03", "ALEG", "Escritos de alegaciones o recursos presentados contra la aporbación de los Textos de Bases y Estatutos", "Bases y Estatutos: alegaciones/recursos", 6 },
                    { 58, "ALEG-04", "ALEG", "Escrito de alegación presentado ante la administracion actuante contra la aprobación del Catalogo de Bienes y Espacios Protegidos", "Catálogo de bienes y espacios protegidos: alegación", 6 },
                    { 59, "ALEG-05", "ALEG", "Escritos de alegaciones o recursos presentados contra la aprobacción de la constitución de una entidad", "Constitución: alegaciones/recursos", 6 },
                    { 60, "ALEG-06", "ALEG", "Escrito de alegaciones por el que se manifiesta (debidamente argumentado) la disconformidad al convenio de acreedores.", "Convenio acreedores: impugnación texto definitivo", 6 },
                    { 61, "ALEG-07", "ALEG", "Escrito de acreedores oponiéndose a la aprobación del Convenio de Acreedores", "Convenio acreedores: oposición a la aprobación", 6 },
                    { 62, "ALEG-08", "ALEG", "Escrito de alegación presentado ante la administracion actuante contra la aprobacioin del Estudios de Detalle", "Estudio de detalle: alegación", 6 },
                    { 63, "ALEG-09", "ALEG", "Escrito de alegación presentado ante la administracion actuante manifestando el rechazo a la hoja de aprecio con indicación de hechos y derechos", "Expropiación: alegación hoja de aprecio", 6 },
                    { 64, "ALEG-10", "ALEG", "Impugnación del Balance de Liquidación presentados por los acreedores del FAB", "Extinción FAB: alegación estados financieros de liquidación", 6 },
                    { 65, "ALEG-11", "ALEG", "Posibilidad si así lo establece la escritura de constitución del FAB de presentar alegaciones por los acreedores del fondo al proyecto de fusión/escisión propuesto.", "Fusión / Escisión FAB: derecho oposición", 6 },
                    { 66, "ALEG-12", "ALEG", "Alegación realizada por parte de los acreedores al plan de liquidación propuesto.", "Liquidación concurso: observación al plan", 6 },
                    { 67, "ALEG-13", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad a las normas complementarias aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Normas complementarias: alegación", 6 },
                    { 68, "ALEG-14", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad a las normas subsidiarias aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Normas subsidiarias: alegación", 6 },
                    { 69, "ALEG-15", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Proyecto de Delimitación del Suelo Urbano aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Proyecto de delimitación de suelo urbano: alegación", 6 },
                    { 70, "ALEG-16", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan de Sectorización / Delimitación aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan de sectorización/delimitación: alegación", 6 },
                    { 71, "ALEG-17", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan de singular interés aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan de singular interés: alegación", 6 },
                    { 72, "ALEG-18", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan reforma interior aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan especial de reforma interior: alegación", 6 },
                    { 73, "ALEG-19", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan especial aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan especial: alegación", 6 },
                    { 74, "ALEG-20", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan general de ordenación aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan general de ordenación territorial: alegación", 6 },
                    { 75, "ALEG-21", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan general aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan general: alegación", 6 },
                    { 76, "ALEG-22", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al Plan parcial aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Plan parcial: alegación", 6 },
                    { 77, "ALEG-23", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al programa de actuación aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Programa de actuación: alegación", 6 },
                    { 78, "ALEG-24", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al proyecto de actuación especial aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Proyecto actuación especial: alegación", 6 },
                    { 79, "ALEG-25", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al proyecto de calificación urbanística aprobado inicialmente en defensa de los intereses del activo.", "Proyecto calificación urbanística: alegación", 6 },
                    { 80, "ALEG-26", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al proyecto de equidistribución aprobado inicialmente en defensa de los intereses del activo.", "Proyecto equidistribución: alegación", 6 },
                    { 81, "ALEG-27", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al proyecto de expropiación aprobado inicial o provisionalmente en defensa de los intereses del activo.", "Proyecto expropiación: alegación", 6 },
                    { 82, "ALEG-28", "ALEG", "Escrito de alegación presentado ante la administracion actuante en el que se manifiesta (debidamente argumentado) la disconformidad al proyecto de urbanización aprobado inicialmente en defensa de los intereses del activo.", "Proyecto urbanización: alegación", 6 },
                    { 83, "ALEG-29", "ALEG", "Conjunto de documentos originados como consecuencia de la presentación ante la administración actuante de un escrito, a través del cual se exponen las razones sobre las que se basa la oposición total o parcial contra un acto administrativo. Dentro de este tipo documental únicamente se incluirán aquellas alegaciones que no tenga un tipo documental propio mas específico.", "Alegación", 6 },
                    { 84, "ALEG-31", "ALEG", "Escrito que puede presentar el concursado que no haya presentado propuesta de convenio, oponiéndose al mismo (articulo 128.3 LC)", "Convenio: oposición propuesta anticipada de convenio", 6 },
                    { 85, "ALEG-32", "ALEG", "Documento emitido por las partes realizando manifestaciones que rebaten el criterio indicado por perito en el procedimiento de ejecución", "Ejecución: Alegaciones e informes de partes y titulares registrales para contradecir informe del perito", 6 },
                    { 86, "ANEX-01", "ANEX", "Documento en el que se detalla el fin ultimo al que se va a dedicar la operación solicitada.", "Ayuda y/o subvención: anexo finalidad operación financiera", 7 },
                    { 87, "ANEX-02", "ANEX", "Documento denominado \"Anexo 0\" y recoge la información básica del deudor y de la operación solicitada al ICO.", "Ayuda y/o subvención: anexo información básica del deudor y de la operación solicitada al ICO", 7 },
                    { 88, "ANEX-03", "ANEX", "Documento en el que se detallan las ayudas publicas obtenidas por un deudor.", "Ayuda y/o subvención: anexo mínimos", 7 },
                    { 89, "ANEX-04", "ANEX", "Relación de documentación anexa al contrato.", "Contrato: anexo contrato", 7 },
                    { 90, "ANEX-05", "ANEX", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del plan.", "Plan general de ordenación territorial: anexo", 7 },
                    { 91, "ANEX-06", "ANEX", "Documentación adicional incorporada a la tasación no comprendida ni en el informe ni en el certificado.", "Tasación: anexo", 7 },
                    { 92, "AUVI-03", "AUVI", "Documento privado en el que   se manifiesta y se deja constancia de  la ausencia de indicios de ocupación del inmueble", "Grabación estado inmueble, toma posesión", 8 },
                    { 93, "AYSU-01", "AYSU", "Documento mediante el que se justifica la cantidad de dinero que se concede a una persona, una entidad o una institución como ayuda económica para realizar una actividad, especialmente la que se recibe del Estado o de un organismo oficial.", "Subvención recibida", 9 },
                    { 94, "CEDU-01", "CEDU", "Documentos o certificados administrativos municipales acreditativos de las circunstancias urbanísticas que concurran en las fincas, solares o parcelas. Pueden encontrarse embebidos: solicitudes, denegaciones, escritos observaciones, etc\". Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"07 - Garantías\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-...)", "Cédula urbanística", 10 },
                    { 95, "CERA-01", "CERA", "Movimiento de amortización total o parcial sobre el cuadro de amortización.", "Amortización cuenta préstamo", 11 },
                    { 96, "CERA-02", "CERA", "Documento de Justificación de pago de las arras penitenciales o confirmatorias (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Arras: justificante abono", 11 },
                    { 97, "CERA-03", "CERA", "Modelo de Liquidación de impuesto, tasa o contribución en periodo voluntario.", "Autoliquidación", 11 },
                    { 98, "CERA-04", "CERA", "Documento a través del cual se deposita ante el organismo público correspondiente el aval o fianza (p.e. depósito de la fianza de acuerdo a lo establecido en el artículo 36 de la  L.A.U.,  depósito de los avales por la ejecución de obras,…)", "Aval / fianza: justificante depósito", 11 },
                    { 99, "CERA-05", "CERA", "Documento emitido por el acreedor y en el que certifica que el deudor le ha satisfecho totalmente la deuda.", "Carta pago", 11 },
                    { 100, "CERA-06", "CERA", "Justificación del pago de las correspondientes cuotas exigidas para la pertenencia a un colegio profesional determinado.", "Colegio profesional: recibo", 11 },
                    { 101, "CERA-07", "CERA", "Documentación relativa a la contribución especial, es decir, el tributo cuyo hecho imponible consiste en la obtención por el obligado tributario de un beneficio o de un aumento de valor de sus bienes como consecuencia de la realización de obras públicas o del establecimiento o ampliación de servicios públicos.", "Contribución especial", 11 },
                    { 102, "CERA-08", "CERA", "Justificante de abono emitido por la entidad al partícipe como consecuencia del reparto de los costes asumidos por la entidad y girados al participe en el porcentaje que le corresponde.  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Derramas y cuotas: justificante de abono", 11 },
                    { 103, "CERA-09", "CERA", "Justificante de haber dispuesto de la financiación concedida.", "Documento desembolso disposición de crédito", 11 },
                    { 104, "CERA-10", "CERA", "Justificante de abono del justiprecio establecido en una expropiación  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Expropiación: justificante abono justiprecio", 11 },
                    { 105, "CERA-11", "CERA", "Recibo de Préstamo Ordinario o extraordinario (pago retrasado)", "Extracto préstamo donde figure la IPF aplicada o justificante gasto", 11 },
                    { 106, "CERA-12", "CERA", "Carta de Pago del Impuesto Municipal sobre Bienes Inmuebles (IBI) (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad) Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Impuesto sobre bienes inmuebles (IBI): justificante abono", 11 },
                    { 107, "CERA-13", "CERA", "Carta de Pago del Impuesto de Sucesiones (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Impuesto de sucesiones: justificante abono", 11 },
                    { 108, "CERA-14", "CERA", "Conjunto de documentación relativa al Impuesto de Trasmisiones Patrimoniales y/o Actos Jurídicos Documentados.", "Impuesto de transmisiones patrimoniales (ITP) y Actos Jurídicos Documentados (AJD)", 11 },
                    { 109, "CERA-15", "CERA", "Conjunto de documentación relativa al Impuesto del Valor Terrenos de Naturaleza Urbana (Plusvalía)", "Impuesto Incremento Valor Terrenos Naturaleza Urbana (plusvalía)", 11 },
                    { 110, "CERA-16", "CERA", "Documento justificativo del pago del impuesto de bienes inmuebles emitido por la administración compentente", "Impuesto sobre bienes inmuebles (IBI): recibo", 11 },
                    { 111, "CERA-17", "CERA", "Conjunto de documentación relativa al Impuesto Municipal sobre Construcciones Instalaciones y Obras (ICIO)", "Impuesto sobre construcciones, instalaciones y obras (ICIO)", 11 },
                    { 112, "CERA-18", "CERA", "Documento justificativo de haber recibido alguna cantidad compensatoria por un daño recibido.", "Indemnización: cobro", 11 },
                    { 113, "CERA-19", "CERA", "Documento Justificativo de habar realizado el abono de un concepto no contemplado en otros apartados de la clasificación  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Justificante abono", 11 },
                    { 114, "CERA-20", "CERA", "Documento Justificativo de haber recibido el cantidades como consecuencia de las actuaciones realizadas durante la  liquidación de una sociedad deudora/garante/... (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Liquidación concurso: justificante abono", 11 },
                    { 115, "CERA-21", "CERA", "Certificado extendido por Organismo Tributario que certifica que se esta al corriente con las obligaciones Tributarias.", "Obligaciones tributarias: certificado estar al corriente de pago", 11 },
                    { 116, "CERA-22", "CERA", "Carta de Pago de un Impuesto, tasa o contribución no incluidos en otros apartados de la clasificación.", "Otro impuesto / tasa / contribución", 11 },
                    { 117, "CERA-23", "CERA", "Carta de Pago del Impuesto de transmisiones patrimoniales (ITP) y Actos Jurídicos Documentados (AJD) en el préstamo originario  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Prestamo originario: Justificante abono Impuesto de transmisiones patrimoniales (ITP) y Actos Jurídicos Documentados (AJD)", 11 },
                    { 118, "CERA-24", "CERA", "Carta pago de la Tasa de Tramitación del programa de actuación  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Programa de actuación: justificante abono tasa tramitación", 11 },
                    { 119, "CERA-25", "CERA", "Carta pago de la Tasa de Tramitación del proyecto de actuación especial  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto actuación especial: justificante abono tasa tramitación", 11 },
                    { 120, "CERA-26", "CERA", "Carta pago de la Tasa de Tramitación del proyecto de calificacción urbanística  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto calificación urbanística: justificante abono tasa tramitación", 11 },
                    { 121, "CERA-27", "CERA", "Carta pago de la Tasa de Tramitación del proyecto de ejecución  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto ejecución: justificación abono tasa tramitación", 11 },
                    { 122, "CERA-28", "CERA", "Carta pago de la Tasa de Tramitación del proyecto de equidistribución (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto equidistribución: justificante abono tasa tramitación", 11 },
                    { 123, "CERA-29", "CERA", "Carta pago de la Tasa de Tramitación del proyecto de parcelación / segregación / agrupación (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto parcelación / segregación / agrupación: justificante abono tasa tramitación", 11 },
                    { 124, "CERA-30", "CERA", "Carta pago de la Tasa de Tramitación del proyectto de urbanización  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Proyecto urbanización: justificante abono tasa tramitación", 11 },
                    { 125, "CERA-31", "CERA", "Documento justificativo del pago de la cuota de permanencia en el Régimen de Autónomos.", "Régimen especial de autónomos (RETA): recibo", 11 },
                    { 126, "CERA-32", "CERA", "Documento Justificativo de haber abonado la reserva   (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Reserva: justificante abono", 11 },
                    { 127, "CERA-33", "CERA", "Certificado extendido por la Seguridad Social que certifica que un trabajador/empresa está al corriente con las obligaciones Tributarias.", "Seguridad social: certificado estar al corriente de pagos", 11 },
                    { 128, "CERA-34", "CERA", "Justificante Pago Recibo Seguro Multirriesgo/Hogar   (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro multirriesgo: justificante abono", 11 },
                    { 129, "CERA-35", "CERA", "Justificante Pago Seguro Todo Riesgo Construcción  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro de construcción (TRC): justificante abono", 11 },
                    { 130, "CERA-36", "CERA", "Justificante Pago Recibo Seguro Multirriesgo/Hogar (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro multirriesgo: justificante abono", 11 },
                    { 131, "CERA-37", "CERA", "Justificante Pago Recibo Seguro Paralización de Obra  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro obra paralizada: justificante abono", 11 },
                    { 132, "CERA-38", "CERA", "Justificante Pago Seguro (No contemplado en otras Clasificaciones)  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro prima: justificante abono", 11 },
                    { 133, "CERA-39", "CERA", "Justificante Pago Recibo Seguro Responsabilidad Civil  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro responsabilidad civil: justificante abono", 11 },
                    { 134, "CERA-40", "CERA", "Justificante Pago Recibo Seguro Decenal  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro responsabilidad decenal: justificante abono", 11 },
                    { 135, "CERA-41", "CERA", "Justificante Pago Seguro Responsabilidad Medioambiental  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro responsabilidad medioambiental: justificante abono", 11 },
                    { 136, "CERA-42", "CERA", "Documento Justificativo de haber abonado el importe por el que se ha adjudicado el bien en la subasta   (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Subasta: justificante abono", 11 },
                    { 137, "CERA-43", "CERA", "Carta pago de la tasa asociada a una Licencia y/o permiso (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Tasa licencia y/o permiso: justificante abono", 11 },
                    { 138, "CERA-44", "CERA", "Conjunto de documentación relativa a la tasa municipal de recogida de basura, Vado, o cualquier otra no contemplada en la presente clasificación.", "Tasa municipal basura, vado, etc", 11 },
                    { 139, "CERA-45", "CERA", "Documento Justificativo de haber abonado el importe de la transacción pactada  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Transacción: justificante abono", 11 },
                    { 140, "CERA-46", "CERA", "Documento justificativo del pago del impuesto de actividades económicas emitido por la administración compentente", "Impuesto actividades económicas (IAE): recibo", 11 },
                    { 141, "CERA-48", "CERA", "Documento de justificación de pago de una deuda (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Pago deuda: justificante abono", 11 },
                    { 142, "CERA-50", "CERA", "Documento acreditativo de la utilización o uso de una cantidad determinada de dinero", "Justificante destino de los fondos", 11 },
                    { 143, "CERA-51", "CERA", "Justificante de pago del recibo del seguro de percepción de rentas  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Seguro percepción de rentas: justificantes abono", 11 },
                    { 144, "CERA-52", "CERA", "Justificante del ingreso del importe del depósito marcado por contrato  (este tipo documental incorpora también la devolución total o parcial de las cantidades abonadas con anterioridad)", "Depósito justificante abono", 11 },
                    { 145, "CERA-53", "CERA", "Documento que acredita el pago de una cantidad estipulada a favor del arrendador por parte del arrendatario.", "Alquiler: justificante pago", 11 },
                    { 146, "CERA-54", "CERA", "Carta de pago del Impuesto de Transmisiones Patrimoniales (Modelo 600 - ITP) realizado por el arrendatario como consecuencia de la liquidación del impuesto correspondiente al contrato de arrendamiento firmado.", "Alquiler: justificante pago Impuesto Transmisiones Patrimoniales (ITP)", 11 },
                    { 147, "CERA-55", "CERA", "Documento acreditativo de la realización de algún acto", "Justificante", 11 },
                    { 148, "CERA-56", "CERA", "Certificado extendido por la Seguridad Social o Inem que certifica que una persona recibe o no recibe prestaciones o su situación laboral", "Seguridad social: certificado recepción prestación/situacion Laboral", 11 },
                    { 149, "CERA-57", "CERA", "Documento que acredita el reconocimiento de la calificación del grado de discapacidad en la Comunidad de Madrid", "Certificado de Incapacidad", 11 },
                    { 150, "CERA-58", "CERA", "Documento que justifica la recuperación de un aval o fianza depositado en un organismo público u otra entidad", "Aval-fianza: recuperación", 11 },
                    { 151, "CERA-59", "CERA", "Documento justificativo de haber realizado el abono de un concepto con pago posterior", "Justificante abono pago aplazado", 11 },
                    { 152, "CERJ-01", "CERJ", "Documento privado emitido por el servicer en el que se acredita la comprobación de que las facultades o poderes de una o varias personas físicas son suficientes para actuar en nombre y representación de una determinada persona jurídica en la realización de determinadas actuaciones mercantiles y económicas.", "Bastanteo poderes", 12 },
                    { 153, "CERJ-02", "CERJ", "Certificado de titularidad de un activo concreto emitido por Registro Bienes Muebles.", "Bien mobiliario: certificación titularidad y libertad cargas", 12 },
                    { 154, "CERJ-03", "CERJ", "Certificado que acredita la inexistencia de una relación entre una entidad y activo. *Para los informes de vinculación utilice CERJ-59", "Carta no vinculación", 12 },
                    { 155, "CERJ-04", "CERJ", "Documento que acredita los datos físicos, jurídicos de los inmuebles obrantes en el registro administrativo dependiente del Ministerio de Hacienda y Administraciones Públicas.", "Catastro: certificación", 12 },
                    { 156, "CERJ-05", "CERJ", "Documento que permite al titular del inmueble conocer su valor administrativo actualizado.", "Catastro: certificado valor", 12 },
                    { 157, "CERJ-06", "CERJ", "Certificado de Titularidad y Vigencia de Cuenta Bancaria", "Certificación de titularidad cuenta bancaria", 12 },
                    { 158, "CERJ-07", "CERJ", "Certificado de Cancelación de Deuda o Deuda 0 €", "Certificado deuda saldo cero", 12 },
                    { 159, "CERJ-08", "CERJ", "Certificado de Saldo en Cuenta Bancaria a una fecha concreta.", "Certificación saldo", 12 },
                    { 160, "CERJ-09", "CERJ", "Certificado de posición de cartera de valores a una fecha concreta.", "Certificado de acciones", 12 },
                    { 161, "CERJ-10", "CERJ", "Certificado que acredita la existencia y estado de financiación viva.", "Certificado financiación", 12 },
                    { 162, "CERJ-11", "CERJ", "Certificado emitido con indicación de que no existe deuda.", "Certificado deuda", 12 },
                    { 163, "CERJ-12", "CERJ", "Certificado que justifica una transacción financiera.", "Confirmación transacción", 12 },
                    { 164, "CERJ-13", "CERJ", "Certificación emitida por una institución, empresa o entidad en la que se da fe de la existencia de una relación contractual entre ésta y una persona física determinada.", "Contrato laboral: certificación", 12 },
                    { 165, "CERJ-14", "CERJ", "Certificado que acredita la existencia de una deuda en un contrato pendiente.", "Contrato: certificación deuda", 12 },
                    { 166, "CERJ-15", "CERJ", "Documento que justifica la presentación en el Registro Mercantil correspondiente de las cuentas anuales de una determinada persona jurídica, independientemente del medio en que estas se presenten.", "Cuentas anuales: presentación registro", 12 },
                    { 167, "CERJ-16", "CERJ", "Certificado que manifiesta el cumplimiento de la Ley Integración Social de Minusválidos.", "Cumplimiento Ley integración social minusvalidos (LISMI): certificado", 12 },
                    { 168, "CERJ-17", "CERJ", "Documento por que se   manifiesta si existe comisión de tercero y quien es el responsable de su pago dejando indemnes a SAREB de cualquier reclamación.", "Declaración indemnidad", 12 },
                    { 169, "CERJ-18", "CERJ", "Documento que acredita el conocimiento adecuado para la contratación de un producto financiero.", "Declaración idoneidad", 12 },
                    { 170, "CERJ-19", "CERJ", "Certificado emitido por medico forense que certifica la fecha, hora y motivo del fallecimiento de una persona.", "Defunción: certificado", 12 },
                    { 171, "CERJ-20", "CERJ", "Certificado emitido por la una entidad que acredita que un propietario, que forma parte de la entidad, está al corriente de pago de las derramas y cuotas giradas.", "Derramas y cuotas: certificación deuda", 12 },
                    { 172, "CERJ-21", "CERJ", "Documento que acredita que una vez disuelta una entidad se ha procedido a liquidar entre los partícipes, en relación a su participación, el patrimonio común que tenía en el momento de la liquidación.", "Disolución: liquidación económica", 12 },
                    { 173, "CERJ-22", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del estudio de detalle.", "Estudio de detalle: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 174, "CERJ-23", "CERJ", "Documento bancario a través del cual se permite valorar la liquidez y disponibilidad económica de una persona física o jurídica.", "Extracto bancario", 12 },
                    { 175, "CERJ-24", "CERJ", "Certificado tributario normalizado, en el que se justifica el alta en el Impuesto de Actividades Económicas.", "Impuesto actividades económicas (IAE): certificación alta", 12 },
                    { 176, "CERJ-25", "CERJ", "Certificado, girado por el ayuntamiento donde se ubica el inmueble, en el que se detalla la cuantía del impuesto a satisfacer por el propietario del mismo. La cuantía girada está en relación a los datos físicos, jurídicos y económicos que constan en el Padrón Municipal de Bienes Inmuebles.", "Impuesto sobre bienes inmuebles (IBI): certificado", 12 },
                    { 177, "CERJ-26", "CERJ", "Documento justificativo de las inversiones realizadas en acciones de una empresa, ya sean ordinarias o incorporen algún tipo de condición preferente, pero también todos los instrumentos derivados de estas acciones como los contratos de opción o de futuro, siempre que no puedan ser liquidados más que con la entrega de acciones u otros instrumentos de capital de la empresa.", "Inversión en instrumentos de capital", 12 },
                    { 178, "CERJ-27", "CERJ", "Escrito/informe de Rendición de Cuentas de la Administración Concursal", "Liquidación concurso: rendición de cuentas administración concursal", 12 },
                    { 179, "CERJ-28", "CERJ", "Documento privado por el que el órgano de administración de la sociedad certifica (título privado o notarial) si hay personas físicas que controlen o posean >25% de participación de la sociedad, identificando el nombre de las mismas.", "Manifestación titularidad real: certificación", 12 },
                    { 180, "CERJ-29", "CERJ", "Certificadoemitido por la Administración correspondiente que certifica que la persona física o jurídica está al corriente de las Obligaciones Fiscales y Seguridad social.", "Obra: certificación deudas con Administración Pública", 12 },
                    { 181, "CERJ-30", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del plan de sectorización/delimitación.", "Plan de sectorización/delimitación: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 182, "CERJ-31", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del plan especial de reforma interior.", "Plan especial de reforma interior: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 183, "CERJ-32", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del plan especial de reforma interior.", "Plan especial: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 184, "CERJ-33", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del plan parcial.", "Plan parcial: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 185, "CERJ-34", "CERJ", "Documento acreditativo que permite justificar el origen de las retenciones e ingresos correspondientes a los premios de la Sociedad Estatal Loterías y Apuestas del Estado.", "Premio lotería: certificado", 12 },
                    { 186, "CERJ-35", "CERJ", "Documento en el que consta la titularidad, cargas, gravámenes y superficies del activo inmobiliario que se encuentra vinculado a la operación del préstamo.", "Préstamo financiación operación: certificación / nota simple", 12 },
                    { 187, "CERJ-36", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del programa de actuación.", "Programa de actuación: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 188, "CERJ-37", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto.", "Proyecto actuación especial: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 189, "CERJ-38", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de calificación.", "Proyecto calificación urbanística: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 190, "CERJ-39", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de equidistribución.", "Proyecto equidistribución: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 191, "CERJ-40", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de expropiación.", "Proyecto expropiación: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 192, "CERJ-41", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de parcelación/segregación/agrupación.", "Proyecto parcelación/segregación/agrupación: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 193, "CERJ-42", "CERJ", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de urbanización.", "Proyecto urbanización: solicitud aprobación ante administración actuante (entrada en registro)", 12 },
                    { 194, "CERJ-43", "CERJ", "Documento que acredita la recepción de las llaves de un activo propiedad de Sareb así como la documentación asociada a la entrega de las mismas.", "Recibí entrega llaves y documentación", 12 },
                    { 195, "CERJ-44", "CERJ", "Certificado bancario a través el cual se permite valorar la solvencia económica de una persona física o jurídica, para la operación.", "Referencia escrita entidades financieras", 12 },
                    { 196, "CERJ-45", "CERJ", "Documento que acredita el alta de una persona física en el Régimen Especial de Trabajadores Autónomos.", "Régimen especial de autónomos (RETA): alta", 12 },
                    { 197, "CERJ-46", "CERJ", "Certificación relacionando Ortofoto con descripción Registral de Finca.", "Registro Propiedad: certificación base gráfica", 12 },
                    { 198, "CERJ-47", "CERJ", "Documento oficial emitido por el Registro de la Propiedad oportuno, en el cual se certifica en una determinada fecha el estado de Dominio y Cargas de la finca a consultar. Este documento va firmado y cotejado por el Registrador y tiene una validez mucho mayor que la nota simple informativa, sirviendo como prueba frente a terceros en caso de disputas judiciales. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"07 - Garantías\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-...)", "Registro Propiedad: certificación dominio y cargas", 12 },
                    { 199, "CERJ-48", "CERJ", "Certificado sobre finca rustica emitido por SIGPAC (organismo dependiente del Ministerio Agricultura)", "Sigpac: certificación", 12 },
                    { 200, "CERJ-49", "CERJ", "Documento en el que se asegura la veracidad de una valoración y/o las circunstancias relacionadas con la misma, en el que se contiene entre otros el tipo del inmueble tasado, el estado de ocupación, localización, finca registral, valor de tasación, cautelas al valor de tasación, métodos de valoración utilizados,...", "Tasación: certificado", 12 },
                    { 201, "CERJ-50", "CERJ", "Certificado de tasación en el cual se recogen de forma agrupada diversas fincas.", "Tasación: certificado agrupado", 12 },
                    { 202, "CERJ-51", "CERJ", "Certificado Emitido por el Ministerio de justicia que Certifica la existencia, o no, de Testamento y notario en el que se firmó.", "Ultimas voluntades: certificado", 12 },
                    { 203, "CERJ-52", "CERJ", "Documento expedido por la administración que certifica la adjudicación de un bien y que constituye título inscribible en el registro respecto al cambio de titularidad del bien.", "Certificación administrativa de adjudicación", 12 },
                    { 204, "CERJ-53", "CERJ", "Conjunto de documentos relacionados con la cancelación de las condiciones resolutorias inscritas en el registro en una vivienda de protección (solicitud a la administración actuante, certificación adminstrativa,…)", "Vivienda de protección: cancelación condiciones resolutorias", 12 },
                    { 205, "CERJ-54", "CERJ", "Documento en el que se detalla la entrega de una garantía otorgada por el arrendador a favor del arrendatario (p.e. documento de aval, resguardo del ingreso de cantidades –fianzas/depósitos – en la cuenta del arrendador,…). Téngase en cuenta que el resguardo del depósito de la fianza legal en el organismo correspondiente cuenta con un tipo documental propio”.", "Alquiler: fianza / Aval", 12 },
                    { 206, "CERJ-55", "CERJ", "Documento, emitido por el padrón municipal correspondiente, donde se certifica que una persona concreta reside habitualmente en un inmueble concreto ubicado en el municipio del padrón correspondiente.", "Certificado de empadronamiento", 12 },
                    { 207, "CERJ-56", "CERJ", "Informe emitido por la Seguridad Social donde se relaciona toda la actividad laboral de un trabajador, resumiendo, con su fecha de inicio y fin, las distintas empresas donde a trabajado, el grupo de cotización,…", "Certificado de vida laboral", 12 },
                    { 208, "CERJ-57", "CERJ", "Certificado que detalla, para un determinado momento, los importes adeudados por concepto.", "Certificado deuda saldo pendiente", 12 },
                    { 209, "CERJ-59", "CERJ", "Documento informativo que narra la vinculación o no de un activo con una persona o entidad. *Para las cartas de no vinculación utilice CERJ-03", "Informe de vinculaciones", 12 },
                    { 210, "CERJ-60", "CERJ", "Documento que acredita la recepción de la amortización parcial de un préstamo", "Recibí cancelación parcial", 12 },
                    { 211, "CERJ-61", "CERJ", "Certificado que acredita que el receptor dispone de la documentación enviada por un emisor", "Recibí documentación", 12 },
                    { 212, "CERJ-63", "CERJ", "Documento que certifica la titularidad y vigencia de una cuenta bancaria.", "Certificado titularidad cuenta bancaria", 12 },
                    { 213, "CERJ-64", "CERJ", "Certificado para la comisión sobre financiación (concedida por el gestor y/o sócio bancario o cualquier entidad perteneciente al grupo)", "Certificado comisión", 12 },
                    { 214, "CERJ-65", "CERJ", "Documento que certifica oficialmente, que una persona cuenta con antecedentes penales de acuerdo a la legislación vigente.", "Certificado penales", 12 },
                    { 215, "CERJ-66", "CERJ", "Documento oficial emitido por el Registro mercantil en el cual se certifica una información referente a una empresa u organización concreta.", "Registro mercantil: certificación", 12 },
                    { 216, "CERJ-67", "CERJ", "Documento a través del cual se constata que una persona, relacionada con alguna operación de Sareb, se encuentra por razones físicas, familiares, económicas o cualesquiera otras, en situación vulnerable, entendiendo por esta, aquella circunstancia que hace que la persona se tenga que enfrentar a especiales dificultades las cuales deben ser tenidas en cuenta a la hora de tomar la decisión oportuna.", "Acreditación situación vulnerable", 12 },
                    { 217, "CERJ-68", "CERJ", "Documento expedido por el tribunal que recoge el mejor precio ofrecido por una persona en subasta y la deuda pendiente por todos los conceptos", "Subasta: Certificación acreditativa del precio del precio del remate y la deuda", 12 },
                    { 218, "CERJ-69", "CERJ", "Documento que pone fin a la subasta electrónica transcurrido el plazo legal establecido, existiendo pujas o suspendida por el letrado de la administración", "Subasta: Cierre de subasta en el portal de subastas electrónicas", 12 },
                    { 219, "CERT-01", "CERT", "Documento acreditativo de que la obra nueva cumple con las exigencias legales relativas a la protección contra el ruido.", "Acústica: certificación", 13 },
                    { 220, "CERT-02", "CERT", "Documento oficial garante de que la obra cumple con las especificaciones de calidad marcadas por el proyecto de ejecución. Este documento es necesario para el libro del edificio y para la elevación a público de la obra.", "Calidad: certificación control calidad y programa resultados", 13 },
                    { 221, "CERT-03", "CERT", "Documento que acredita, a través de los ensayos físicos y químicos realizados, que una o varias características de una materia prima o productos relacionados con el proceso constructivo están dentro de los parámetros marcados para el mismo.", "Calidad: certificación ensayo materiales", 13 },
                    { 222, "CERT-04", "CERT", "Documento acreditativo de que la referida instalación, ha sido mantenida de acuerdo con el manual de uso y mantenimiento del activo.", "Certificado mantenimiento", 13 },
                    { 223, "CERT-05", "CERT", "Documento acreditativo de la clasificación energética alcanzada por el inmueble. Es un documento oficial redactado por un técnico competente que incluye información objetiva sobre las características energéticas de un inmueble, calculando el consumo anual de energía necesario para satisfacer la demanda energética de éste en condiciones normales de ocupación y funcionamiento.", "Eficiencia energética: certificado", 13 },
                    { 224, "CERT-06", "CERT", "Documento, emitido por persona autorizada preceptivo para la obtencion de la licencia de primera ocupación, que certifica el cumplimiento de la entrega de residuos de construcción y demolición por parte del poseedor al gestor en documento fehaciente donde figura, al menos, la identificación del poseedor y del productor, la obra de procedencia y, en su caso, el número de licencia de la obra, la cantidad, expresada en toneladas o en metros cúbicos, o en ambas unidades cuando sea posible, el tipo de residuos entregados, codificados con arreglo a la Lista Europea de Residuos.", "Gestión de residuos: certificado", 13 },
                    { 225, "CERT-07", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica que la instalación de agua cumple con todos los requisitos para el suministro. Este certificado es necesario para contratar el suministro de agua.", "Instalación agua: boletín suministro", 13 },
                    { 226, "CERT-08", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica la correcta instalación de los ascensores.", "Instalación ascensores: certificación", 13 },
                    { 227, "CERT-09", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica que la instalación de equipos térmicos (calefacción, ventilación, refrigeración) es adecuada", "Instalaciones térmicas: certificación", 13 },
                    { 228, "CERT-10", "CERT", "Documento acreditativo oficial emitido por un instalador electricista autorizado que certifica que una instalación cumple todos los requisitos para el suministro. Recoge la características de la instalación, la potencia instalada y la máxima admisible y garantiza la calidad de la misma. Incluye un esquema y un plano de ubicación de los elementos instalados. Este certificado es necesario para contratar el suministro eléctrico.", "Instalación eléctrica: boletín suministro", 13 },
                    { 229, "CERT-11", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica que la instalación de gas cumple con todos los requisitos para el suministro. Este certificado es necesario para contratar el suministro del gas.", "Instalación gas: boletín suministro", 13 },
                    { 230, "CERT-12", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica que la instalación de los paneles solares es adecuada.", "Instalación paneles solares: certificación", 13 },
                    { 231, "CERT-13", "CERT", "Documento acreditativo oficial, firmado por técnico titulado competente y debidamente visado, en el que se acredita que la instalación de protección contra incendios cumple con el proyecto y con las prescripciones legales y reglamentarias.", "Instalación PCI: certificación", 13 },
                    { 232, "CERT-14", "CERT", "Documento acreditativo oficial, emitido por instalador autorizado, que certifica la correcta instalación de la red y equipos de telecomunicaciones.", "Instalación telecomunicaciones: certificación", 13 },
                    { 233, "CERT-15", "CERT", "Documento acreditativo que contiene la medición valorada según el presupuesto del proyecto con los modificados que se introducen durante la ejecución de la obra, realizada por la empresa constructora y ratificada por dirección facultativa que garantiza ante la propiedad o la promotora o banco crediticio u organismo institucional que una parte de la obra ha sido realmente ejecutada por el contratista y para que así conste a los efectos oportunos", "Obra: certificación obra", 13 },
                    { 234, "CERT-16", "CERT", "Certificado descriptivo de la obra y acreditativo de su estado constructivo de acuerdo al proyecto de obra para con el que se obtuvo la licencia firmado por el director de obras de construcción.", "Obra: certificado estado obra", 13 },
                    { 235, "CERT-17", "CERT", "Documento acreditativo, visado en el colegio profesional, correspondiente de la finalización de la obra. El director de la obra certificará que la misma ha sido realizada bajo su dirección, de conformidad con el proyecto objeto de licencia y la documentación técnica que lo complementa, hallándose dispuesta para su adecuada utilización con arreglo a las instrucciones de uso y mantenimiento.", "Obra: certificado final", 13 },
                    { 236, "CERT-18", "CERT", "Documentos acreditativo de los instaladores donde se acredita que lo ejecutado se ajusta a la legalidad y a lo proyectado y correspondiente a su trabajo especifico. No es un documento normativo sino un escrito necesario en tramitaciones como la calificación definitiva en viviendas de protección.", "Otra instalación: certificación", 13 },
                    { 237, "CERT-19", "CERT", "Calificación definitiva de la vivienda a promover expedida por el organismo correspondiente, a solicitud del promotor, con posterioridad a la obtención del certificado provisional y tras la finalización de la obra, conforme al uso y tipo de protección marcado legalmente.", "Vivienda de protección: certificado definitivo", 13 },
                    { 238, "CERT-20", "CERT", "Calificación provisional de la vivienda a promover, solicitada por el promotor y expedida por el organismo correspondiene, al inicio de la promoción conforme al uso y tipo de protección marcado legalmente. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Vivienda de protección: certificado provisional", 13 },
                    { 239, "CERT-21", "CERT", "Documento a través del cual se certifica el estado de cargas (económicas, urbanísticas,..) de una obra de urbanización", "Obra: certificado cargas", 13 },
                    { 240, "CERT-22", "CERT", "Documento a través del cual el redactor del proyecto de ejecución certifica que, de acuerdo a la normativa aplicable y a las características físicas, instalaciones y demás determinaciones del proyecto, al inmueble de nueva planta le corresponde una calificación de eficiencia energética concreta.", "Proyecto ejecución: certificado de eficiencia energética", 13 },
                    { 241, "CERT-23", "CERT", "Documento de naturaleza pública que detalla la clasificación de un suelo de acuerdo a la normativa urbanística vigente.", "Suelo: Certificado clasificación", 13 },
                    { 242, "CNCV-01", "CNCV", "Documento que manifiesta el compromiso de los intervinientes a no utilizar una información específica para sus propios fines", "Acuerdo confidencialidad", 14 },
                    { 243, "CNCV-02", "CNCV", "Acuerdo por el que se aprueba la venta de una garantía asociada a una operación financiera.", "Acuerdo desinversión colateral", 14 },
                    { 244, "CNCV-03", "CNCV", "Documento contractual firmado entre la entidad y un tercero a través del cual se acuerda el arrendamiento de un activo propiedad dicha entidad.", "Alquiler activo entidad: contrato", 14 },
                    { 245, "CNCV-04", "CNCV", "Documento contractual a través del cual el propietario (arrendador) se compromete a ceder durante un tiempo determinado el derecho a usar y disfrutar un bien a favor de un tercero (arrendatario) el cual se compromete a realizar los pagos acordados como contraprestación de este derecho de uso. Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Alquiler: contrato", 14 },
                    { 246, "CNCV-05", "CNCV", "Contrato privado en el que se alquila un bien incluyendo una clausula por la que el arrendador puede ejercer la adquisición del bien a un precio previamente establecido.", "Alquiler: contrato con opción a compra", 14 },
                    { 247, "CNCV-06", "CNCV", "Documento acreditativo de la situación ocupacional de un inmueble adjudicado por sentencia judicial.", "Alquiler: modelo libertad de arrendamiento", 14 },
                    { 248, "CNCV-07", "CNCV", "Documento, firmado por el propietario del bien y el potencial comprador, en el que se detallan los datos que aparecerán en el futuro contrato de compravena y en el que se refleja el acuerdo jurídico por el que el potencial comprador se reserva el derecho sobre la compra del bien mediante la entrega de una cantidad. Las arras pueden ser penienciales o confirmatorias, siendo las más habituales las penitenciales, celebrados al amparo del art. 1454 del Código Civil. Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Arras: contrato", 14 },
                    { 249, "CNCV-08", "CNCV", "Pliego de condiciones o acuerdo marco sobre la obtención de una ayuda o subvención.", "Ayuda y/o subvención: acuerdo marco y/o pliego de condiciones", 14 },
                    { 250, "CNCV-09", "CNCV", "Contrato de colaboración formalizado sobre ayuda o subvención.", "Ayuda y/o subvención: contrato colaboración entidad otorgante", 14 },
                    { 251, "CNCV-10", "CNCV", "Documento en el que se formaliza el aval de la SGR.", "Ayuda y/o subvención: contrato reafianzamiento", 14 },
                    { 252, "CNCV-11", "CNCV", "Contratos de cobertura sobre pagos.", "Bien mobiliario: covenant", 14 },
                    { 253, "CNCV-12", "CNCV", "Documento de rescisión de Contrato.", "Cancelación: contrato", 14 },
                    { 254, "CNCV-13", "CNCV", "Acuerdo de Plan de Pagos aceptado.", "Cobros de regularización específicos: acuerdo", 14 },
                    { 255, "CNCV-14", "CNCV", "Contrato firmado entre una persona física o jurídica con una entidad financiera y por el que dicha persona puede ingresar en dicha entidad importes en efectivo que conforman un saldo a su favor del que puede disponer de forma inmediata, parcial o totalmente.", "Contrato apertura de cuentas", 14 },
                    { 256, "CNCV-15", "CNCV", "Relación contractual que se establece normalmente entre dos partes y que supone que la primera (el arrendador) le entrega algún elemento suyo (mueble o inmueble) a la segunda parte (el arrendatario) para que la utilice en su beneficio propio.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Contrato arrendamiento", 14 },
                    { 257, "CNCV-16", "CNCV", "Otro contrato de Cobertura no incluido en otra tipología definida en la presente clasificación.", "Contrato cobertura", 14 },
                    { 258, "CNCV-17", "CNCV", "Contrato privado que recoge los acuerdos y condiciones existentes entre el comprador y el vendedor para transmitir un activo a cambio de una contraprestación.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Contrato compraventa", 14 },
                    { 259, "CNCV-18", "CNCV", "Contrato privado de constitución de garantía en una operación financiera.", "Contrato constitución garantía", 14 },
                    { 260, "CNCV-19", "CNCV", "Documento a través del cual un tercero, ajeno al negocio principal garantizado, conviene de forma expresa prestar garantía personal comprometiéndose a responder, subsidiaria o solidariamente, del cumplimiento ante el acreedor, en lugar del deudor, que es el obligado principal, para el caso en que éste no cumpla.", "Contrato fianza", 14 },
                    { 261, "CNCV-20", "CNCV", "Documento por el que se entrega un objeto o cantidad de dinero al prestatario, quien se obliga a restituirlo con posterioridad, con el objeto de abordar una operación financiera determinada.", "Contrato financiación", 14 },
                    { 262, "CNCV-21", "CNCV", "Documento en el que se recoge un pacto o acuerdo entre trabajador y empresario en virtud del cual el trabajador se compromete de manera voluntaria, a la realización o prestación de determinados servicios, por cuenta del empresario y dentro de su ámbito de organización y dirección, a cambio de una retribución.", "Contrato laboral", 14 },
                    { 263, "CNCV-22", "CNCV", "Contrato sobre Paquete SAREB 1-5 Bienes", "Contrato marco PDV", 14 },
                    { 264, "CNCV-23", "CNCV", "Contrato Privado de operación financiera, no contemplado en otras clasificación", "Contrato operación financiera", 14 },
                    { 265, "CNCV-24", "CNCV", "Contrato privado o documento público por el cual un tercero sustituye (total o parcialmente) a una de las partes del contrato de la operación", "Contrato subrogación", 14 },
                    { 266, "CNCV-25", "CNCV", "Acto jurídico bilateral en virtud de la cual una parte se obliga a prestar servicios específicos, por un tiempo determinado en favor de otra, la que a su vez se obliga a pagar una cierta cantidad de dinero por dichos servicios.", "Contrato transacción comercial o prestación de servicios", 14 },
                    { 267, "CNCV-27", "CNCV", "Documento a través del cual, la empresa contratante, pone de manifiesto la contratación de una obra o servicio a favor de una o varias personas físicas o jurídicas.", "Contrato: adjudicación", 14 },
                    { 268, "CNCV-28", "CNCV", "Contrato a través del cual las partes acuerdan la cesión de deuda referente a un contrato.", "Contrato: cesión deuda", 14 },
                    { 269, "CNCV-29", "CNCV", "Documento que acredita los acuerdos no onerosos entre las partes contratantes.", "Contrato: convenio", 14 },
                    { 270, "CNCV-30", "CNCV", "Documento en el que se recoge un pacto o acuerdo entre las partes contratantes en virtud del cual se regulan las relaciones que regirán su relación contractual y se comprometen de manera voluntaria y recíproca al cumplimiento de lo establecido en el mismo.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Contrato: documento contractual", 14 },
                    { 271, "CNCV-31", "CNCV", "Documento que constituye un suplemento al contrato existente, a través del cual se especifican aquellos aspectos del contrato que se acuerda modificar documentando en el éste el costo y se define el procedimiento para adaptarlo a la planificación original", "Contrato: orden de cambio", 14 },
                    { 272, "CNCV-32", "CNCV", "Acuerdo suscrito por las partes (privadas o públicas) al objeto de colaborar en el desarrollo de la actividad urbanística.", "Convenio entidad gestión", 14 },
                    { 273, "CNCV-33", "CNCV", "Documento en el que el deudor asume determinados compromisos para proteger el pago de la deuda de la operación contratada.", "Covenant", 14 },
                    { 274, "CNCV-34", "CNCV", "Relación de KPI's que advierten sobre la calidad o el riesgo de un Covenant.", "Covenant - Indicadores de calidad del riesgo o de control", 14 },
                    { 275, "CNCV-35", "CNCV", "Contrato de Compra venta que recoge la dación de un bien para la satisfacción de una deuda.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Dación: contrato", 14 },
                    { 276, "CNCV-36", "CNCV", "Propuesta de convenio del Deudor presentado en el Procedimiento Concursal.", "Declaración concurso: propuesta convenio", 14 },
                    { 277, "CNCV-37", "CNCV", "Acuerdo privado realizado entre la entidad gestora y el propietario de un bien o derecho incompatible a través del cual se acuerda la indemnización a recibir por el propietario como consecuencia de dicha incompatibilidad con el planeamiento marcado.", "Indemnización: acuerdo entre partes", 14 },
                    { 278, "CNCV-38", "CNCV", "Contrato privado de operación sindicada.", "Operaciones sindicadas: contrato entidades participantes", 14 },
                    { 279, "CNCV-39", "CNCV", "Póliza de garantía personal.", "Póliza de afianzamiento", 14 },
                    { 280, "CNCV-40", "CNCV", "Documento privado en el que pone de manifiesto la entrega de la posesión sobre un activo concreto.", "Posesión activo: documento privado de entrega", 14 },
                    { 281, "CNCV-41", "CNCV", "Contrato de prestación de servicios de postventa de una empresa o profesional.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Postventa: contrato gestor", 14 },
                    { 282, "CNCV-42", "CNCV", "Acuerdo de refinanciación en el marco de Articulo 5.3 LC, negociaciones previas al concurso y comunicadas al Juzgado Mercantil.", "Preconcurso: acuerdo refinanciación", 14 },
                    { 283, "CNCV-44", "CNCV", "Documento por el que se entrega un objeto o cantidad de dinero al prestatario, quien se obliga a restituirlo con posterioridad, con el objeto de abordar una operación financiera determinada.", "Préstamo financiación operación: contrato privado", 14 },
                    { 284, "CNCV-45", "CNCV", "Contrato privado de Préstamo que constituye el origen de una o varias operaciones financieras o inmobiliarias.", "Préstamo originario: contrato", 14 },
                    { 285, "CNCV-46", "CNCV", "Documento acreditativo de la presentación, ante la administración actuante, de la solicitud de aprobación del proyecto de parcelación/segregación/agrupación.", "Programa de actuación: convenio", 14 },
                    { 286, "CNCV-47", "CNCV", "Acuerdo suscritos por las partes (privadas o públicas) al objeto de colaborar en el desarrollo de la actividad urbanística contenida en el proyecto de equidistribución.", "Proyecto equidistribución: convenio", 14 },
                    { 287, "CNCV-48", "CNCV", "Acuerdo suscritos por las partes (privadas o públicas) al objeto de colaborar en el desarrollo de la actividad urbanística contenida en el proyecto de urbanización.", "Proyecto urbanización: convenio", 14 },
                    { 288, "CNCV-49", "CNCV", "Contrato privado que recoge el cobro de una deuda mediante la aportación de dinero.", "Recuperación fondos líquidos: contrato", 14 },
                    { 289, "CNCV-50", "CNCV", "Documento a través del cual las partes interesadas en la transmisión de un bien (propietario del activo y futuro comprador) acuerdan la reserva de un bien para su posteior compraventa.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Reserva: contrato", 14 },
                    { 290, "CNCV-51", "CNCV", "Documento debidamente motivado que expresa la denuncia de un contrato motivado por el previo incumplimiento de la otra parte.", "Resolución contrato", 14 },
                    { 291, "CNCV-52", "CNCV", "Acuerdo en el marco de negociaciones anteriores al Preconcurso (Art 5.3) o Concurso de Acreedores.", "Standstill: acuerdo", 14 },
                    { 292, "CNCV-53", "CNCV", "Documento por el cual el ejecutante en una subasta cede a un tercero la adjudicación.", "Subasta: cesión de remate", 14 },
                    { 293, "CNCV-54", "CNCV", "Acuerdo entre partes para la cesión del uso de un solar.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Uso solar: contrato privado cesión", 14 },
                    { 294, "CNCV-55", "CNCV", "Documento contractual firmado entre la entidad y un tercero a través del cual se acuerda la venta de un activo propiedad dicha entidad.  Dentro de este tipo documental se incluirán las distintas modificaciones o novaciones del contrato, la resolución,…", "Venta activo entidad: contrato", 14 },
                    { 295, "CNCV-56", "CNCV", "Acuerdo de suspensión temporal de un contrato formal en el marco de una gestión de deuda en riesgo de cobro.", "Waiver: acuerdo", 14 },
                    { 296, "CNCV-57", "CNCV", "Documento en el que se detallan los distintos acuerdos alcanzados, durante la ejecución de las obras, con las compañias suministradoras", "Obra: acuerdos con compañias suministradoras", 14 },
                    { 297, "CNCV-58", "CNCV", "Plan de Pagos propuesto por el deudor en su propuesta de renegociacion de la deuda", "Cobros de regularización específicos: Plan de Pagos", 14 },
                    { 298, "CNCV-59", "CNCV", "Documento contractual en el cual se determinan o establecen las condiciones que se aceptan en un contrato", "Pliego de Contratación", 14 },
                    { 299, "CNCV-61", "CNCV", "Documento a través del cual la propiedad de un inmueble autoriza la comercialización por un tercero del inmueble de su propiedad.", "Contrato de comercialización", 14 },
                    { 300, "CNCV-62", "CNCV", "Documento contractual a través del cual, el depositario acuerda entregar al depositante, de forma unilateral y gratuita, una cantidad de dinero acordada en concepto de depósito. Quedando el depositario obligado a devolverlo íntegramente una vez vencido lo estipulado en el contrato o a solicitud del depositante.", "Depósito: contrato", 14 },
                    { 301, "CNCV-63", "CNCV", "Documento contractual, de tipología no contemplada en otro tipo documental de la presente clasificación, que acredita el título de propiedad del activo.", "Titularidad del activo: otro contrato", 14 },
                    { 302, "CNCV-64", "CNCV", "Documento en el que se muestra la propuesta de redacción de un contrato, ya sea privado o a elevar a público, acorde a las nuevas condiciones establecidas para su análisis y validación de forma previa a la formalización del mismo.", "Borrador contratos", 14 },
                    { 303, "CNCV-65", "CNCV", "Documento que acredita el título suficiente del derecho de uso de un inmueble por naturaleza distinta a la arrendaticia (p.e. usufructo, comodato, precario,…)", "Derecho de uso: Título", 14 },
                    { 304, "CNCV-66", "CNCV", "Documento anexo al acuerdo de confidencialidad (NDA) firmado anteriormente por el inversor en el que incorpora una operación analizada concreta dentro del alcance del acuerdo de confidencialidad firmado previamente", "Acuerdo de confidencialidad: Anexo", 14 },
                    { 305, "CNCV-67", "CNCV", "Documento en el que se expresan acuerdos entre distintos intervinientes, antes de redactarlo definitivamente y proceder a su firma privada o elevarlo a público.", "Minutas / borradores", 14 },
                    { 306, "CNCV-68", "CNCV", "Documento contractual por el que se prestan los suministros en un inmuble", "Contrato de suministro", 14 },
                    { 307, "CNCV-69", "CNCV", "Acuerdo entre comprador y vendedor para la transmisión de un Activo previo a su formalización en documento público, en el que se  recogen las condiciones y contraprestaciones  de esa transmisión", "Acuerdo de Venta", 14 },
                    { 308, "CNCV-70", "CNCV", "Documento contractual a través del cual el propietario (arrendador) y arrendatario finalizan el contrato de alquiler en vigor", "Rescisión Alquiler: contrato", 14 },
                    { 309, "COMU-01", "COMU", "Tramite administrativo previo realizado ante la Administración a través del cual se solicita la modificación de la licencia, previamente aprobada y en vigor, en el que se especifica el cambio de actividad y/o titularidad solicitado.", "Actividad: comunicado cambio titularidad o actividad", 15 },
                    { 310, "COMU-02", "COMU", "Documento suscrito por el titular de la actividad o por su representante, en el que manifiesta, bajo su responsabilidad, que cumple con los requisitos urbanísticos y sectoriales exigidos por la normativa vigente para implantar, modificar o ejercer la actividad, que dispone de la documentación que así lo acredita y que se compromete a mantener su cumplimiento durante el tiempo en que ejercite su actividad.", "Actividad: declaración de responsable", 15 },
                    { 311, "COMU-03", "COMU", "Escrito de Insinuación de Crédito a la Administración Concursal.", "Administración concursal: comunicado de crédito", 15 },
                    { 312, "COMU-04", "COMU", "Comunicación formal de una Administración Publica.", "Administraciones publicas: comunicación", 15 },
                    { 313, "COMU-05", "COMU", "Comunicación Emitida por la Agencia Tributaria.", "AEAT: comunicación", 15 },
                    { 314, "COMU-06", "COMU", "Documento en el que se especifica la propuesta o comunicación realizada por la asamblea y/o el consejo.", "Asamblea y/o consejo: propuesta y/o comunicación", 15 },
                    { 315, "COMU-07", "COMU", "Comunicación fehaciente y prejudicial realizada al deudor en la que se le emplaza para la satisfacción de la deuda concreta.", "Notificación deudor", 15 },
                    { 316, "COMU-08", "COMU", "Notificación, recibo o carta no formal comunicando la cancelación anticipada de la deuda", "Cancelación: notificación cancelación anticipada", 15 },
                    { 317, "COMU-09", "COMU", "Comunicaciones y otros escritos relacionados con la viabilidad delos suministros durante las obras de urbanización", "Compañías suministradoras: comunicación", 15 },
                    { 318, "COMU-11", "COMU", "Documento en el que se pone en conocimiento de Sareb la existencia incidencia sobre un activo o en el desarrollo de una operación concreta.", "Comunicación incidencia", 15 },
                    { 319, "COMU-12", "COMU", "Escrito formal remitido a inversores en el marco de una operación de compra venta.", "Comunicación inversores", 15 },
                    { 320, "COMU-13", "COMU", "Escrito del titular del terreno por el acepta o rechaza el incorporarse dentro de la junta de compensacion o similar para participar en el desarrollo de la actuación.", "Constitución: comunicación aceptación/rechazo a cooperar", 15 },
                    { 321, "COMU-14", "COMU", "Documento declarativo del titular del terreno del modo (especie o metálico) de cooperación en el proceso urbanizador o retribución al agente urbanizador.", "Constitución: comunicación elección modo retribución cuotas", 15 },
                    { 322, "COMU-15", "COMU", "Documento contractual, de carácter exhaustivo y obligatorio en el cual se establecen las condiciones o cláusulas que se aceptan en un contrato.", "Contrato: comunicación", 15 },
                    { 323, "COMU-17", "COMU", "Comunicación del Administrador Concursal en la que se indica la apertura del concurso con la copia del Auto", "Declaración concurso: notificación administrador", 15 },
                    { 324, "COMU-18", "COMU", "Documentación asociada a la reclamación. respecto al impago de los gastos comunes girados en cuotas o derramas, efectuada por el gestor de la entidad al propietario moroso. En este apartado se incluye cualquier tipo de comunicaciones mantenida incluida el burofax de reclamación de deuda preceptivo para el inicio del procedimiento de reclamación preceptivo.", "Derramas y cuotas: carta reclamación impagados y burofax", 15 },
                    { 325, "COMU-19", "COMU", "Comunicación sobre el proceso expropiatorio no incluida dentro de otro documento específico definido en la clasificación realizado para las expropiaciones.", "Expropiación: otra comunicación", 15 },
                    { 326, "COMU-20", "COMU", "Escrito de comunicación, a los acreedores, en la que se manifiesto el fin de la vigencia de la escritura de FAB", "Extinción FAB: comunicación a acreedores", 15 },
                    { 327, "COMU-21", "COMU", "Escrito de comunicación, a la CNMV, en la que se manifiesto el fin de la vigencia de la escritura de FAB", "Extinción FAB: comunicación a la CNMV", 15 },
                    { 328, "COMU-22", "COMU", "Documento presentado ante el Ayuntamiento que insta al establecimiento del sistema de compensación con el objeto de asumir la actuación urbanizadora. Incluye en su caso la propuesta de bases y estatutos de la Junta de Compensación o Agrupación de Interés Urbanístico.", "Iniciativa urbanizadora", 15 },
                    { 329, "COMU-23", "COMU", "Escrito formal en el que se notifica al deudor el cambio de titularidad de la operación financiera.", "Notificación cambio de titularidad activos", 15 },
                    { 330, "COMU-24", "COMU", "Manifestación a la Administración competente de la apertura de un centro de trabajo.", "Obra: comunicación apertura centro trabajo", 15 },
                    { 331, "COMU-25", "COMU", "Comunicaciones y otros escritos relacionados con la viabilidad de los suministros durante las obras de urbanización emitidos o recibidos por las compañias suministradoras.", "Obra: comunicación compañías suministradoras", 15 },
                    { 332, "COMU-26", "COMU", "Comunicaciones con agentes externos intervinientes en la ejecución de la obra en curso que puedan ser relevantes tanto durante la ejecución de la obra como en un momento posterior.", "Obra: comunicación con agente externo", 15 },
                    { 333, "COMU-27", "COMU", "Comunicación dirigida al Ayuntamiento, en la que se pone de manifiesto el inicio de la obra.", "Obra: comunicación inicio", 15 },
                    { 334, "COMU-28", "COMU", "Solicitud realizada por parte del Ayuntamiento para que el proyecto, la obra o la edificación cumpla los criterios legales marcados por la administración tanto en la modificación para la obtención de licencia, subsanación de deficiencias, concesión de licencias,...", "Obra: requerimiento municipal", 15 },
                    { 335, "COMU-29", "COMU", "Documento por el cual el gestor de la iniciativa solicita a la administración actuante la recepción de las obras de urbanización, instalaciones y dotaciones cuya ejecución estaba prevista en el plan de ordenación y proyecto de urbanización aprobado.", "Obra: solicitud recepción obra", 15 },
                    { 336, "COMU-30", "COMU", "Comunicaciones y otros escritos relacionados con la postventa de un activo.", "Postventa. escrito y comunicación", 15 },
                    { 337, "COMU-31", "COMU", "Escrito enviado por el cliente durante la fase de postventa a través del cual solicita al promotor (Sareb) la subsanación de alguna deficiencia detectada en el bien adquirido.", "Postventa: requerimiento cliente", 15 },
                    { 338, "COMU-32", "COMU", "Documentación relativa a las servidumbres derivadas de la navegación aérea que afectan al activo.", "Servidumbre y afección: aérea", 15 },
                    { 339, "COMU-33", "COMU", "Documentación relativa a la protección del dominio público hidráulico y de la calidad de las aguas que afecta al activo.", "Servidumbre y afección: aguas", 15 },
                    { 340, "COMU-34", "COMU", "Documentación relativa a Protección del dominio público viario y limitaciones a la propiedad que afectan al activo.", "Servidumbre y afección: carreteras", 15 },
                    { 341, "COMU-35", "COMU", "Documentación relativa a las afecciones derivadas de contaminación atmosférica, acústica o de agua que afectan al activo.", "Servidumbre y afección: contaminaciones", 15 },
                    { 342, "COMU-36", "COMU", "Documentación relativa a la protección de costas que afecta al activo.", "Servidumbre y afección: costas", 15 },
                    { 343, "COMU-37", "COMU", "Documentación relativa a las Afecciones derivadas del las infraestructuras ferroviarias que afectan al activo.", "Servidumbre y afección: ferroviaria", 15 },
                    { 344, "COMU-38", "COMU", "Documentación relativa a las Afecciones derivadas del resto de infraestructuras territoriales que afectan al activo.", "Servidumbre y afección: infraestructuras", 15 },
                    { 345, "COMU-39", "COMU", "Documentación relativa a las afecciones derivadas por yacimientos minerales y demás recursos geológicos que afectan al activo", "Servidumbre y afección: minas", 15 },
                    { 346, "COMU-40", "COMU", "Documentación relativa a cualesquiera otras servidumbres que afecten al activo y que no se encuentren especificadas en la clasificación", "Servidumbre y afección: otra", 15 },
                    { 347, "COMU-41", "COMU", "Documentación relativa a las afecciones derivadas de restos arqueológicos que afectan al activo.", "Servidumbre y afección: protección arqueológica", 15 },
                    { 348, "COMU-42", "COMU", "Documentación relativa a las afecciones por valor artístico, histórico o antropológico que afectan al activo.", "Servidumbre y afección: protección cultural", 15 },
                    { 349, "COMU-43", "COMU", "Escrito formal en el que se notifica a tercero interesado el cambio de titularidad del bien", "Traspaso Sareb: notificación cambio titularidad", 15 },
                    { 350, "COMU-44", "COMU", "Comunicación fehaciente con certificación de contenido en el adquirente/adjudicatario del activo manifiesta al sujeto pasivo originario el cumplimiento de la obligacion legalmente establecida respecto a la inversión del sujeto pasivo en transmisiones de inmuebles como consecuencia de ejecuciones de garantía.", "Subasta: burofax enviado ajustado a normativa IVA", 15 },
                    { 351, "COMU-45", "COMU", "Conjunto de comunicaciones, tanto emitidas como recibidas, relatival al impuesto de bienes inmuebles", "Impuesto sobre bienes inmuebles (IBI): comunicaciones", 15 },
                    { 352, "COMU-46", "COMU", "Conjunto de actividades extrajudiciales realizadas por el gestor del activo en aras a lograr el abandono y posterior toma de posesión de un inmueble propiedad de Sareb que está siendo ocupado por un tercero ilegalmente", "Posesión activo: gestiones extrajudiciales desalojo", 15 },
                    { 353, "COMU-47", "COMU", "Documento formal, publico o no,  por el que el deudor o el propietario del bien o derecho autoriza para el levantamiento de la inmovilización ejercida sobre dicho bien o derecho", "Autorización de acreditado y propietario levantamiento pignoración", 15 },
                    { 354, "COMU-48", "COMU", "Relación de comunicaciones emitidas o recibidas por las diversas entidades no definidas de forma mas detallada en la presente clasificación.", "Otra comunicación", 15 },
                    { 355, "COMU-51", "COMU", "Documento que registra las comunicaciones de carácter confidencial mantenidas entre Sareb y las cedentes o servicers, relativas a los temas de prevención de blanqueo de capitales y financiación del terrorismo.", "PBC: comunicación confidencial cedente / servicer", 15 },
                    { 356, "COMU-57", "COMU", "Nota o declaración en la que se manifiesta la negativa a involucrarse en una operación debido a la vinculación del interviniente respecto a un interesado.", "Comunicación abstención", 15 },
                    { 357, "COMU-58", "COMU", "Documento que manifiesta la tenencia y disponibilidad de la documentación solicitada", "Manifiesto disponibilidad documentación", 15 },
                    { 358, "COMU-59", "COMU", "Carta emitida por Sareb/Servicer a través de la cual se confirma la reserva del activo a favor de un tercero y se comunica la suspensión de la comercialización", "Carta reserva y suspensión comercialización", 15 },
                    { 359, "COMU-60", "COMU", "Carta emitida por Sareb/Servicer a través de la cual se comunica al tercero que realizó la reserva la extensión del plazo de  la misma.", "Carta extensión plazo reserva", 15 },
                    { 360, "COMU-61", "COMU", "Carta emitida por Sareb/Servicer a través de la cual se comunica al tercero que realizó la reserva la resolución anticipada de la reserva firmada.", "Carta resolución anticipada reserva", 15 },
                    { 361, "COMU-62", "COMU", "Relación de comunicaciones giradas entre las partes o entre estas y terceros relacionadas con el contrato de arrendamiento", "Alquiler: comunicación / declaración", 15 },
                    { 362, "COMU-63", "COMU", "Documento a través del cual, un tercero, autoriza la consulta de ficheros generados por organismos públicos o privados referentes a este. Generalmente ficheros de solvencia patrimonial. (RAI/ ASNEF/ CIRBE)", "Autorización consulta ficheros", 15 },
                    { 363, "COMU-64", "COMU", "Documento que contiene la comunicación realizada por el Coordinador de Seguridad y salud, a través del cual, se informa del inicio o finalización de la coordinación de las labores de seguridad y salud referentes a la ejecución de una obra en concreto", "Seguridad y salud: comunicación inicio/ finalización coordinación", 15 },
                    { 364, "COMU-65", "COMU", "Comunicación realizada por el registro de la propiedad confirmando la incripción de un derecho real de garantía", "Manifiesto de hipoteca inscrita", 15 },
                    { 365, "COMU-66", "COMU", "Documento (orden de domiciliación, mandato,…) a través del cual un cliente comunica a su entidad financiera la autorización, de acuerdo a la normativa SEPA, del cargo en cuenta de adeudos concretos.", "Autorización cargo en cuenta", 15 },
                    { 366, "COMU-67", "COMU", "Escrito remitido al Registrador de la Propiedad para que inicie el procedimiento de calificación y, llegado el caso, inscripción de un derecho real concreto sobre una finca incluida dentro de su circunscripción (p.e. inscripción de un cambio de titularidad o de una carga, incorporación o modificación de aspectos referentes a la descripción de la finca, de un levantamiento de cargas,…)", "Registro Propiedad: instancia", 15 },
                    { 367, "COMU-68", "COMU", "Documento que muestra el resultado obtenido de la comprobación del interviniente en la operación en las listas de Factiva", "Evidencia de comprobación en FACTIVA", 15 },
                    { 368, "COMU-69", "COMU", "Documento a través del cual el  interesado o cliente autoriza el tratamiento de los datos personales aportados por éste o generados como consecuencia del desarrollo de una operación y se pone de manifiesto, entre otros, el responsable del  tratamiento de dichos datos, los derechos del interesado,…", "Proteccion de datos: Consentimiento", 15 },
                    { 369, "COMU-70", "COMU", "Documento por el cual el depositario comunica los saldos de las garantias financieras", "Saldo de garantias financieras: Comunicacion depositarios", 15 },
                    { 370, "COMU-71", "COMU", "Documento   que registra  las gestiones realizadas   para la obtención de los saldos u otro dato relativo a  las garantías financieras", "Garantias financieras: Comunicación gestiones", 15 },
                    { 371, "COMU-72", "COMU", "Comunicación justificativa  al ofertante/proponente en el  que se le da la posibilidad de presentar una nueva oferta o propuesta", "Faculatad emisión nueva oferta / propuesta: comunicación", 15 },
                    { 372, "COMU-73", "COMU", "Comunicación relativa al ejercicio o derecho de los derechos de adquisión preferente ( tanto/retracto) en la transmisión de Activos", "Derecho de adquisición preferente: comunicación", 15 },
                    { 373, "COMU-74", "COMU", "Acto de comunicación procesal dirigido a autoridades no judiciales para la práctica de embargos judiciales y determinadas diligencias de investigación", "Ejeción: oficios de embargo y/o exhortos", 15 },
                    { 374, "COMU-75", "COMU", "Comunicación de la existencia del procedimiento a la persona a cuyo favor resulte practicada la última inscripcióm de dominio en el registro de la propiedad, con el fin de que pueda intervenir en la ejecución o satisfacer antes del remate el importe del crédito y los intereses y costas, o a los acreedores de  cargas o derechos reales posteriores a la hipoteca", "Ejecución: comunicación de la ejecución al titular inscrito y acreedores posteriores", 15 },
                    { 375, "COMU-76", "COMU", "Notificaciones/ comunicaciones realizadas por el letrado de la administración de justicia a autoridades no judiciales y a otros funcionarios", "Oficios, mandamientos y exhortos", 15 },
                    { 376, "COMU-77", "COMU", "Documento emitido por el letrado de la administración de justicia para ordenar el libramiento de certificaciones, testimonios y la práctica de cualquier actuación cuya ejecución corresponda a los registradores de la propiedad, notarios o funcionarios de la administración de justicia, como mandamientos de pago, mandamiento de anotación de embargos en el Registro de la Propiedad", "Mandamiento de pago", 15 },
                    { 377, "COMU-78", "COMU", "Documento que acredita la entrega o recepción  de documentación de gestorías  para la tramitación de procesos de inscripción", "Recibí de gestoría inscripción título", 15 },
                    { 378, "COMU-79", "COMU", "Visto bueno del coordinador de seguridad y salud al inicio de las Obras", "Seguridad y salud: comunicación autorización inicio de obras", 15 },
                    { 379, "COMU-80", "COMU", "Documentación relativa a la solicitud realizada ante la administración para la obtención de una licencia de  obras", "Licencia obras: Solicitud/instancia", 15 },
                    { 380, "COMU-81", "COMU", "Documentación relativa a la solicitud realizada ante la administración para la obtención de una licencia de  apertura y actividad", "Licencia de Actividad y apertura: Solicitud/instancia", 15 },
                    { 381, "COMU-82", "COMU", "Documentación relativa a la solicitud realizada ante la administración para la obtención de una licencia de  primera ocupación", "Licencia primera ocupación: Solicitud/instancia", 15 },
                    { 382, "COMU-83", "COMU", "Documentación relativa a la solicitud realizada  para la obtención del certificado final de obra", "Certificado final de obra: Solicitud / instancia", 15 },
                    { 383, "COMU-84", "COMU", "Documentación relativa a la solicitud realizada  para la obtención de una cédula de habitabilidad", "Cedula de habitabilidad: Solicitud/Instancia", 15 },
                    { 384, "COMU-85", "COMU", "Documento emitido por la adminisitación pública   por el que se describe la afección de un suelo  a cotos o derehos de caza", "Servidumbre y afección: caza", 15 },
                    { 385, "COMU-86", "COMU", "Documento emitido por la adminisitación pública   por el que se describe la afección de un suelo  a derechos  o deberes derivados de políticas agrarias (por ejemplo la P.A.C)", "Servidumbre y afección: agraria", 15 },
                    { 386, "COMU-87", "COMU", "Burofax remitido informando de la finalización del contrato", "Alquiler: Burofax finalización contrato", 15 },
                    { 387, "COMU-88", "COMU", "Comunicación remitida informando la subrogación en un contrato de alquiler en vigor", "Alquiler: carta subrogación en contrato", 15 },
                    { 388, "COMU-89", "COMU", "Burofax recibido por Sareb relativo a una operación de alquiler", "Alquiler: burofax recibido Sareb", 15 },
                    { 389, "COMU-90", "COMU", "Comunicaciones con la administración pública para cerciorarse de la disponibilidadde tener depositado un aval o una fianza en la misma", "Alquiler: consulta/respuesta disponibilidad de depósito de aval/fianza en organismo público", 15 },
                    { 390, "CONV-01", "CONV", "Escrito de llamamiento o citación a señalando el día, hora y lugar para que concurran a un acto o encuentro de la Asamblea de la Entidad correspondiente.", "Asamblea y/o consejo: convocatoria asistencia", 16 },
                    { 391, "CONV-02", "CONV", "Comunicación de la fecha y lugar en la que tendrá lugar la junta de acreedores.", "Convenio acreedores: convocatoria asistencia junta", 16 },
                    { 392, "CORR-01", "CORR", "Comunicación a través del servicio ordinario.", "Carta correo ordinario", 17 },
                    { 393, "CORR-02", "CORR", "Comunicación a través de medios telemáticos.", "Correo electrónico", 17 },
                    { 394, "CORR-03", "CORR", "Contacto a través de línea telefónica.", "Llamada telefónica", 17 },
                    { 395, "CUAD-01", "CUAD", "Documento a través del cual se detalla la planificación, en actividades y tiempos, definida para la ejecución de una o varias actividades contenidas en un contrato.", "Contrato: planing", 18 },
                    { 396, "CUAD-02", "CUAD", "Documento que expresa la evolución en el tiempo de un préstamo, con indicación de Cuota de pago, Cuota de Amortización , Cuota de intereses, Capital Amortizado y Capital Pendiente.", "Cuadro amortización", 18 },
                    { 397, "CUAD-03", "CUAD", "Documento de trabajo en el que se reflejan los cálculos realizados para el análisis la tarea que se este tratando.", "Cuadro cálculos", 18 },
                    { 398, "CUAD-04", "CUAD", "Documento que expresa el reparto de una carga hipotecaria y su asignación individual a cada bien dividido del principal. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Cuadro distribución responsabilidad hipotecaria", 18 },
                    { 399, "CUAD-05", "CUAD", "Documento en el que se establecen los porcentajes de participación que cada inmueble tendrá respecto al resto de la edificación , todos los inmuebles de la promoción deben sumar el 100%.", "Cuadro reparto coeficientes propiedad", 18 },
                    { 400, "CUAD-06", "CUAD", "Cuadro que expresa el reparto de costes de una construcción , obra o servicio.", "Cuadro reparto costes", 18 },
                    { 401, "CUAD-07", "CUAD", "Cuadro de superficies de las distintas unidades incluidas en la promoción (tanto útil como construida) visado y firmado por la dirección facultativa.", "Cuadro superficies", 18 },
                    { 402, "CUAD-08", "CUAD", "Documento de análisis orientado a determinar la rentabilidad del proyecto, obtenida a partir de la previsión de ventas ajustadas al mercado en el que está ubicado el activo y tipología de unidades resultantes definidos, de la estimación realista de los costes y gastos necesarios para la consumación y entrega en condiciones del producto inmobiliario definido.", "Estudio viabilidad: cuadro económico", 18 },
                    { 403, "CULC-01", "CULC", "Cuenta que contiene la distribución a prorrata entre todos las personas adjudicatarias de fincas resultantes de los gastos reales de urbanización una vez se ha concluido ésta.", "Cuenta de liquidación definitiva", 19 },
                    { 404, "CULC-02", "CULC", "Relación de documentación relativa a la situación patrimonial, económica y financiera de la comunidad.", "Derramas y cuotas: balance situación y cierre cuentas anuales", 19 },
                    { 405, "CULC-03", "CULC", "Informes que se preparan a partir de los saldos de los registros contables, y presentan diversos aspectos de la situación financiera, resultados y flujos de efectivo de una empresa, de conformidad con principios de contabilidad generalmente aceptados.", "Estados financieros", 19 },
                    { 406, "CULC-04", "CULC", "Documentos contables que muestran, de acuerdo a lo definido legalmente, la situación patrimonial o los resultados de una empresa a una fecha o período determinado para su remisión al Registro Mercantil correspondiente. Dentro de éstas se incorpora el balance de situación, la cuenta de pérdidas y ganancias y estado de evolución de patrimonio neto, estado de flujos de efectivo, impacto ambiental, informe de gestión, informe de autocartera, ...", "Cuentas anuales: Información contable e informes", 19 },
                    { 407, "CULC-05", "CULC", "Resumen contable en el que se muestran todos los activos y pasivos en un momento o periodo contable determinado.", "Balance de situación", 19 },
                    { 408, "DEAC-01", "DEAC", "Documento en el que se especifica el acuerdo sobre alteración de los datos catastrales.", "Catastro: acuerdo alteración", 20 },
                    { 409, "DEAC-02", "DEAC", "Documento en el que se refleja la cesión que hace una entidad de derecho público a un particular de la gestión de un servicio público o el disfrute exclusivo de un dominio público durante un plazo determinado.", "Concesión administrativa", 20 },
                    { 410, "DEAC-03", "DEAC", "Acuerdo de asociación de los propietarios de los terrenos objeto de una actuación urbanística a fin de acometer la ejecución de las obras de urbanización de acuerdo a la normativa urbanística aplicable.", "Constitución: acuerdo", 20 },
                    { 411, "DEAC-04", "DEAC", "Acuerdo de cese de la actividad de la Junta y su extinción por haber cumplidos los objetivos por lo que se constituyó.", "Disolución: decreto", 20 },
                    { 412, "DEAC-05", "DEAC", "Escrito exposición urgencia en un proceso expropiatorio.", "Expropiación: declaración de urgencia y acuerdo necesidad ocupación", 20 },
                    { 413, "DEAC-06", "DEAC", "Resolución administrativa a través de la cual la administración actuante autoriza, previa solicitud de la entidad gestora, la ejecución simultánea de las obras de urbanización y de edificación en una actuación urbanística.", "Obra: decreto simultaneidad obras urbanización y edificación", 20 },
                    { 414, "DEAC-07", "DEAC", "Comunicación por la que se pone de manifiesto el mal estado de una instalación y se ordena de su reparación.", "Orden reparación", 20 },
                    { 415, "DEAC-08", "DEAC", "Acuerdo sobre alteración de los datos de las parcelas declaradas por los agricultores y ganaderos, en cualquier régimen de ayudas relacionado con la superficie cultivada o aprovechada por el ganado.", "Sigpac: acuerdo alteración", 20 },
                    { 416, "DEAC-09", "DEAC", "Documento que certifica la recepción por parte del ayuntamiento y/o promotor, de las obras por haberse ejecutado conforme al proyecto presentado. En el caso de las obras de urbanización, supone la entrega de los derechos y obligaciones de su gestión y/o conservación.", "Obra: decreto recepción definitiva", 20 },
                    { 417, "DEAC-10", "DEAC", "Documento que certifica por parte del ayuntamiento y/o promotor de las obras la recepción de fases de la obra terminadas (recepción parcial) sujeta a posterior recepción definitiva (provisional) o recepción con determinadas deficiencias pendientes de subsanar (con reservas)", "Obra: decreto recepción parcial, provisional o con reserva", 20 },
                    { 418, "DECL-01", "DECL", "Documentación y formularios presentados al catastro a través de los cuales se solicita la alteración del inmueble, más concretamente: 900D.- Modelo que desde el 06/12/2018 se utilizará para declarar cualquier alteración catastral que hasta dicha fecha se declaraban en los modelos que siguen: 901.-Formulario para realizar la alteración catastral o la cuota de participación en un inmueble 902.- Formulario para realizar la alteración catastral en el Ayuntamiento, asigna referencia catastral a cada uno de los inmuebles edificados una vez realizada la división horizontal 903.-Agregación, agrupación, segregación o división de bienes inmuebles 904.-Cambio de cultivo o aprovechamiento, cambio de uso o demolición o derribo de bienes inmuebles", "Catastro: declaración alteración (MOD 901, 902, 903, 904, 900D)", 21 },
                    { 419, "DECL-02", "DECL", "Documento obligatorio de información que deben realizar todas las personas físicas o jurídicas, en la que habrán de relacionar las empresas con quienes hayan efectuado operaciones cuyo volumen durante el año natural correspondiente haya superado las 3.005,06 Euros.", "Declaración operación con tercero o inversión", 21 },
                    { 420, "DECL-03", "DECL", "Modelo de la liquidación trimestral del IGIC.", "Impuesto general indirecto canario (IGIC): declaración", 21 },
                    { 421, "DECL-04", "DECL", "Documento acreditativo del pago del impuesto correspondiente en el país de origen de la persona implicada en la operación.", "Impuesto país origen", 21 },
                    { 422, "DECL-05", "DECL", "Tributo de carácter directo y naturaleza personal que grava el patrimonio neto de las personas físicas, en los términos previstos en en la Ley.", "Impuesto patrimonio", 21 },
                    { 423, "DECL-06", "DECL", "Documento modelo que acredita la presentación ante la Agencia Tributaria del IRPF del ejercicio correspondiente", "Impuesto renta personas físicas (IRPF): declaración", 21 },
                    { 424, "DECL-07", "DECL", "Documento en el que se reflejan las cantidades que se detraen al contribuyente por el pagador de determinadas rentas, por estar así establecido en la ley, para ingresarlas en la Administración tributaria como “anticipo” de la cuota del Impuesto que el contribuyente ha de pagar", "Impuesto renta personas físicas (IRPF): retención", 21 },
                    { 425, "DECL-08", "DECL", "Documento modelo que acredita la presentación ante la Agencia Tributaria del impuesto que grava la renta obtenida en un año natural por las personas jurídicas residentes en España o contribuyentes.", "Impuesto sociedades: declaración", 21 },
                    { 426, "DECL-09", "DECL", "Documento modelo que acredita la presentación ante la Agencia Tributaria del impuesto que impuesto que grava el valor añadido o agregado de un producto en las distintas fases de su producción.", "Impuesto valor añadido (IVA): declaración", 21 },
                    { 427, "DECL-10", "DECL", "Documento por el cual el adjudicatario renuncia a la exención de IVA y evitar la tributación por actos jurídicos documentos.", "Impuesto Valor Añadido (IVA): renuncia a la exoneración del impuesto", 21 },
                    { 428, "DOCA-01", "DOCA", "Documentación correspondiente a la AEAT y no contemplada en otra tipología.", "AEAT: otra documentación", 22 },
                    { 429, "DOCA-02", "DOCA", "Documento administrativo, no detallado en la clasificación, relacionado con el catálogo de bienes y espacios protegidos, entendiéndose por esto la relación de inmuebles, incluida en el PGOU, sujetos a protección en virtud de la legislación reguladora del patrimonio histórico y artístico y los merecedores de protección en atención a sus valores y por razón urbanística, e incorpora el régimen de protección para su preservación.", "Catálogo de bienes y espacios protegidos: otro documento administrativo", 22 },
                    { 430, "DOCA-03", "DOCA", "Escrito por el que pone en conocimiento de la autoridad judicial o policial de un delito o pretensión jurídica.", "Denuncia", 22 },
                    { 431, "DOCA-04", "DOCA", "Documentación relativa a la solicitud realizada por la entidad gestora ante la administración actuante referente al del inicio de la vía de apremio contra el participe (titular de los terrenos adherido) moroso.", "Derramas y cuotas: exacción vía apremio", 22 },
                    { 432, "DOCA-05", "DOCA", "Documento administrativo relacionado con el estudio arqueológico que no se encuentra especificado en otro apartado de la clasificación definida.", "Estudio arqueológico: otro documento administrativo", 22 },
                    { 433, "DOCA-06", "DOCA", "Documento administrativo relacionado con el estudio de detalle que no se encuentra especificado en otro apartado de la clasificación definida.", "Estudio de detalle: otro documento administrativo", 22 },
                    { 434, "DOCA-07", "DOCA", "Documento administrativo relacionado con el estudio geotécnico que no se encuentra especificado en otro apartado de la clasificación definida.", "Estudio geotécnico: otro documento administrativo", 22 },
                    { 435, "DOCA-08", "DOCA", "Documento administrativo relacionado con el estudio topográfico que no se encuentra especificado en otro apartado de la clasificación definida.", "Estudio topográfico: otro documento administrativo", 22 },
                    { 436, "DOCA-09", "DOCA", "Depósito de dinero a cuenta del valor final de lo expropiado o de la indemnización por rápida ocupación.", "Expropiación: depósito previo a la ocupación e indemnización por rápida ocupación", 22 },
                    { 437, "DOCA-10", "DOCA", "Documento procesal que deja constancia la consignación en la Caja general de depósitos del justiprecio.", "Expropiación: diligencia consignación justiprecio", 22 },
                    { 438, "DOCA-11", "DOCA", "Acuerdo adoptado por el jurado de expropiación", "Expropiación: resolución jurado", 22 },
                    { 439, "DOCA-12", "DOCA", "Documento administrativo relacionado con las normas complementarias que no se encuentra especificado en otro apartado de la clasificación definida.", "Normas complementarias: otro documento administrativo", 22 },
                    { 440, "DOCA-13", "DOCA", "Documento administrativo relacionado con las normas subsidiarias que no se encuentra especificado en otro apartado de la clasificación definida.", "Normas subsidiarias: otro documento administrativo", 22 },
                    { 441, "DOCA-14", "DOCA", "Cualquier otro documento del plan calidad urbanística no contemplado en otras clasificaciones.", "Plan control calidad urbanización: otro documento administrativo", 22 },
                    { 442, "DOCA-15", "DOCA", "Documento administrativo relacionado con el proyecto que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto de delimitación de suelo urbano: otro documento administrativo", 22 },
                    { 443, "DOCA-16", "DOCA", "Documento administrativo relacionado con el plan de sectorización/delimitación que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan de sectorización/delimitación: otro documento administrativo", 22 },
                    { 444, "DOCA-17", "DOCA", "Documento administrativo relacionado con el plan de singular interés que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan de singular interés: otro documento administrativo", 22 },
                    { 445, "DOCA-18", "DOCA", "Documento administrativo relacionado con el plan de reforma interior que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan especial de reforma interior: otro documento administrativo", 22 },
                    { 446, "DOCA-19", "DOCA", "Documento administrativo relacionado con el plan especial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan especial: otro documento administrativo", 22 },
                    { 447, "DOCA-20", "DOCA", "Documento administrativo relacionado con el plan general de ordenación territorial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan general de ordenación territorial: otro documento administrativo", 22 },
                    { 448, "DOCA-21", "DOCA", "Documento administrativo relacionado con el plan que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan general: otro documento administrativo", 22 },
                    { 449, "DOCA-22", "DOCA", "Documento redactado por el técnico competente en el que se especifica el tratamiento que se le tendrá que dar, de acuerdo a la normativa aplicable, a los residuos de construcción y demolición que se generarán durante la ejecución de la obra.", "Plan gestión residuos: otro documento administrativo", 22 },
                    { 450, "DOCA-23", "DOCA", "Documento administrativo relacionado con el plan parcial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan parcial: otro documento administrativo", 22 },
                    { 451, "DOCA-24", "DOCA", "Documento redactado por el técnico competente en el que se especifica, de acuerdo a la normativa aplicable, el que se establecen las condiciones mínimas de seguridad y salud que deberán tenerse presentes durante la ejecución de las obras.", "Plan seguridad y salud: otro documento administrativo", 22 },
                    { 452, "DOCA-25", "DOCA", "Documento administrativo relacionado con el programa de actuación que no se encuentra especificado en otro apartado de la clasificación definida.", "Programa de actuación: otro documento administrativo", 22 },
                    { 453, "DOCA-26", "DOCA", "Documento administrativo relacionado con el proyecto de actuación especial que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto actuación especial: otro documento administrativo", 22 },
                    { 454, "DOCA-27", "DOCA", "Documento administrativo relacionado con el proyecto de calificación urbanística que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto calificación urbanística: otro documento administrativo", 22 },
                    { 455, "DOCA-28", "DOCA", "Documento emitido por la administración actuante a través de la cual se certifica la aprobación del proyecto de equidistribución y se incluye el texto concreto del proyecto de lo aprobado para su inscripción en el registro de la propiedad.", "Proyecto equidistribución: certificación aprobación", 22 },
                    { 456, "DOCA-30", "DOCA", "Documento administrativo relacionado con el proyecto de equidistribución que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto equidistribución: otro documento administrativo", 22 },
                    { 457, "DOCA-31", "DOCA", "Documento administrativo relacionado con el proyecto de expropiación que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto expropiación: otro documento administrativo", 22 },
                    { 458, "DOCA-32", "DOCA", "Documento administrativo relacionado con el proyecto de parcelación / segregación / agrupación que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto parcelación / segregación / agrupación: otro documento administrativo", 22 },
                    { 459, "DOCA-33", "DOCA", "Documento administrativo relacionado con el proyecto de urbanización que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto urbanización: otro documento administrativo", 22 },
                    { 460, "DOCA-34", "DOCA", "Medio de impugnación de un acto administrativo a través del cual el superior jerárquico al órgano que lo dictó revisa la resolución recurrida agotando la vía administrativa y dejando abierto el cauce jurisdiccional.", "Recurso alzada", 22 },
                    { 461, "DOCA-35", "DOCA", "Recurso presentado a través del cual se impugna una resolución ante el tribunal que la dictó", "Recurso reposición", 22 },
                    { 462, "DOCA-36", "DOCA", "Es un recurso extraordinario, que procede tan sólo contra actos firmes en vía administrativa y, que los motivos de impugnación están tasados en la Ley.", "Recurso revisión", 22 },
                    { 463, "DOCA-37", "DOCA", "Cualquier documento que afecte al Impuesto de Sucesiones no contemplado en otra clasificación.", "Sucesiones: otro documento", 22 },
                    { 464, "DOCA-38", "DOCA", "Orden dictada por una entidad pública referente a la adjudicación de un activo como consecuencia de la ejecución de un título por incumplimiento del deudor.", "Resolución administrativa de adjudicación y/o cargas", 22 },
                    { 465, "DOCA-39", "DOCA", "Documento en el que se establecen las distintas condiciones que regiran la adjudicación de una vivienda de protección en una actuación concreta.", "Vivienda de protección: pliego de condiciones", 22 },
                    { 466, "DOCA-40", "DOCA", "Conjunto de documentación relativa al desarrollo del convenio firmado. Dentro de este apartado se incorporarán, entre otros, los  requerimientos realizados por la administración respecto al cumplimiento de las obligaciones dimanantes del convenio, las comunicaciones giradas entre las partes,…", "Contrato: otra documentación convenio", 22 },
                    { 467, "DOCA-41", "DOCA", "Conjunto de documentación referida a la normativa y el planeamiento urbanístico aplicable al colateral. Para la documentación de normativa y planeamiento aplicable a un Reo utilicesé aquellos tipos documentales identificados en las series 07 (normas e instrumentos urbanísticos), 08 (Suelo) del cuadro de AAII.", "Documentación de normativa y planeamiento urbanístico", 22 },
                    { 468, "DOCA-42", "DOCA", "Notificación de  acto administrativo por el que se retienen bienes o derechos  para el pago de deudas", "Resolución embargo", 22 },
                    { 469, "DOCA-43", "DOCA", "Notificaciones de actos administrativos, como consecuencia de procedimientos de cambio de uso, declaración fuera de ordenación o perdida de edificabilidad. Incluye alegaciones Sareb", "Expediente Administrativo: Cambio de uso, fuera de ordenación o perdida de edificabilidad", 22 },
                    { 470, "DOCA-44", "DOCA", "Notificaciones de actos administrativos, como consecuencia de otros procedimientos administrativos.", "Expediente Administrativo: Sancionador, multa coercitiva", 22 },
                    { 471, "DOCA-45", "DOCA", "Notificacines de actos administrativos, como consecuencia de procedimientos para la restauración de la legalidad. Incluye alegaciones Sareb", "Expediente Administrativo: Restauración Legalidad", 22 },
                    { 472, "DOCA-46", "DOCA", "Notificaciones de actos administrativos de declaración o inscripción de Activos en registros públicos. Incluye alegaciones Sareb", "Expediente Administrativo: Inscripción en registros públicos o declaración bien de interés artístico y protección cultural", 22 },
                    { 473, "DOCA-47", "DOCA", "Notificaciones de actos administrativos o alegaciones en otros procedimientos adminitrativos", "Expediente Administrativo: Otro", 22 },
                    { 474, "DOCA-48", "DOCA", "Resolución administrativa que inicia el expediente administrativo", "Expediente administrativo: inicio", 22 },
                    { 475, "DOCA-49", "DOCA", "Resolución administrativa a las partes implicadas en el expediente, incluida cualquier forma legal oportuna, edictos, publicación Boe u otra publicación", "Expediente administrativo: Notificación a las partes", 22 },
                    { 476, "DOCA-50", "DOCA", "Trámite dirigido a comprobar hechos no demostrados por las partes en un proceso o aclarar las discrepancias existentes entre ellas.", "Práctiva prueba- Admisión-Inadmisión", 22 },
                    { 477, "DOCA-51", "DOCA", "Resolución de la Administración por la cual se fija el día y hora para comparecer o realizar determinado trámite , vista, práctica prueba, audiencia", "Expediente administrativo: Audiencia", 22 },
                    { 478, "DOCA-52", "DOCA", "Resolución dictada por la Administración que finaliza el expediente debiendo ser perfectamente motivada", "Fin: Resolución, desistimieto o renuncia", 22 },
                    { 479, "DOCA-53", "DOCA", "Documentación varia de comunidades de propietarios", "Documentación de comunidades de propietarios", 22 },
                    { 480, "DOCA-54", "DOCA", "Documentación varia de juntas de compensación", "Documentación de juntas de compensación", 22 },
                    { 481, "DOCA-55", "DOCA", "Documentación relativa al pago aplazado", "Documentacion del pago aplazado", 22 },
                    { 482, "DOCI-01", "DOCI", "Documento nacional de identidad expedido en España.", "Documento identidad persona física (DNI / NIE)", 23 },
                    { 483, "DOCI-02", "DOCI", "Documento de identidad expedido por el Ministerio de Asuntos Exteriores y de Cooperación para el personal de las representaciones diplomáticas y consulares de terceros países en España.", "Documento identidad Ministerio de Asuntos Exteriores", 23 },
                    { 484, "DOCI-03", "DOCI", "Documento, carta o tarjeta, expedida por los autoridades competentes del país de origen o de procedencia, que acredite la identidad de los interesados, expedida por las autoridades competentes del país de origen o de procedencia.", "Documento oficial identidad (país de origen UE)", 23 },
                    { 485, "DOCI-04", "DOCI", "Documento expedido por el Ministerio de Justicia de España para registrar la relación de parentesco entre padres e hijos. Se anotan los nacimientos, adopciones, defunciones, separaciones y divorcios. En el caso de que los titulares se divorcien y tengan hijos con otras parejas se expide un nuevo libro para acreditar esa nueva relación", "Libro de familia", 23 },
                    { 486, "DOCI-05", "DOCI", "Documento con validez internacional, que identifica a su titular acreditando su identidad y nacionalidad, expedido por las autoridades de su respectivo país.", "Pasaporte", 23 },
                    { 487, "DOCI-06", "DOCI", "Documento único y exclusivo destinado a dotar de documentación a los extranjeros en situación de permanencia legal en España. Dicha tarjeta acredita la permanencia legal de los extranjeros en España, su identificación y que se ha concedido autorización para permanecer en España por tiempo superior a 6 meses.", "Tarjeta de residencia", 23 },
                    { 488, "DOCI-07", "DOCI", "Documento de naturaleza fiscal en el que se especifíca el código asignado por la Agencia Tributaria a una persona física o jurídica", "Tarjeta identificación fiscal (NIF /CIF)", 23 },
                    { 489, "DOCI-08", "DOCI", "Documento a través del cual se atestigua que una persona, física o jurídica, forma parte, o cumple los parámetros establecidas para formar parte, de un colectivo, asociación, agrupación, colegio o similar. Por ejemplo: pertenencia a grupos con especiales dificultades para la inserción sociolaboral", "Acreditación pertenencia colectivo", 23 },
                    { 490, "DOCJ-01", "DOCJ", "Escrito incorporado al procedimiento través del cual la parte demandada alega todas sus excepciones y defensas respecto al incidente concursal instado por la otra parte (p.e. recusación de administradores, anulación de actos del deudor, acción rescisoria, oposición calificación concurso,…)", "Incidente concursal: contestación a la demanda", 24 },
                    { 491, "DOCJ-02", "DOCJ", "Escrito incorporado al procedimiento través del cual la parte demandante solicita al juzgado de lo mercantil que dirima sobre una cuestión concreta que deberá ser resulta durante el concurso y de la cual no hay en la ley concursal especificado procedimiento concreto (p.e. recusación de administradores, anulación de actos del deudor, acción rescisoria, oposición calificación concurso,…)", "Incidente concursal: Demanda", 24 },
                    { 492, "DOCJ-03", "DOCJ", "Inventario y Lista de Acreedores Definitiva de la Administración Concursal", "Administración concursal: lista definitiva de bienes derechos y créditos", 24 },
                    { 493, "DOCJ-04", "DOCJ", "Escrito de los acreedores denunciando el incumplimiento del Convenio.", "Convenio acreedores: acción judicial por incumplimiento", 24 },
                    { 494, "DOCJ-05", "DOCJ", "Escrito presentado por abogado y procurador ante la Jurisdicción de lo Civil en defensa de un derecho vulnerado", "Demanda civil", 24 },
                    { 495, "DOCJ-06", "DOCJ", "Escrito presentado por abogado y procurador ante la Jurisdicción de lo Civil instando el desalojo de un inmueble.", "Desahucio: demanda", 24 },
                    { 496, "DOCJ-07", "DOCJ", "Documentación que afecta la instrucción del procedimiento de desahucio", "Desahucio: instrucción del procedimiento", 24 },
                    { 497, "DOCJ-08", "DOCJ", "Escrito de Demanda, petición, solicitud, ampliación  en cualquier procedimiento jurídico para ejercitar una reclamación civil o solicitar al tribunal que se despache ejecución", "Ejecución: demanda", 24 },
                    { 498, "DOCJ-09", "DOCJ", "Escrito de oposición, contestación a la demanda, contrademanda introduciendo nuevas peticiones frente al demandante o a cualquier documento presentado por las partes en un procedimiento jurídico", "Ejecución: escrito oposición", 24 },
                    { 499, "DOCJ-10", "DOCJ", "Información patrimonial facilitada por el Juzgado y obtenida de Organismos Públicos.", "Ejecución: información judicial patrimonio deudor", 24 },
                    { 500, "DOCJ-11", "DOCJ", "Escrito del Administrador concursal solicitando la resolución del concurso anticipada por insuficiencia de bienes.", "Liquidación concurso: solicitud conclusión anticipada", 24 },
                    { 501, "DOCJ-12", "DOCJ", "Procedimiento especial de reclamación de deudas en la Jurisdicción Civil.", "Monitorio", 24 },
                    { 502, "DOCJ-13", "DOCJ", "Documento en el que se nombra al depositario de un bien.", "Nombramiento depositario", 24 },
                    { 503, "DOCJ-14", "DOCJ", "Acta por la que se decreta la posesión del bien.", "Posesión activo: acta", 24 },
                    { 504, "DOCJ-15", "DOCJ", "Acta a través de la cual el juzgado acuerda el desalojo del inmueble y se ordena a las fuerzas del orden la realización del mismo con presencia de una comitiva judicial y del procurador de la propiedad.", "Posesión activo: acta lanzamiento", 24 },
                    { 505, "DOCJ-16", "DOCJ", "Comunicación de apertura de negociaciones con acreedores previa a la solicitud del concurso Art 5.3 LC.", "Preconcurso: comunicación del deudor al juzgado inicio negociaciones", 24 },
                    { 506, "DOCJ-17", "DOCJ", "Recurso extraordinario que tiene por objeto anular una sentencia judicial que contiene una incorrecta interpretación o aplicación de la Ley o que ha sido dictada en un procedimiento que no ha cumplido las solemnidades legales, es decir por un error in indicando o bien error in procediendo respectivamente.", "Recurso casación", 24 },
                    { 507, "DOCJ-18", "DOCJ", "El recurso contencioso-administrativo es un recurso que se puede interponer contra las disposiciones de carácter general y contra los actos expresos y presuntos de la Administración Pública que pongan fin a la vía administrativa sean definitivos o de trámite.", "Recurso contencioso-administrativo", 24 },
                    { 508, "DOCJ-19", "DOCJ", "Notificación al deudor y, en su caso, fiador del resultado de la liquidación con requerimiento de pago (BUROFAX)", "Requerimiento al deudor", 24 },
                    { 509, "DOCJ-20", "DOCJ", "Documento en el que se detalla la consignación del precio de un activo incluido en una subasta.", "Subasta: consignación precio", 24 },
                    { 510, "DOCJ-21", "DOCJ", "Escrito presentado ante el Juzgado en el que se solicita el inicio del procedimiento de Subasta.", "Subasta: solicitud", 24 },
                    { 511, "DOCJ-22", "DOCJ", "Demanda de un tercero que alega poseer dominio de un bien en ejecución", "Tercería de dominio: demanda", 24 },
                    { 512, "DOCJ-23", "DOCJ", "Documento a través del cual la propiedad del activo da noticia a la autoridad competente de la comisión de un delito de usurpación, entendiendo como tal, el acto a través del cual el denunciado ocupa un inmueble o derecho real inmobiliario, propiedad del denunciante, con violencia o intimidación o también aquel que sin autorización de la propiedad ocupare un inmueble o se mantuviera en éste contra la voluntad de su titular.", "Usurpación: denuncia", 24 },
                    { 513, "DOCJ-24", "DOCJ", "Documentación asociada al conjunto de actos por medio de los cuales se aportan al órgano decisorio los elementos de juicio necesarios para que dicte resolución sobre el asunto en cuestión.", "Usurpación: instrucción del procedimiento", 24 },
                    { 514, "DOCJ-25", "DOCJ", "Comunicación, en el ambito de Blanqueo de Capitales, al Servicio Ejecutivo de la imposibilidad de evitar la ejecucion de un bien o derecho sobre el que se sospecha relacion con Blanqueo de Capitales (Art 19 Ley 10/10)", "Comunicación de abstención de ejecución", 24 },
                    { 515, "DOCJ-26", "DOCJ", "Escrito mediante el cual se interpone un recurso contra una resolución judicial.", "Impugnación: escrito", 24 },
                    { 516, "DOCJ-27", "DOCJ", "Documento mediante el cual se pretende impugnar la resolución dictada por un tribunal", "Recurso apelación: Interposición", 24 },
                    { 517, "DOCJ-28", "DOCJ", "Documentación de un procedimiento judicial cuya tipología documental no se encuentra contemplada en la presenta clasificación.", "Otra documentación del procedimiento", 24 },
                    { 518, "DOCJ-29", "DOCJ", "Documento judicial mediante el cual, el órgano judicial, aprueba la admisión a trámite de la demanda presentada", "Decreto admisión a trámite demanda", 24 },
                    { 519, "DOCJ-30", "DOCJ", "Documento judicial a través del cual se recoge el acta de la visita realizada.", "Desahucio: acta", 24 },
                    { 520, "DOCJ-31", "DOCJ", "Resolución formal o material del proceso a través del cual el letrado de la administración de justicia reconoce la firmeza del decreto judicial de adjudicación resultante de la tramitación de un procedimiento de ejecución hipotecaria", "Subasta: diligencia de ordenación firmeza", 24 },
                    { 521, "DOCJ-32", "DOCJ", "Escrito de solicitud al juzgado solicitando el señalamiento para la toma de posesión/lanzamiento", "Posesión activo: escrito solicitud", 24 },
                    { 522, "DOCJ-33", "DOCJ", "Escrito de solicitud al juzgado solicitando la expedición o subsanación de documentación (p.e. expedición de Mandamientos de Cancelación de Cargas o subsanaciones al mismo)", "Subasta: solicitud expedición/subsanación", 24 },
                    { 523, "DOCJ-34", "DOCJ", "Escrito presentado por alguna de las partes en el proceso en el que manifiestan no estar de acuerdo con los honorarios, minutas, cuentas presentados por el abogado o procurador por considerarlos elevados o improcedentes, o con la tasación de los intereses y costas presentadas", "Escritos de impugnación de honorario, intereses y costas", 24 },
                    { 524, "DOCJ-35", "DOCJ", "Formalización por la parte recurrida de su oposición a un recurso", "Recurso: impugnación", 24 },
                    { 525, "DOCJ-36", "DOCJ", "Inventario de bienes y derechos integrados en el patrimonio del deurdor", "Administración concursal: inventario de la masa activa", 24 },
                    { 526, "DOCJ-37", "DOCJ", "Escrito presentado por las partes solicitando la cesación temporal del procedimiento.", "Petición de suspensión del proceso", 24 },
                    { 527, "DOCJ-38", "DOCJ", "Escrito presentado por el demandado/ejecutado denunciando la falta de jurisdicción o competencia del tribunal ante el que se ha interpuesto la demanda.", "Escrito de Declinatoria", 24 },
                    { 528, "DOCJ-39", "DOCJ", "Escrito de alegaciones formulado frente a una declinatoria, alegando y aportando lo que consideren conveniente para sostener la jurisdicción y competencia del tribunal.", "Impugnación declinatoria", 24 },
                    { 529, "DOCJ-40", "DOCJ", "Escrito presentado por deudor/acreedores oponiéndose/impugnando  la rendición de cuentas presentada por el administrador concursal", "Administración concursal: Oposición a la rendición de cuentas", 24 },
                    { 530, "DOCJ-41", "DOCJ", "Escrito presentado por las partes solicitando determinada actuación por parte del juzgado", "Escrito requiriendo notificación / libramiento de cédula / oficio", 24 },
                    { 531, "DOCJ-42", "DOCJ", "Escrito de parte solicitando que el proceso se desarrolle y avances en sus distintas fases", "Escrito solicitando Impulso procesal", 24 },
                    { 532, "DOCJ-43", "DOCJ", "Escrito que presentan las partes en el procedimiento con el fin de dar impulso procesal al mismo", "Escrito solicitando sustantación / Resolución de cuestión", 24 },
                    { 533, "DOCJ-44", "DOCJ", "Escrito  mejorando precio más alto para la adquisión del bien", "Subasta: Mejora oferta", 24 },
                    { 534, "DOCJ-45", "DOCJ", "Escrito por el cual se solicita al juzgado que se valide el acuerdo adoptado por empresa deudora y sus acreedores para evitar el concurso.", "Preconcurso: Solicitud de homologación de acuerdos de refinanciación", 24 },
                    { 535, "DOCJ-46", "DOCJ", "Escrito interpuesto por los acreedores que refuta la resolución que homologa el acuerdo de refinanciación", "Preconcurso: Impugnación de la homologación de  los acuerdos de refinanciación", 24 },
                    { 536, "DOCJ-47", "DOCJ", "Escrito interpuesto por los acreedores solicitando al juez la declaración de incumpliento del acuerdo de refinanciación", "Preconcurso: Petición de incumplimiento de acuerdos de refinanciación", 24 },
                    { 537, "DOCJ-48", "DOCJ", "Documento  que, excepcionalmente, pueden presentar las partes legitimadas de un proceso por el que solicitan la nulidad de actuaciones cuando la resolución que pone fin al proceso no sea susceptible de recurso que pueda reparar la indefensión producida. Este escrito puede presentarse sólo por los siguientes motivos: por defectos de forma que les ha causado indefensión y que no hubiera podido denunciar antes de recaer sentencia o resolución equivalente", "Escrito solicitando la nulidad de actuaciones", 24 },
                    { 538, "DOCJ-49", "DOCJ", "Escrito presentado por el deudor, los acreedores o la administración concursal  solicitando la liquidación", "Concurso: Petición apertura fase liquidación", 24 },
                    { 539, "DOCJ-50", "DOCJ", "Documento elaborado por el Ministerio Fiscal  o por la parta acusadora el que determinan los hechos por lo que se acusa, el delito que constituyen los hechos, la autoría de los mismos, atenuantes , agravantes si concurren y la solicitud de pena a imponer.", "Procedimiento abreviado: Escrito de acusación", 24 },
                    { 540, "DOCJ-51", "DOCJ", "Documento elaborado por el abogado del acusado en el que exponen sus conclusiones numeradas y correlativas a las de las acusaciones, manifestando su conformidad o disconformidad en cada una de ellas o mostrando sus puntos de divergencia.", "Procedimiento abreviado: Escrito de defensa", 24 },
                    { 541, "DOCJ-52", "DOCJ", "Escrito que presentan las partes en el procedimiento una vez finalizadas las fases de demanda, constestación y prueba, en el que hacen valoraciones  fácticas y jurídicas de las pretensiones recogidas en la demanda, debatidas en la vista, etc.", "Escrito de conclusiones", 24 },
                    { 542, "DOCJ-53", "DOCJ", "Escrito del demandante, demandado o un tercero, solicitando la comparecencia en el proceso judicial para ejercitar sus derechos", "Escrito de personación", 24 },
                    { 543, "DOCJ-54", "DOCJ", "Escrito solicitando al letrado de la administración de justicia la liquidación de las costas del proceso, una vez firme la sentencia o el auto en el que se hubiese impuesto la condena en costas.", "Petición de tasación de intereses y costas", 24 },
                    { 544, "DOCJ-55", "DOCJ", "Escrito que presentan los acreedores sobre la acción rescisoria solicitada en el procedimiento (dicha acción rescisoria se realiza para garantizar la masa activa, articulo 71 LC)", "Administración concursal: escrito de los acreedores impugnando o ejerciendo la acción rescisoria", 24 },
                    { 545, "DOCJ-56", "DOCJ", "Escrito solicitando la revocación del administración concursal nombrado", "Administración concursal: Escrito solicitando la separación del administrador", 24 },
                    { 546, "DOCJ-57", "DOCJ", "Escrito presentado por el actor en el que manifiesta su voluntad de abandonar la pretensión   (Art  20,2 lec) o por el demandado, montrándose conforme con las pretensiones del actor", "Fin de procedimiento: Escrito de desistimiento, allanamiento, renuncia", 24 },
                    { 547, "DOCJ-58", "DOCJ", "Escrito interpuesto por el abogado o procurador renunciando a acudir al procedimiento de jura de cuentas", "Cuenta abogado y procurador: Renuncia", 24 },
                    { 548, "DOCJ-59", "DOCJ", "Escrito presentado por los acreedores contradiendo la determinación de la masa pasiva presentada por la administración concursal.", "Concurso: Impugnación del reconocimiento de los creditos", 24 },
                    { 549, "DOCJ-60", "DOCJ", "Escrito de alguna de las partes renunciando a la oposición interpuesta  durante el procedimiento de ejecución", "Ejecución: Desistimiento de la operación", 24 },
                    { 550, "DOCJ-61", "DOCJ", "Acto por el cual el inquilino/arrendantario ejerce su derecho de adquisición preferente del bien ejecutado en el proceso de ejecución hipotecaria", "Ejecución: Derecho de tanteo y retracto", 24 },
                    { 551, "DOCJ-62", "DOCJ", "Escrito aceptando o anulando el cargo de guardar/preservar los bienes y/o derechos de contenido económico del patrimonio de los ejecutantes, asegurando así el resultado del proceso", "Medidas cautelares: Aceptación / revocación del depositario o administrador judicial", 24 },
                    { 552, "DOCJ-63", "DOCJ", "Documentación de procedimiento de ejecución( ejecutivo, hipotecario )cuya tipología documental no se encuentra contemplada en la presente clasificación.", "Otra documentación del procedimiento de ejecución", 24 },
                    { 553, "DOCJ-64", "DOCJ", "Escrito presentado por las partes solicitando apartar del proceso al perito designado judicialmente por concurrir determinadas circustancias previstas en la ley", "Ejecución: Recusacion del perito", 24 },
                    { 554, "DOCJ-65", "DOCJ", "Escrito de aceptación del cargo de perito tasador designado por el letrado de la administración de justicia durante la ejecución para valorar los bienes. Aceptado el cargo, elabora el informe con la valoración", "Ejecución: Aceptación y elaboración del informe del perito", 24 },
                    { 555, "DOCJ-66", "DOCJ", "Escrito del ejecutante solicitando al letrado de la administración de justicia alternativas a la subasta, bien la administración judicial para el pago, bien el convenio de realización o venta por persona o entidad especializada", "Ejecución: Petición de las partes solicitando alternativa a la subasta", 24 },
                    { 556, "DOCJ-67", "DOCJ", "Escrito que posibilita comparecer a las personas que no son los ejecutados pero que ocupan el inmueble objeto de ejecución y se ven afectados indirectamente por el procedimiento", "Subasta: Escrito solicitando que se de traslado a los arrendatarios/ ocupantes de la existencia del procedimiento", 24 },
                    { 557, "DOCJ-68", "DOCJ", "Acto del ejecutado ofreciendo garantía pecuniaria para responder en caso de  demora durante la oposición a la ejecución provisional", "Ejecucion: Prestacion caucion", 24 },
                    { 558, "DOCJ-69", "DOCJ", "Escrito que presenta el ejecutado solicitando la no permanencia de los ocupantes en el inmueble objeto de ejecución (éstos alegaron situación de vulnerabilidad)", "Subasta: Peticioón del ejecutante sobre el no derecho de los ocupantes de permanecer en el inmueble (art 661 LEC)", 24 },
                    { 559, "DOCJ-70", "DOCJ", "Escrito presentado por el demandado alegando motivos para que no se realice el lanzamiento del inmueble", "Subasta: Oposición del demandado a la ejecución del lanzamiento", 24 },
                    { 560, "DOCJ-71", "DOCJ", "Escrito presentado por el ejecutado alegando haber realizado el pago o cumplido lo acordado en la resolución judicial que se ejecuta", "Ejecución: Oposicion a la ejecución", 24 },
                    { 561, "DOCJ-72", "DOCJ", "Escrito presentado por el ejecutante en el que  muestra su disconformidad a lo manifestado por el ejecutado, por haber alegado éste el pago o el cumplimiento de lo ordenado en la sentencia que se pretende ejecutar", "Ejecución: escrito de impugnación de la oposición del ejecutante", 24 },
                    { 562, "DOCJ-73", "DOCJ", "Escrito solicitando aplazamiento del lanzamiento del bien inmueble", "Subasta: Moratoria suspensión lanzamiento. Escrito solicitando aplazamiento", 24 },
                    { 563, "DOCJ-74", "DOCJ", "Acta de comparecencia en juzgado para entrega o deposito de las llaves de un activo de Sareb.", "Acta de comparecencia y/o entrega de llaves", 24 },
                    { 564, "DOCN-01", "DOCN", "Documento notarial a través del cual el promotor deja constancia de la concusión de los trabajos de edificación y aporta la documentación legalmente preceptiva.", "Acta notarial final de obra", 25 },
                    { 565, "DOCN-02", "DOCN", "Documento notarial que hace relación al hecho de haber sido examinado por el notario los acuerdos sociales, acreditando su veracidad. Los acuerdos sociales se incorporan con estas actas al protocolo notarial con el fin de que en caso de extravío, demostrar su existencia o su fecha o conseguir copias futuras.", "Acta protocolización acuerdos sociales", 25 },
                    { 566, "DOCN-03", "DOCN", "Documento notarial por el que se eleva a público la disposición de la financiación en un préstamo promotor, de acuerdo al calendario de disposiciones marcado.", "Disposición préstamo y pólizas crédito: acta notarial", 25 },
                    { 567, "DOCN-04", "DOCN", "Documento notarial elaborado por notario en el que se documenta lo acaecido durante el procedimiento de ejecución de una garantía en la Notaría.", "Ejecución Notarial: acta notarial", 25 },
                    { 568, "DOCN-05", "DOCN", "Documento notarial emitido por un notario en el que se acredita que el órgano de administración de la sociedad si hay personas físicas que controlen o posean >25% de participación de la sociedad, identificando el nombre de las mismas.", "Manifestación titularidad real: acta notarial", 25 },
                    { 569, "DOCN-06", "DOCN", "Elevación a publico, documento notarial que describe el cambio de titularidad que se efectúa en el activo.", "Traspaso Sareb: acta complementaria cambio titularidad", 25 },
                    { 570, "DOCN-07", "DOCN", "Documento fehaciente a través del cual y de acuerdo a las condiciones marcadas por contrato se  liquida el importe del saldo resultante de las operaciones derivadas de contratos formalizados en escritura pública o en póliza intervenida.  Dicho documento, de acuerdo a la legislación española, es preceptivo que acompañe a la demanda ejecutiva", "Fijación saldo: acta", 25 },
                    { 571, "DOCN-08", "DOCN", "Operación financiera intervenida notarialmente por la que una entidad financiera otorga a la empresa el derecho a endeudarse hasta una determinada cantidad durante un período prefijado", "Poliza de credito intervenida", 25 },
                    { 572, "DOCN-09", "DOCN", "Documento  notarial enviado al depositario con  la lista completa de las garantías financieras sobre las que se  requiere la obtención de algún dato", "Requerimiento notarial depositario garantias financieras", 25 },
                    { 573, "DOCN-10", "DOCN", "Documento notarial por el que se manifiesta la intención de ejercer un derecho  de adquisción preferente", "Derecho de adquisición preferente: Requerimiento notarial", 25 },
                    { 574, "DOCN-11", "DOCN", "Testimonio de la elevación a publico del acta de traspaso, documento notarial que describe el cambio de titularidad que se efectúa en el activo.", "Testimonio del Traspaso Sareb", 25 },
                    { 575, "DOCT-01", "DOCT", "Documento técnico relacionado con el catálogo de bienes y espacios protegidos que no se encuentra especificado en otro apartado de la clasificación definida.", "Catálogo de bienes y espacios protegidos: otro documento técnico", 26 },
                    { 576, "DOCT-02", "DOCT", "Documento técnico relacionado con el estudio de detalle que no se encuentra especificado en otro apartado de la clasificación definida.", "Estudio de detalle: otros documentos técnicos y estudios", 26 },
                    { 577, "DOCT-03", "DOCT", "Documento técnico relacionado con las normas complementarias que no se encuentra especificado en otro apartado de la clasificación definida", "Normas complementarias: Otro documento técnico", 26 },
                    { 578, "DOCT-04", "DOCT", "Documento técnico relacionado con las normas subsidiarias que no se encuentra especificado en otro apartado de la clasificación definida", "Normas subsidiarias: Otro documento técnico", 26 },
                    { 579, "DOCT-05", "DOCT", "Documento a través de los cuales se definen las normas, exigencias y procedimientos a ser empleados y aplicados en todos los trabajos de construcción.", "Obra: especificación técnica", 26 },
                    { 580, "DOCT-06", "DOCT", "Documento técnico relacionado con el proyecto que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto de delimitación de suelo urbano: otro documento técnico", 26 },
                    { 581, "DOCT-07", "DOCT", "Documento técnico relacionado con el plan de sectorización/delimitación que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan de sectorización/delimitación: otros documentos técnicos y estudios", 26 },
                    { 582, "DOCT-08", "DOCT", "Documento técnico relacionado con el plan de singular interés que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan de singular interés: otro documento técnico", 26 },
                    { 583, "DOCT-09", "DOCT", "Documento técnico relacionado con el plan especial de reforma interior que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan especial de reforma interior: otros documentos técnicos y estudios", 26 },
                    { 584, "DOCT-10", "DOCT", "Documento técnico relacionado con el plan especial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan especial: otros documentos técnicos y estudios", 26 },
                    { 585, "DOCT-11", "DOCT", "Documento técnico relacionado con el plan general de ordenación territorial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan general de ordenación territorial: otro documento técnico", 26 },
                    { 586, "DOCT-12", "DOCT", "Documento técnico relacionado con el plan general que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan general: otro documento técnico", 26 },
                    { 587, "DOCT-13", "DOCT", "Documento técnico relacionado con el plan parcial que no se encuentra especificado en otro apartado de la clasificación definida.", "Plan parcial: otros documentos técnicos y estudios", 26 },
                    { 588, "DOCT-14", "DOCT", "Documento expresivo de la asunción de la ordenación detallada establecida en el Plan General o que contenga propuesta de ordenación que complete detalladamente la del sector, o unidad de actuación, o modifique la determinada en el planeamiento. Incluirá anteproyecto de urbanización.", "Programa de actuación: alternativa técnica", 26 },
                    { 589, "DOCT-15", "DOCT", "Documento técnico relacionado con el proyecto de ejecución que no se encuentra especificado en otro apartado de la clasificación definida.", "Proyecto ejecución: otros documentos técnicos y estudios", 26 },
                    { 590, "DOCT-16", "DOCT", "Documento que tiene como finalidad fijar las unidades de obra, el número de veces que se repite y dar una idea lo más aproximada posible, del importe de la realización del proyecto de ejecución.", "Proyecto ejecución: mediciones", 26 },
                    { 591, "DOCT-17", "DOCT", "Documento cuyo objeto es la ordenación de las condiciones que han de regir en la ejecución de las obras de construcción previstas en el proyecto de ejecución.", "Proyecto ejecución: pliego condiciones", 26 },
                    { 592, "DOCT-18", "DOCT", "Documento que tiene como finalidad fijar las unidades de obra, el número de veces que se repite y dar una idea lo más aproximada posible, del importe de la realización del proyecto de urbanización.", "Proyecto urbanización: mediciones", 26 },
                    { 593, "DOCT-19", "DOCT", "Comprende el conjunto de características que deberán cumplir los materiales empleados en la construcción, así como las técnicas de su colocación en la obra y los que deberán mandar en la ejecución de cualquier tipo de instalaciones y de obras accesorias y dependientes para la ejecución del proyecto de urbanización", "Proyecto urbanización: pliego condiciones", 26 },
                    { 594, "DOSS-01", "DOSS", "Presentación comercial de un producto para mostrar a potenciales inversores o compradores.", "Dossier venta", 27 },
                    { 595, "DOSS-02", "DOSS", "Conjunto de fotografías referentes a un activo.", "Dossier Fotográfico", 27 },
                    { 596, "DOSS-03", "DOSS", "Documento que resume de forma general una oportunidad de inversion sobre un activo concreto. Dicho documento es habitualmente utilizado para presentar la oportunidad de inversión al potencial inversor.", "Teaser comercial", 27 },
                    { 597, "ESCR-01", "ESCR", "Escritura que recoge el levantamiento del derecho real de garantía, que se constituyó, sobre un inmueble, para asegurar el cumplimiento de una obligación.", "Cancelación carga hipotecaria: escritura", 28 },
                    { 598, "ESCR-02", "ESCR", "Documento publico por el cual se acredita la cancelación de la deuda contraída", "Cancelación: escritura", 28 },
                    { 599, "ESCR-03", "ESCR", "Escritura de Cesión de préstamo a través del cual una persona llamada cedente, transmite a otra, llamad cesionaria, la titularidad de un derecho de crédito frente a un tercero.", "Cesión préstamo: escritura", 28 },
                    { 600, "ESCR-04", "ESCR", "Documento formalizado ante notario donde se describen y detallan los aspectos relacionados con la constitución legal de una entidad, donde entre otros se describen los datos generales de las personas que participan, el volumen del capital y los estatutos.", "Constitución: escritura", 28 },
                    { 601, "ESCR-05", "ESCR", "Documento de carácter publico en el que se crea un FAB.", "Constitución FAB: escritura", 28 },
                    { 602, "ESCR-06", "ESCR", "Documento publico por el que se acredita el traspaso de la titularidad sobre un bien entregado a cambio de la cancelación de la deuda pendiente.", "Dación: escritura", 28 },
                    { 603, "ESCR-07", "ESCR", "Documento elevado a público en el que acredita el derecho real temporal sobre cosa ajena con vocación de dominio, por el que su titular adquiere la facultad de elevar una o varias plantas, o de realizar construcciones bajo el suelo.", "Derecho vuelo y/o superficie: escritura", 28 },
                    { 604, "ESCR-08", "ESCR", "Escritura de adjudicación de inmueble tras procedimiento de reclamación notarial.", "Ejecución Notarial: escritura adjudicación inmueble", 28 },
                    { 605, "ESCR-09", "ESCR", "Escritura pública que contiene determinados productos de cobertura de tipos derivados financieros asociados a la operación.", "Escritura cobertura", 28 },
                    { 606, "ESCR-10", "ESCR", "Documento legal realizado ante notario de elevación a público de los acuerdos y condiciones existentes entre comprador y vendedor para transmitir inmuebles, suelo….", "Escritura compraventa", 28 },
                    { 607, "ESCR-11", "ESCR", "Documento en el que se constituye una garantía sobre un bien determinado como garantía del cumplimiento del pago de la deuda.", "Escritura constitución de garantía", 28 },
                    { 608, "ESCR-12", "ESCR", "Documento en el que el deudor asume determinados compromisos para proteger el pago de la deuda de la operación contratada", "Escritura covenant", 28 },
                    { 609, "ESCR-13", "ESCR", "Obra Nueva.- Elevación a publico , documento notarial que describe la edificación a construir, elementos comunes a los inmuebles como son zonas de ocio, deportivas, local de uso común vecinal…. División Horizontal.- Elevación a público, documento notarial que describe de forma individualizada todos y cada uno de los inmuebles que forman parte de la construcción ( linderos, metros, ubicación, departamentos que lo integran….). Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Escritura obra nueva y división horizontal", 28 },
                    { 610, "ESCR-14", "ESCR", "Documento notarial en el que se materializa un intercambio, en el que los contratantes se entregan bienes y no dinero. En inmuebles habitualmente el propietario del suelo acuerda vender el activo y como parte del precio recibirá determinados inmuebles de los que se construyan en lo que era su anterior activo.", "Escritura permuta", 28 },
                    { 611, "ESCR-15", "ESCR", "Documento notarial mediante el cual se acredita que una determinada persona tiene potestad para realizar las gestiones, trámites y operaciones para los que ha sido autorizada. Estas potestades han sido autorizadas por el órgano o persona que tiene capacidad para hacerlo.", "Escritura poder", 28 },
                    { 612, "ESCR-16", "ESCR", "Documento notarial por el que el cual la entidad financiera (prestamista) entrega al cliente (prestatario) una determinada cantidad de dinero estableciéndose contractualmente la forma en que habrá de restituirse el capital, y abonar los intereses remuneratorios generalmente en unos vencimientos prefijados en el cuadro de amortización que acompaña al contrato", "Escritura préstamo", 28 },
                    { 613, "ESCR-17", "ESCR", "Escritura pública a través de la cual, en el caso de la segregación, de una finca registral se separa una porción de terreno que pasa a ser considerada como finca independiente o, en el caso se una agrupación,  se realiza la unión registral de dos o mas fincas inscritas para formar una sola, dotándola de una nueva descripción y linderos.", "Escritura segregación y/o agrupación", 28 },
                    { 614, "ESCR-18", "ESCR", "Documento público por el cual un tercero sustituye (total o parcialmente) a una de las partes del contrato de la operación", "Escritura subrogación", 28 },
                    { 615, "ESCR-19", "ESCR", "Escritura en la que se corrige error material de titulo previo", "Escritura subsanación activo", 28 },
                    { 616, "ESCR-20", "ESCR", "Documento publico que recoge la Escritura de Fusión / Escisión de FAB", "Fusión / Escisión FAB: escritura", 28 },
                    { 617, "ESCR-21", "ESCR", "Escritura de Adjudicación de herencia y la aceptación de la misma por parte de los Herederos", "Herencia: escritura adjudicación y aceptación", 28 },
                    { 618, "ESCR-22", "ESCR", "Documento Publico en el que se pone de manifiesto la voluntad del fallecido sobre sus bienes o el reparto realizado por los herederos legales", "Herencia: testamento o declaración de herederos", 28 },
                    { 619, "ESCR-23", "ESCR", "Documento publico que recoge la participación en la operación de riesgos de las distintas entidades", "Operaciones sindicadas: escritura entidades participantes", 28 },
                    { 620, "ESCR-24", "ESCR", "Documento elevado a público a través del cual la entidad financiera (prestamista) entregó al cliente (prestatario) una determinada cantidad de dinero estableciéndose contractualmente la forma en que debió restituir el capital, y abonar los intereses remuneratorios y que, con el tiempo y tras el incumplimiento del prestatario, dio origen, tras la adjudicación, dación o similar, a la adquisición del bien inmobiliario por parte de Sareb", "Préstamo originario: escritura", 28 },
                    { 621, "ESCR-25", "ESCR", "Documento elevado a público a través del cual la un tercero sustituye (total o parcialmente) a una de las partes del contrato de préstamo y que, con el tiempo y tras el incumplimiento del prestatario, dio origen, tras la adjudicación, dación o similar, a la adquisición del bien inmobiliario por parte de Sareb", "Préstamo originario: escritura subrogación", 28 },
                    { 622, "ESCR-26", "ESCR", "Documento notarial que acredita el titulo de propiedad del activo y no ha sido contemplado en otra tipología de la presente clasificación.", "Titularidad del activo: otra escritura", 28 },
                    { 623, "ESCR-27", "ESCR", "Documento notarial, a través del cual, se eleva a público el contrato de arrendamiento firmado entre las partes", "Alquiler: escritura", 28 },
                    { 624, "ESCR-28", "ESCR", "Documento formalizado ante notario donde se describen y detallan los aspectos relacionados con la partición de una mercantil  en varias (escisión) o  la agrupación de varias mercantiles en una (fusión) donde entre otros se describen los datos generales de las personas que participan, los antecedentes, los datos de la sociedad resultante,..", "Escritura de fusion / Escision", 28 },
                    { 625, "ESCR-29", "ESCR", "Documento que acredita la titularidad del activo a favor de un tercero anterior a Sareb  (p.e. la entidad cedente). Se ha de tener en cuenta que en este tipo documental se englobarán tanto las escrituras como cualesquiera otros documentos de naturaleza  distinta (DOCA,  CNCV…) a excepción de aquella de naturaleza judicial (que cuenta  con tipo propio) que acrediten la titularidad previa del activo a favor de un tercero", "Documento contractual de titularidad anterior a Sareb", 28 },
                    { 626, "ESCR-30", "ESCR", "Escritura en la que se declara la al alteración o modificación física de una finca o edificación, bien sea para alterar su superficie, volumen o  restructurar su configuración legal alterando su estructura.", "Escritura de obra nueva", 28 },
                    { 627, "ESCR-31", "ESCR", "Escritura que tiene por objeto establecer en un inmueble edificado un régimen jurídico especial en el que coexisten elementos privativos ( en propiedad exclusiva y separada ) y elementos comunes ( en régimen de copropiedad)", "Escritura de división horizontal", 28 },
                    { 628, "ESCR-32", "ESCR", "Formalización en documentos público del levantamiento de una condición cuyo cumplimiento  extingue o anula un derecho u obligación", "Escritura Cancelación condición Resolutoria", 28 },
                    { 629, "ESCR-33", "ESCR", "Formalización en documentos público de la escritura de ratificacion de venta", "Escritura ratificación de venta", 28 },
                    { 630, "ESIN-01", "ESIN", "Informe técnico emitidio por el técnico municipal de disciplina urbanística como consecuencia de la visita realizada a la obra a través del cual se pretende garantizar que las actividades se ajustan a derecho .", "Actividad: informe técnico", 29 },
                    { 631, "ESIN-02", "ESIN", "Informe de la Administración Concursal referido al Art 75", "Administración concursal: informe", 29 },
                    { 632, "ESIN-03", "ESIN", "Informe resumen que detalla la actuación/actuaciones realizadas por los gestores de la entidad y que es presentado a en la asamblea o en el consejo", "Asamblea y/o consejo: informe ejecutivo", 29 },
                    { 633, "ESIN-04", "ESIN", "Documento en que se detalla las diversas pruebas realizadas a una instalación ejecutada en la obra en aras a verificar su correcta ejecución y funcionamiento de acuerdo a las especificaciones marcadas en el proyecto.", "Calidad: prueba funcionamiento instalaciones", 29 },
                    { 634, "ESIN-05", "ESIN", "Escrito de calificación de acreedor/s personados", "Calificación concurso: escrito acreedores", 29 },
                    { 635, "ESIN-06", "ESIN", "Escrito de contestación del deudor a la calificación", "Calificación concurso: escrito del concursado", 29 },
                    { 636, "ESIN-07", "ESIN", "Informe de Calificación del concurso de la Administración Concursal", "Calificación concurso: informe administración concursal", 29 },
                    { 637, "ESIN-08", "ESIN", "Informe de Calificación del concurso del Ministerio Fiscal", "Calificación concurso: informe ministerio fiscal", 29 },
                    { 638, "ESIN-09", "ESIN", "Acta de seguimiento del convenio de acreedores", "Convenio acreedores: acta seguimiento convenio", 29 },
                    { 639, "ESIN-10", "ESIN", "Informe de asesoría jurídica sobre propuesta de convenio de acreedores", "Convenio acreedores: informe legal", 29 },
                    { 640, "ESIN-11", "ESIN", "Informe semestral sobre evolución del convenio de acreedores", "Convenio acreedores: informe semestral", 29 },
                    { 641, "ESIN-12", "ESIN", "Resolución de finalización de convenio por cumplimiento del mismo", "Convenio acreedores: texto definitivo", 29 },
                    { 642, "ESIN-13", "ESIN", "Informe que indica el estado y avance sobre el presupuesto de costes inicial", "Costes: informe de estado", 29 },
                    { 643, "ESIN-14", "ESIN", "Informe emitido por un auditor independiente en el que se refleja el estado de las cuentas anuales de una entidad durante un periodo determinado, generalmente un ejercicio contable.", "Cuentas anuales: informe auditoría", 29 },
                    { 644, "ESIN-15", "ESIN", "Documento obligatorio, de aplicación exclusiva en la Comunidad Autónoma Andaluza, de acuerdo al reglamento de información al consumidor (decreto 218/2005) mediante el cual, los promotores o intermediarios profesionales, de viviendas en proyecto o construcción deberán, de forma gratuita, poner a disposición de sus clientes el Documento Informativo Abreviado, en el cual se plasmarán una serie de informaciones resumen aplicable a la promoción en venta , como son, a título de ejemplo, información legal del promotor, del proyectista, del director de obra, de la empresa constructora, planos de la vivienda, superficies de la misma, descripción de la vivienda y sus anejos,...", "Documento informativo abreviado (DIA)", 29 },
                    { 645, "ESIN-16", "ESIN", "Estudio profundo realizado en virtud del cual el adquirente de un activo, con explicito consentimiento y asistencia del vendedor, realiza una detallada investigación de los diferentes aspectos (legal, fiscal, técnica, urbanística,...) del activo o entidad que se pretende adquirir al objeto de conocer con mayor profundidad el activo.", "Due diligence (legal, fiscal, técnica, urbanística…)", 29 },
                    { 646, "ESIN-17", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del proyecto, obtenida a partir de la previsión de ventas ajustadas al mercado en el que está ubicado el activo y tipología de unidades resultantes definidos, de la estimación realista de los costes y gastos necesarios para la consumación y entrega en condiciones del producto inmobiliario definido", "Estudio de detalle: estudio de viabilidad económica", 29 },
                    { 647, "ESIN-18", "ESIN", "Análisis sobre la situación de la oferta y la demanda de similares bienes inmuebles ubicados dentro de una zona determinada.", "Estudio mercado", 29 },
                    { 648, "ESIN-19", "ESIN", "Documento de investigación a través del cual se realiza un análisis de la situación, en términos generales, en que se encuentra el mercado inmobiliario y de la posible evolución del mismo.", "Estudio y/o informe situación inmobiliaria", 29 },
                    { 649, "ESIN-20", "ESIN", "Informe redactado por la empresa constructora en el que se especifica el tratamiento que se le va a dar, de acuerdo a la normativa aplicable, a los residuos de construcción y demolición generados en durante la ejecución de la obra.", "Gestión de residuos: informe", 29 },
                    { 650, "ESIN-21", "ESIN", "Informe anual sobre la gestión del FAB.", "Gestión FAB: informe anual", 29 },
                    { 651, "ESIN-22", "ESIN", "Informe de auditoria sobre el FAB.", "Gestión FAB: informe auditoría", 29 },
                    { 652, "ESIN-23", "ESIN", "Informe en el que se comunican las cuentas anuales a la CNMV.", "Gestión FAB: informe de cuentas anuales comunicadas a la CNMV", 29 },
                    { 653, "ESIN-24", "ESIN", "Informe semestral de gestión del FAB.", "Gestión FAB: informe semestral", 29 },
                    { 654, "ESIN-25", "ESIN", "Informe emitido por asesoría jurídica sobre procedimiento de testamentaria.", "Herencia: dictamen jurídico", 29 },
                    { 655, "ESIN-26", "ESIN", "Listado de riesgos directos e indirectos que el deudor mantiene con el Sistema Financiero.", "Informe CIRBE situación con el resto del sistema financiero", 29 },
                    { 656, "ESIN-27", "ESIN", "Informe de conflicto de intereses en relación emitido por el departamento de regulación y cumplimiento", "Informe conflicto intereses (RyC)", 29 },
                    { 657, "ESIN-28", "ESIN", "Informe interno de Sareb realizado con ocasión de la transferencia de activos.", "Informe de \"transfer\" Sareb", 29 },
                    { 658, "ESIN-30", "ESIN", "Documento informativo emitido por un experto a través del cual se documenta el resultado de la revisión efectuada sobre el activo.", "Informe de revisión de experto", 29 },
                    { 659, "ESIN-31", "ESIN", "Informe de análisis sobre una operación financiera en el que se considera la adecuación de la misma a las directrices de la entidad, idoneidad sobre la finalidad y viabilidad de la misma y solvencia de los solicitantes, previa a su paso al comité decisor.", "Informe riesgos", 29 },
                    { 660, "ESIN-32", "ESIN", "Documento informativo emitido por un experto a través del cual se documentan con detalle determinados aspectos concretos relativos a un activo.", "Informe detalle", 29 },
                    { 661, "ESIN-33", "ESIN", "Informe resumen sobre las catacterísticas de una operación, remitido al comité decisor correspondiente. *Para los informes ejecutivos sobre activos utilice ESIN 91.", "Informe ejecutivo", 29 },
                    { 662, "ESIN-34", "ESIN", "Documento informativo emitido por un experto a través del cual se documentan con detalle el estado en el que se encuentra un activo concreto.", "Informe estado", 29 },
                    { 663, "ESIN-35", "ESIN", "Documento informativo emitido por base de datos documental  donde de deja evidencia de la consulta realizada sobre posibles personas que de responsabilidad publica y otras relaciones.", "Informe factiva", 29 },
                    { 664, "ESIN-36", "ESIN", "Documento informativo emitido por un departamento, persona o ente, relativos a la ley o al derecho. Incluir en este tipo documental las valoraciones de letrados o asesoría jurídica.*Para informes legales referentes a un activo utilizar ESIN-90", "Informe legal", 29 },
                    { 665, "ESIN-37", "ESIN", "Documento informativo emitido por un experto técnico a través del cual se documenta con detalle la situación, en lo que a medioambiente se refiere, de un activo concreto.", "Informe medioambiental", 29 },
                    { 666, "ESIN-39", "ESIN", "Cuantificación de la calidad crediticia de un acreditado.", "Informe rating interno", 29 },
                    { 667, "ESIN-40", "ESIN", "Informe que valora la solvencia económico patrimonial de un solicitante o avalista.", "Informe solvencia titular / avalista", 29 },
                    { 668, "ESIN-41", "ESIN", "Documento informativo emitido por un experto a través del cual se documentan con detalle determinados aspectos técnicos referentes a un activo concreto.", "Informe técnico", 29 },
                    { 669, "ESIN-42", "ESIN", "Documento justificativo a través del cual se pretende valorar la conveniencia respecto al precio propuesto en una oferta realizada para la compra de un activo.", "Justificación precio propuesto", 29 },
                    { 670, "ESIN-43", "ESIN", "Informe trimestral obligatorio sobre la aplicación del Plan de Liquidación.", "Liquidación concurso: informe trimestral administración concursal", 29 },
                    { 671, "ESIN-45", "ESIN", "Documento suscrito por técnico competente por el que se pone de manifiesto la situación de la obra.", "Obra: informe estado", 29 },
                    { 672, "ESIN-46", "ESIN", "Informe emitido por los técnicos municipales en el que se acredita el estado de la obra en el momento de la inspección.", "Obra: informe municipal", 29 },
                    { 673, "ESIN-47", "ESIN", "Informe emitido por la empresa responsable de la supervisión y control (Organismo de Control Técnico) durante el proceso constructivo, en aras a verificar la calidad del proyecto, la calidad de los materiales y la calidad de ejecución de la obra. Esta verificación se realiza de acuerdo al proyecto y normativa aplicable y sin interferir con las funciones propias de la dirección facultativa.", "Obra: informe organismo de control técnico (OCT)", 29 },
                    { 674, "ESIN-48", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan.", "Plan de sectorización/delimitación: estudio de viabilidad económica", 29 },
                    { 675, "ESIN-49", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan de singular interés.", "Plan de singular interés: estudio viabilidad económico-financiera", 29 },
                    { 676, "ESIN-50", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan especial de reforma interior.", "Plan especial de reforma interior: estudio de viabilidad económica", 29 },
                    { 677, "ESIN-51", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan especial.", "Plan especial: estudio de viabilidad económica", 29 },
                    { 678, "ESIN-52", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan general.", "Plan general: estudio viabilidad económico-financiera", 29 },
                    { 679, "ESIN-53", "ESIN", "Documento de análisis orientado a determinar la rentabilidad del plan de sectorización obtenida a partir de la previsión de ingresos y costes y gastos previstos por el plan parcial.", "Plan parcial: estudio de viabilidad económica", 29 },
                    { 680, "ESIN-54", "ESIN", "Documento que justifica el precio a aplicar a una transacción.", "Precio transferencia", 29 },
                    { 681, "ESIN-55", "ESIN", "Documento informativo, con frecuencia mensual, en el que se detalla el resultado del seguimiento realizado por técnico competente sobre de la obra en ejecución. En el caso de GMAO corresponde al documento AD17 - FICHA BONUS/MALUS CONSTRUCTORA", "Seguimiento: informe mensual", 29 },
                    { 682, "ESIN-56", "ESIN", "Seguimiento documentado del cumplimiento, en materia de seguridad y salud, de las condiciones y distintas normativas que le sean de aplicación a la obra.", "Seguridad y salud: auditoría", 29 },
                    { 683, "ESIN-57", "ESIN", "Resumen de periodicidad mensual de las actuaciones llevadas acabo por el Coordinador de Seguridad y Salud durante la fase de ejecución.", "Seguridad y salud: informe mensual", 29 },
                    { 684, "ESIN-58", "ESIN", "Documento informativo en el que se detalla el resultado del seguimiento realizado por técnico competente sobre el estado de la seguridad en la obra en ejecución", "Seguridad: informe", 29 },
                    { 685, "ESIN-59", "ESIN", "Conjunto de documentos en el que se incluye la documentación necesaria para la realización subasta de un activo.", "Subasta: informe", 29 },
                    { 686, "ESIN-60", "ESIN", "Perfil ciego en el que se ofrece únicamente información fragmentada del producto ofrecido.", "Teaser comercial", 29 },
                    { 687, "ESIN-61", "ESIN", "Informe de Valoración en la venta de un activo de la entidad.", "Venta activo entidad: informe valoración", 29 },
                    { 688, "ESIN-62", "ESIN", "Documento en el que se incluye de forma detallada las distintas inversiones a realizar sobre el inmueble para, a través de éstas, obtener una mejora en la rentabilidad del mismo.", "Informe Capex", 29 },
                    { 689, "ESIN-63", "ESIN", "Conjunto de informes (preliminares, complementarios, de situación,...)  que, de acuerdo al RD 9/2005 y demás normativa aplicable, deberán de ser presentados  por el titular de la actividad, catalogada como contaminante, ante la autoridad competente. Así  mismo dichos informes se deberán presentar cuando la actividad a realizar se ubique en suelos catalogados como contaminados por la administración.", "Suelo: informe de actividades potencialmente contaminantes", 29 },
                    { 690, "ESIN-64", "ESIN", "Informe emitido por la Asociación Nacional de Entidades de Financiación, hoy Asociación Nacional de Establecimientos Financieros de Crédito, o del Registro de Aceptaciones Impagadas, referente a la inclusión o no de una persona concreta en su fichero de morosos, detallando por tanto si la persona concreta tiene o no una deuda impagada con alguno de sus socios.", "Informe ASNEF", 29 },
                    { 691, "ESIN-66", "ESIN", "Informe que manifiesta el estado de mantenimiento de un activo", "Informe revisión periódica mantenimiento", 29 },
                    { 692, "ESIN-68", "ESIN", "Documento de análisis de determinadas labores de tramitación de una operación.", "Informe seguimiento gestión", 29 },
                    { 693, "ESIN-69", "ESIN", "Documento de análisis para la correcta determinación de un precio.", "Informe fijación de precio", 29 },
                    { 694, "ESIN-70", "ESIN", "Documento de análisis para el establecimiento de acciones tendentes a corregir un determinado estado o situación.", "Informe medidas correctoras", 29 },
                    { 695, "ESIN-71", "ESIN", "Documento que incluye el estudio y análisis de la existencia o no de indicios o sospechas de blanqueo de capitales y financiación del terrorismo.", "PBC: informe análisis indicios", 29 },
                    { 696, "ESIN-75", "ESIN", "Documento emitido por el departamento de Prevención de Blanqueo de Capitales donde se evalúa e informa la viabilidad de una operación financiera.", "PBC: informe evaluación riesgo", 29 },
                    { 697, "ESIN-76", "ESIN", "Informe valorativo proveniente del departamento de Patrimonio.", "Informe patrimonio", 29 },
                    { 698, "ESIN-77", "ESIN", "Documento que especifica las cargas fiscales que implican la realización o no de una operación.", "Informe cargas fiscales", 29 },
                    { 699, "ESIN-78", "ESIN", "Documento que especifica las cargas jurídicas que implican la realización o no de una operación.", "Informe cargas jurídicas", 29 },
                    { 700, "ESIN-79", "ESIN", "Informe que unifica la información recibida de todos los servicers que tienen posiciones en un concurso.", "Informe posición global", 29 },
                    { 701, "ESIN-80", "ESIN", "Documento que incluye la descripción escrita de la opinión departamental o personal sobre una acción específica u operación. *Para las valoraciones de letrado o valoraciones de asesoría jurídica utilizar ESIN-36.", "Informe valorativo", 29 },
                    { 702, "ESIN-81", "ESIN", "Informe en el que se recogen los dichos del interesado o de su abogado argumentando por escrito, hechos y derechos en defensa de su causa.", "Informe alegaciones", 29 },
                    { 703, "ESIN-82", "ESIN", "Informe, habitualmente mensual, sobre plazos y calidades efectivos, a la vista del desarrollo de las obras, emitido por el responsable de la monitorización del proyecto inmobiliario a través del cual se pretende controlar la inversión financiera durante los procesos de construcción.", "Obra project monitoring: informe", 29 },
                    { 704, "ESIN-84", "ESIN", "Documento que incluye la descripción escrita, de las características y pasos a seguir en una circunstancia o asunto específico.", "Informe procedimiento", 29 },
                    { 705, "ESIN-85", "ESIN", "Documento que incluye la comprobación o examen de un hecho o una operación específica.", "Informe verificación", 29 },
                    { 706, "ESIN-86", "ESIN", "Documento que describe o explica aspectos fiscales o tributarios de un hecho, negocio o acto.", "Informe fiscal", 29 },
                    { 707, "ESIN-87", "ESIN", "Documento que describe o  explica en base a  unos determinados criterios la  valoración o precio asignado.", "Informe valoración y precio", 29 },
                    { 708, "ESIN-88", "ESIN", "Documento que describe o explica la conveniencia y riesgos de tomar en prenda una cosa, como garantía de cumplimiento de una obligación.", "Informe pignoración", 29 },
                    { 709, "ESIN-89", "ESIN", "Documento que describe o explica  la situación y consecuencias de la posesión como hecho jurídico.", "Informe estado posesorio", 29 },
                    { 710, "ESIN-90", "ESIN", "Documento informativo emitido por un experto legal a través del cual se documenta con detalle la situación legal de un activo concreto. *Para informes legales relacionados con una operación de negocio utilice ESIN-36", "Informe legal activo", 29 },
                    { 711, "ESIN-91", "ESIN", "Informe resumen remitido al comité decisor correspondiente sobre las peculiaridades de un activo. *Para Informes ejecutivos referentes a operaciones de negocio utilice ESIN-33", "Informe ejecutivo activo", 29 },
                    { 712, "ESIN-92", "ESIN", "Informe emitido a través del cual se hace un resumen de las actividades realizadas en una operación en concreto y del resultado obtenido, como consecuencia de los trabajos realizados (p.e. Informe de cierre de los trabajos de mantenimiento realizados sobre un activo inmobiliario propiedad de SAREB)", "Informe cierre", 29 },
                    { 713, "ESIN-93", "ESIN", "Documento que contiene el estudio de seguridad y salud redactado por el técnico competente, referente a los trabajos a realizar en el desarrollo de las labores proyectadas en una obra.", "Proyecto ejecución: estudio seguridad y salud", 29 },
                    { 714, "ESIN-94", "ESIN", "Documento que contiene el informe redactado por un Organismo de Control Autorizado por las autoridades públicas competentes de acuerdo a la normativa vigente.", "Obra: Informe Organismo Control Autorizado (OCA)", 29 },
                    { 715, "ESIN-95", "ESIN", "Documento que contiene la valoración, directa  o indirecta, de un activo inmobiliario mediante la anticipaicón de las rentas netas, reales o hipotéticas, a percibir por el activo a futuro.", "Informe capitalización rentas", 29 },
                    { 716, "ESIN-96", "ESIN", "Informe en el que se acredita que se han tomado las medidas adecuadas para identificar al titular real y  comprobar su identidad con carácter previo al establecimiento de relaciones de negocio o a la ejecución de operaciones.", "Informe diligencia debida", 29 },
                    { 717, "ESIN-97", "ESIN", "Informe a través del cual se resume y evalua el grado de solvencia de una persona atendiendo a los inmuebles sobre los que éste tiene derecho y obligaciones a su favor.", "Resumen de Fincabilidad", 29 },
                    { 718, "ESIN-98", "ESIN", "Informe pericial independiente emitido para, habitualmente, incorporarsepor una de las partes a un procedimiento judicial en el cual se evaluan hechos y circunstancias que se consideran relevantes.", "Informe Forensic", 29 },
                    { 719, "ESIN-AA", "ESIN", "Informe en el que se muestra el análisis respecto a la evolución comercial de un conjunto de activos o colaterales emitido para valorar la conveniencia o no de la renovación/revisión de una operación (p.e. PDV) o el volumen de ingresos obtenidos durante un periodo de tiempo por la explotación de un activo (p.e. a través del alquiler)", "Histórico de ventas/ingresos", 29 },
                    { 720, "ESIN-AC", "ESIN", "Informe presentado por perito en el que se valora el bien embargado para su realización forzosa, determinando el precio que servirá de tipo", "Subasta Avalúo bienes para subasta", 29 },
                    { 721, "ESIN-AD", "ESIN", "Informes emitidos por facultativos o servicios sociales que acreditan especial situación de vulnerabilidad", "Informes médicos/ servicios sociales", 29 },
                    { 722, "ESIN-AE", "ESIN", "Documento emitido por la administración pública relativa  a cuestiones urbanística, derechos administrativos, concesiones….", "Informe de Administración Pública", 29 },
                    { 723, "ESIN-AH", "ESIN", "Informe relativo al programa del plan de trabajo y el nivel de intervención a realizar", "Informe PAS/PIL", 29 },
                    { 724, "ESIN-AI", "ESIN", "Informe relativo al programa del plan de trabajo y el nivel de intervención a realizar firmado por las partes", "Informe PAS/PIL  firmado", 29 },
                    { 725, "ESIN-AJ", "ESIN", "Informe, elaborado por un tercero, respecto a una discrepancia elevada en una valoracion para su elevacion a un comité", "Informe valorativo para Comité (Control3)", 29 },
                    { 726, "ESTA-01", "ESTA", "Documento aprobado en el que se incluye la normativa por la que se regirán las relaciones internas de los miembros de una entidad.", "Bases y estatutos: documento", 30 },
                    { 727, "ESTA-02", "ESTA", "Conjunto de normas que una sociedad establece para su funcionamiento, como complemento de la legislación general.", "Estatutos sociales", 30 },
                    { 728, "ESTA-03", "ESTA", "Conjunto de normas que, junto con los estatutos, rigen las relaciones internas en una comunidad de propietarios.", "Reglamento régimen interior", 30 },
                    { 729, "ESTT-01", "ESTT", "Documento en el que se muestra el resultado de la recopilación y análisis de la información del conjunto de entidades que conforman el sector con el fin de tener una visión global del mismo para, con ello, facilitar la toma de decisiones referente a la actuación que se pretende realizar.", "Catálogo de bienes y espacios protegido: estudio sectorial", 31 },
                    { 730, "ESTT-02", "ESTT", "Documento, en el que queda recogido el desarrollo y los resultados del estudio", "Estudio arqueológico: documento", 31 },
                    { 731, "ESTT-03", "ESTT", "Documento que ayuda a definir la tipología de la cimentación , estructuras, materiales a utilizar, observa el nivel freático, posibles filtraciones…..). Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"07 - Garantías\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-...)", "Estudio geotécnico: documento", 31 },
                    { 732, "ESTT-04", "ESTT", "Documento que analiza el terreno y su composición con el fin de acometer la construcción conforme al suelo en el que se va a asentar (arcilloso, calizo….)", "Estudio topográfico: documento", 31 },
                    { 733, "ESTT-05", "ESTT", "Documento en el que se muestra el resultado de la recopilación y análisis de la información del conjunto de entidades que conforman el sector con el fin de tener una visión global del mismo para, con ello, facilitar la toma de decisiones referente a la actuación que se pretende realizar.", "Plan de singular interés: estudio sectorial", 31 },
                    { 734, "ESTT-06", "ESTT", "Documento que acompaña al plan que describe los efectos medioambientales regulados por la normativa específica (residuos, vertidos, ruido…) así como la valoración de los efectos relevantes no contemplados por ninguna normativa medioambiental y que subyacen en la actuación prevista por el plan.", "Plan de singular interés: informe / memoria medioambiental", 31 },
                    { 735, "ESTT-07", "ESTT", "Documento del proyecto que, de acuerdo a la normativa, describe todos los aspectos ambientales claves relacionados con el proyecto", "Proyecto calificación urbanística: informe / memoria medioambiental", 31 },
                    { 736, "ESTT-08", "ESTT", "Documento técnico anejo a la memoria del proyecto, redatado por el el técnico competente, por el que se establece el tratamiento que se le tendrá a dar, de acuerdo a la normativa aplicable, a los residuos de construcción y demolición que se generarán durante la ejecución de la obra", "Proyecto urbanización: estudio gestión de residuos", 31 },
                    { 737, "ESTT-09", "ESTT", "Documento técnico anejo a la memoria del proyecto por el que se establecen las condiciones mínimas de seguridad y salud en las obras de construcción.", "Proyecto urbanización: estudio seguridad y salud", 31 },
                    { 738, "FACT-01", "FACT", "Factura recibida por la adquisición de un activo", "Adquisición activo: factura", 32 },
                    { 739, "FACT-02", "FACT", "Facturas emitidas por el Catastro como consecuencia de los realizados por la tramitación de la documentación catastral", "Catastro: factura", 32 },
                    { 740, "FACT-03", "FACT", "Factura emitida/recibida como consecuencia de una relación contractual preexistente.", "Contrato: factura comercial", 32 },
                    { 741, "FACT-04", "FACT", "Factura emitida por la entidad al partícipe/socio como consecuencia del reparto de los costes asumidos por la entidad y girados al socio/participe en el porcentaje que le corresponde.", "Derramas y cuotas: factura", 32 },
                    { 742, "FACT-05", "FACT", "Justificante del abono en operación de expropiación", "Expropiación: justificante abono cantidad concurrente", 32 },
                    { 743, "FACT-06", "FACT", "Documento en el que se fijan las condiciones de venta de las mercancías y sus especificaciones. Sirve como comprobante de la venta, exigiéndose para la exportación en el país de origen y para la importación en el país de destino. También se utiliza como justificante del contrato comercial.", "Factura comercial", 32 },
                    { 744, "FACT-07", "FACT", "Facturas recibidas por servicios y suplidos de gestoría", "Gestoría: factura y gastos", 32 },
                    { 745, "FACT-08", "FACT", "Factura recibida por Honorarios del letrado por los servicios prestados", "Letrados: minuta / factura", 32 },
                    { 746, "FACT-09", "FACT", "Factura recibida por honorarios de Notario y otros fedatarios públicos. Dentro de este tipo documental se incorpora tanto la factura como las provisiones de fondos y liquidaciones realizadas.", "Notario: factura", 32 },
                    { 747, "FACT-10", "FACT", "Factura recibida cuya tipología no está contemplada en la presente clasificación.", "Otro gasto", 32 },
                    { 748, "FACT-11", "FACT", "Justificante de Provisión de Fondos", "Préstamo originario: provisión fondos", 32 },
                    { 749, "FACT-12", "FACT", "Factura recibida por Honorarios del procurador por los servicios prestados", "Procuradores: minuta / factura", 32 },
                    { 750, "FACT-13", "FACT", "Factura recibida del registro de la propiedad", "Registro Propiedad: factura", 32 },
                    { 751, "FACT-14", "FACT", "Documento presentado por la entidad acreedora liquidando intereses y adjuntando minuta de Procurador y Abogado", "Subasta: liquidación intereses y tasación de costas ejecución", 32 },
                    { 752, "FACT-15", "FACT", "Factura recibida por la Tasación contratada", "Tasación: factura", 32 },
                    { 753, "FACT-16", "FACT", "Factura correspondiente a una transacción económica", "Transacción: factura", 32 },
                    { 754, "FACT-17", "FACT", "Factura emitida por Sareb como consecuencia del importe girado al arrendatario de acuerdo a lo estipulado en el contrato de arrendamiento firmado entre las partes", "Alquiler: factura", 32 },
                    { 755, "FACT-18", "FACT", "Factura  de los suministros de un inmueble", "Suministros: factura", 32 },
                    { 756, "FIAV-01", "FIAV", "Carta de aval para depositar en administración publica", "Aval / fianza: documento", 33 },
                    { 757, "FIAV-02", "FIAV", "Carta de aval por las cantidades entregadas a cuenta de los clientes", "Aval cantidades entregadas en cuenta por el cliente", 33 },
                    { 758, "FIAV-03", "FIAV", "Documento de formalización en el que se recoge la garantía otorgada a la entidad de conservación", "Garantía otorgada a favor entidad", 33 },
                    { 759, "FIAV-04", "FIAV", "Producto que consiste en la entrega de una cantidad de dinero a una entidad bancaria durante un tiempo determinado.", "Imposiciones a plazo", 33 },
                    { 760, "FIAV-05", "FIAV", "Deposito realizado para participar en la subasta", "Subasta: fianza", 33 },
                    { 761, "FIAV-06", "FIAV", "Documento entregado por el arrendatario a través del cual el arrendatario (mediante una fianza o similar) o un tercero (mediante una aval bancario, una fianza personal o similar) garantiza el cumplimiento de las obligaciones estipuladas en el contrato de arrendamiento firmado por las partes", "Alquiler: garantía / aval", 33 },
                    { 762, "FICH-01", "FICH", "Ficha resumen que recoge los datos técnicos e información urbanística referente a la situación y estado de un activo en lo que al catálogo de bienes y espacios protegidos se refiere.", "Catálogo de bienes y espacios protegidos: ficha", 34 },
                    { 763, "FICH-02", "FICH", "Documentos provenientes del Catastro que acreditan los datos físicos, jurídicos del inmueble", "Catastro: ficha", 34 },
                    { 764, "FICH-03", "FICH", "Documento que le Banco solicita a un cliente, donde debe detallar su situación patrimonial, ingresos, pagos, etc. Normalmente para el estudio de una operación crediticia.", "Declaración de bienes", 34 },
                    { 765, "FICH-04", "FICH", "Documento en el que se recogen los datos del solicitante de una operación (datos identificativos, de actividad, así como el detalle del origen de los fondos destinados a la operación y medios de pago que se van a utilizar)", "Ficha conocimiento cliente (KYC)", 34 },
                    { 766, "FICH-05", "FICH", "Ficha resumen que recoge los datos básicos referentes a la situación y estado de un activo", "Ficha datos básicos", 34 },
                    { 767, "FICH-06", "FICH", "Documento en el que se establecen las comisiones de los comercializadores y el margen neto de SAREB", "Ficha desinversión", 34 },
                    { 768, "FICH-07", "FICH", "Documento resumen que contiene la información relevante referente a la escrituración de un inmueble.", "Ficha escrituración inmueble", 34 },
                    { 769, "FICH-08", "FICH", "Documento resumen de la relación de gastos incurridos por el activo", "Ficha gasto", 34 },
                    { 770, "FICH-09", "FICH", "Ficha resumen que recoge los datos legales referentes a la situación y estado de un activo", "Ficha legal", 34 },
                    { 771, "FICH-10", "FICH", "Documento resumen sobre las características concretas de un activo", "Ficha resumen", 34 },
                    { 772, "FICH-11", "FICH", "Ficha resumen que recoge los datos técnicos e información urbanística referente a la situación y estado de un activo", "Ficha técnica / urbanística", 34 },
                    { 773, "FICH-12", "FICH", "Ficha resumen que recoge los datos técnicos e información urbanística reflejada en el Plan referente a la situación y estado de un activo", "Plan general de ordenación territorial: ficha", 34 },
                    { 774, "FICH-13", "FICH", "Ficha resumen interna de Sareb en la que se recoge el histórico de comercialización del activo objeto de oferta. También se recoge la opinión sobre la operación, el margen sobre el coste de adquisición y la liquidez., así como las acciones de promoción llevadas a cabo por los APIs o las oficinas de comerciales de las Entidades Cedentes.", "Statement", 34 },
                    { 775, "FICH-18", "FICH", "Lista en la que se enumeran los las acciones para la comercialización de un activo.", "Ficha acción comercial", 34 },
                    { 776, "FICH-19", "FICH", "Lista en la que se enumeran los datos identificativos, laborales, y económicos básicos referentes a una persona o entidad. Frecuentemente utilizada para el estudio de una propuesta de riesgos en operaciones financieras o en la comercialización de activos inmobiliarios.", "Ficha cliente", 34 },
                    { 777, "FICH-20", "FICH", "Ficha de sello que recoge la información relativa a las propuestas de actuación sobre activos inmobiliarios", "Ficha de sello de propuesta sobre activos inmobiliarios", 34 },
                    { 778, "FICH-21", "FICH", "Ficha resumen en la cual se documenta el estado en el que se encuentra un activo garantía.", "Ficha estado colaterales", 34 },
                    { 779, "FICH-22", "FICH", "Plantilla en la que se registra y se deja constancia de los indicios de ocupación del inmueble o ausencia de los mismos.", "Plantilla situación posesoria", 34 },
                    { 780, "FICH-23", "FICH", "Ficha de Comercialización: Ficha que recoge el histórico de publicaciones así como el funnel comercial del activo", "Ficha de Comercialización", 34 },
                    { 781, "FICH-24", "FICH", "Ficha resumen de los datos de un Expediente que se encuentran en COLABORA y ATLAS", "Sello", 34 },
                    { 782, "FICH-25", "FICH", "Ficha con la propuesta del plan de trabajo y el nivel de intervención", "Ficha PAS/PIL", 34 },
                    { 783, "FICH-26", "FICH", "Ficha extraida de ATLAS para incorporar a un comité", "Ficha ATLAS", 34 },
                    { 784, "FOTO-01", "FOTO", "Documentación gráfica que muestra el estado de una avería", "Fotografía estado / avería", 35 },
                    { 785, "FOTO-02", "FOTO", "Documentación gráfica que muestra el estado de una reparación", "Fotografía reparación", 35 },
                    { 786, "FOTO-03", "FOTO", "Documentación gráfica que muestra el estado de una avería", "Obra: fotografía estado / avería", 35 },
                    { 787, "FOTO-04", "FOTO", "Documentación gráfica no incluida en el resto de apartados definidos en la clasificación", "Otra fotografía", 35 },
                    { 788, "FOTO-05", "FOTO", "Documentación gráfica que muestra el estado en el que se entrega la vivienda", "Postventa: fotografía", 35 },
                    { 789, "FOTO-06", "FOTO", "Documentación gráfica anexada a la tasación", "Tasación: fotografía", 35 },
                    { 790, "FOTO-07", "FOTO", "Imagen creada artificialmente que ilustra, de cara a facilitar la comercialización del activo, el estado final que tendrá el activo o desarrollo inmobiliario una vez finalizado.", "Infografía", 35 },
                    { 791, "GARA-01", "GARA", "Documento por escrito en el que se establecen el compromiso temporal del instalador/fabricante/mantenedor respecto a los trabajos y materiales utilizados en el mantenimiento o reparación", "Garantía mantenimiento / reparación", 36 },
                    { 792, "GARA-02", "GARA", "Documento por escrito en el que se establecen el compromiso temporal del instalador/fabricante respecto a los trabajos y materiales utilizados en la instalación.", "Garantía equipos y electrodomésticos", 36 },
                    { 793, "HOJA-01", "HOJA", "Escrito del expropiado por el que comunica al órgano expropiante la aceptación o rechazo a la hoja de aprecio de la Administración", "Expropiación: aceptación/rechazo hoja de aprecio", 37 },
                    { 794, "HOJA-02", "HOJA", "Documento que expone la valoración de los bienes por el expropiado cuando no se llega a un acuerdo previo", "Expropiación: hoja de aprecio titular afectado", 37 },
                    { 795, "INLI-01", "INLI", "Relación de puntos de control sobre venta de cartera", "Checklist controles", 38 },
                    { 796, "INLI-02", "INLI", "Documento registro de todos los intervinientes en el desarrollo de la obra, generando un Directorio útil de todos los contactos necesarios durante la obra y obligatorio para la elaboración del Libro del Edificio", "Directorio de intervinientes", 38 },
                    { 797, "INLI-03", "INLI", "Documento que recoge la relación de incidencias detectadas sobre el inmueble", "Listado con problemáticas", 38 },
                    { 798, "INLI-08", "INLI", "Relación de información referente a personas natural o jurídica que desean realizar una inversión de caudales.", "Listado inversores", 38 },
                    { 799, "INLI-09", "INLI", "Inventario, listado o relación de datos. Incluir en este tipo documental los inventarios de información referentes a las peticiones o solicitudes de carteras.", "Inventario: documentación", 38 },
                    { 800, "INLI-10", "INLI", "Plantilla definida por Sareb a través de la cual se documentan los distintos criterios a tener en cuenta de cara a la evaluación de los motivos relevantes para la toma de decisión respecto a la ejecución/formalización o no de una operación concreta (p.e. de daciones)", "Plantilla revisión operación", 38 },
                    { 801, "INLI-11", "INLI", "Documento que recoge el listado de los bienes relacionados con el procedimiento/concurso", "Listado de activos relacionados", 38 },
                    { 802, "INLI-12", "INLI", "Documento que recoge el listado de los bienes relacionados con una operación y la superficie de los mismos", "Listado de activos y superficies", 38 },
                    { 803, "INLI-13", "INLI", "Documento que recoge el listado de las adecuaciones necesarias para acondicionar los activos atomizados para el SEPES", "Checklist acondicionamiento", 38 },
                    { 804, "INRG-01", "INRG", "Documento de inscripción en registro de Bien Mueble", "Bien mobiliario: inscripción registro", 39 },
                    { 805, "INRG-02", "INRG", "Documento de inscripción en el registro de una entidad", "Constitución: inscripción registro", 39 },
                    { 806, "INRG-03", "INRG", "Anotación en el registro de la declaración de concurso", "Declaración concurso: anotación registro", 39 },
                    { 807, "INRG-04", "INRG", "Documento acreditativo de la inscripción en el  registro de la disolución de una entidad", "Disolución: inscripción registro", 39 },
                    { 808, "INRG-05", "INRG", "Justificantes Inscripción de la Fusión/Escisión del FAB en la CNMV", "Fusión / Escisión FAB: inscripción en la CNMV", 39 },
                    { 809, "INRG-06", "INRG", "Justificante e la Fusión/Escisión del FAB en el Registro Mercantil", "Fusión / Escisión FAB: inscripción en registro mercantil", 39 },
                    { 810, "INRG-07", "INRG", "Documento acreditativo de la inscripción en el Registro de la Propiedad del proyecto de reparcelación aprobado definitivamente por la Administración actuante.", "Inscripción proyecto equidistribución", 39 },
                    { 811, "INRG-08", "INRG", "Documento Registral que refleja la calificación realizada por el Registrador del documento presentado y, llegado el caso, la inscripción del cambio de titularidad a favor del nuevo propietario. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Registro Propiedad: calificación y/o inscripción", 39 },
                    { 812, "INRG-09", "INRG", "Asiento preliminar y de duración limitada que se practica en el Libro Diario, en el que consta el acceso al Registro de la Propiedad de un título o documento, en el que se ejercita una pretensión susceptible de iniciar el procedimiento registral.   Dicho asiento implica una actuación registral, y es presupuesto de  la extensión de otros asientos de inscripción, anotación preventiva, cancelación o nota marginal,…", "Registro Propiedad: asiento de presentación", 39 },
                    { 813, "INSS-01", "INSS", "Documento modelo que acredita el alta en el IAE.", "Impuesto actividades económicas (IAE): alta", 40 },
                    { 814, "INSS-02", "INSS", "Documento que acredita la inscripción de una enttidad como demandante de vivienda pública", "Inscripción demandante  vivienda pública", 40 },
                    { 815, "LICM-01", "LICM", "Documento en el que se registran la titularidad originaria y las sucesivas transmisiones de las participaciones sociales en una compañía", "Constitución FAB: libro registro socios", 41 },
                    { 816, "LICM-02", "LICM", "Conjunto de documentos gráficos y escritos que, de acuerdo al artículo 6 de la Ley de Ordenación de la Edificación, recoge una descripción completa del inmueble, sus características y las explicaciones necesarias para su correcto uso y mantenimiento.", "Libro del edificio: memoria y anexos", 41 },
                    { 817, "LICM-03", "LICM", "Documento donde se registra la titularidad originaria y las sucesivas transmisiones, voluntarias o forzosas, de las participaciones sociales, así como la constitución de derechos reales y otros gravámenes sobre las mismas. En cada anotación se indica la identidad y domicilio del titular de la participación o del derecho o gravamen constituido sobre aquélla.", "Libro registro socios", 41 },
                    { 818, "LICM-04", "LICM", "Documento que describe las normas, la organización y los procedimientos que se deberán seguir para el correcto uso y mantenimiento de un activo", "Manual de uso y mantenimiento", 41 },
                    { 819, "LICM-05", "LICM", "Documento que describe las normas, la organización y los procedimientos que se deberán seguir para operar o efectuar el mantenimiento de la instalación.", "Manual operaciones y mantenimiento", 41 },
                    { 820, "LICM-06", "LICM", "Documento en el que se establecen las políticas y los procedimientos de control interno en materia de PBC (en el mismo se recogen los procedimientos en materia de diligencia debida, información, conservación de documentos, control interno, evaluación y gestión de riesgos), con objeto de prevenir e impedir operaciones relacionadas con el blanqueo de capitales o la financiación del terrorismo.", "Manual PBC", 41 },
                    { 821, "LICM-07", "LICM", "Documento de carácter obligatorio que ha de cumplimentar la Dirección Facultativa en el que se reseñan las órdenes, visitas que se produzcan en el desarrollo de la obra.", "Obra: libro de órdenes, asistencias", 41 },
                    { 822, "LICM-08", "LICM", "Documento de carácter obligatorio con fines de control y seguimiento del plan de seguridad y salud en que ese reflejan las incidencias ocurridas en esta materia", "Obra: libro incidencias", 41 },
                    { 823, "LICM-09", "LICM", "Libro habilitado por la autoridad laboral en el que el contratista debe reflejar, por orden cronológico desde el comienzo de los trabajos, todas y cada una de las subcontrataciones realizadas en la obra con empresas subcontratistas y trabajadores autónomos.", "Obra: libro subcontratación", 41 },
                    { 824, "LICM-10", "LICM", "Relación de inmuebles, incluida en el PGOU, sujetos a protección en virtud de la legislación reguladora del patrimonio histórico y artístico y los merecedores de protección en atención a sus valores y por razón urbanística, e incorpora el régimen de protección para su preservación.", "Plan general: catálogo", 41 },
                    { 825, "LICM-11", "LICM", "Relación de principios básicos que deben regir todas las actividades de la obra con el objetivo de lograr un entorno de trabajo seguro para la totalidad de intervinientes en ésta.", "Política prevención riesgos penales", 41 },
                    { 826, "LICM-12", "LICM", "Documento redactado por técnico competente en el que se especifica, de acuerdo a la normativa aplicable, el manual de prevención de caídas al mismo y distinto nivel.", "Seguridad y salud: manual prevención caídas", 41 },
                    { 827, "LICM-15", "LICM", "Documento redactado por el técnico competente, en el que se especifíca, de forma genérica, la relación de protocolos aplicables respecto a las disposiciones mínimas marcadas por la legislación, y referentes a la seguridad y salud de las obras de construcción, las cuales serán de aplicación siempre que lo exijan las características de la obra o de la actividad, las circunstancias o cualquier otro riesgo.", "Manual prevención riesgos laborales", 41 },
                    { 828, "LIPR-01", "LIPR", "Documento administrativo de carácter municipal, que se concede al propietario del negocio, obligatorio para que en un local, nave u oficina se pueda ejercer una actividad comercial, industrial o de servicios. Consiste en un documento que acredita el cumplimiento de las condiciones de habitabilidad y uso de esa actividad.", "Actividad y apertura: licencia", 42 },
                    { 829, "LIPR-02", "LIPR", "Documento administrativo de carácter municipal que habilita la realización de una actividad comercial concreta.", "Actividad: licencia comercial", 42 },
                    { 830, "LIPR-03", "LIPR", "Documento administrativo de nivel municipal, solicitado por la propiedad, que garantiza que el inmueble reúne las condiciones necesarias para vivir en éste. Dicho documento es imprescindible para contratar los servicios de electricidad, agua, gas y telecomunicaciones, concesiones de préstamos, así como para vender o alquilar una casa", "Obra: cédula de habitabilidad", 42 },
                    { 831, "LIPR-04", "LIPR", "Documento emitido por el Ayuntamiento que autoriza a demoler, total o parcialmente, un edificación o construcción conforme a la normativa y planeamiento urbanístico aplicable.", "Obra: licencia demolición", 42 },
                    { 832, "LIPR-05", "LIPR", "Documento necesario para el inicio de las obras. Es un permiso requerido, normalmente por la administración local, para la realización de cualquier tipo de construcción, supone la autorización municipal para realizar las obras. Su fin es comprobar la adecuación de la solicitud de licencia a lo establecido en la normativa urbanística. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Obra: licencia obras", 42 },
                    { 833, "LIPR-06", "LIPR", "Documento administrativo de carácter municipal que tiene por objeto acreditar que las actividades y las obras que se precisan para su implantación, modificación o cambio, han sido ejecutadas de conformidad con el proyecto y condiciones en que la licencia fue concedida, o con las variaciones que no suponen modificación de la licencia, y que se encuentran debidamente terminadas y aptas según las determinaciones urbanísticas, ambientales y de seguridad de su destino específico.", "Obra: licencia primera ocupación", 42 },
                    { 834, "LIPR-07", "LIPR", "Autorización que emite el Ayuntamiento necesaria para realizar el vaciado previo a la construcción del edificio", "Obra: licencia vaciado", 42 },
                    { 835, "LIPR-08", "LIPR", "Autorización para reservar la entrada al garaje de la finca edificada", "Obra: licencia vado", 42 },
                    { 836, "LIPR-09", "LIPR", "Otros permisos específicos en función de las necesidades y casuística de la obra a ejecutar", "Obra: otro permiso / licencia", 42 },
                    { 837, "LIPR-10", "LIPR", "Autorización emitida por el Ayuntamiento que posibilita la ocupación temporal de la vía publica con vehículos o maquinaria destinados a la realización de trabajos relacionados con obras de edificación.", "Obra: permiso ocupación vía pública", 42 },
                    { 838, "LIPR-11", "LIPR", "Documento administrativo de carácter municipal, emitido a solicitud del propietario con la presentación del proyecto, que tiene por objeto permitir la parcelación, segregación o agrupación de un inmueble que es preceptivo para la inscripción en el registro de la propiedad y en el catastro de la parcelación, segregación o agrupación.", "Proyecto parcelación/segregación/agrupación: licencia", 42 },
                    { 839, "LIPR-12", "LIPR", "Documento administrativo de carácter municipal que tiene por objeto autorizar la ejecución, apertura y utilización del espacio del inmueble destinado a aparcamiento de vehículos.", "Obra: licencia garaje", 42 },
                    { 840, "MEMO-01", "MEMO", "Trabajos preliminares para redactar el proyecto de obra, básico, de ejecución", "Anteproyecto: memoria", 43 },
                    { 841, "MEMO-02", "MEMO", "Documentación técnica que comprende los datos generales y la información descriptiva que define adecuadamente el catálogo de bienes y espacios protegidos", "Catálogo de bienes y espacios protegidos: memoria", 43 },
                    { 842, "MEMO-03", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el estudio de detalle.", "Estudio de detalle: memoria", 43 },
                    { 843, "MEMO-04", "MEMO", "Documento en el que se describen de manera pormenorizada todas las actuaciones desarrolladas durante un periodo de tiempo por parte de una entidad o institución.", "Cuentas anuales: Memoria actividades", 43 },
                    { 844, "MEMO-05", "MEMO", "Documento con carácter contractual que describe la naturaleza y calidad de los diferentes materiales que van a ser utilizados en la obra. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Memoria calidades", 43 },
                    { 845, "MEMO-06", "MEMO", "Documento en el que se describen de manera pormenorizada todas las actuaciones desarrolladas en el plan", "Normas complementarias: memoria plan", 43 },
                    { 846, "MEMO-07", "MEMO", "Documento en el que se describen de manera pormenorizada todas las actuaciones desarrolladas en las normas subsidiarias.", "Normas subsidiarias: memoria plan", 43 },
                    { 847, "MEMO-08", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el proyecto", "Proyecto de delimitación de suelo urbano: memoria", 43 },
                    { 848, "MEMO-09", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan de sectorización/delimitación.", "Plan de sectorización/delimitación: memoria", 43 },
                    { 849, "MEMO-10", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan de singular interés.", "Plan de singular interés: memoria", 43 },
                    { 850, "MEMO-11", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan especial de reforma interior.", "Plan especial de reforma interior: memoria", 43 },
                    { 851, "MEMO-12", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan especial.", "Plan especial: memoria", 43 },
                    { 852, "MEMO-13", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan general de ordenación territorial.", "Plan general de ordenación territorial: memoria", 43 },
                    { 853, "MEMO-14", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el plan general.", "Plan general: memoria", 43 },
                    { 854, "MEMO-15", "MEMO", "g-", "Plan parcial: memoria", 43 },
                    { 855, "MEMO-16", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el proyecto básico.", "Proyecto básico: memoria", 43 },
                    { 856, "MEMO-17", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el proyecto de ejecución.", "Proyecto ejecución: memoria", 43 },
                    { 857, "MEMO-18", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el proyecto de equidistribución.", "Proyecto equidistribución: memoria", 43 },
                    { 858, "MEMO-19", "MEMO", "Documentación técnica que comprende los datos generales, la información descriptiva y constructiva necesarios para definir adecuadamente el proyecto de urbanización.", "Proyecto urbanización: memoria", 43 },
                    { 859, "NORM-01", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al catálogo de bienes y espacios protegidos", "Catálogo de bienes y espacios protegidos: normas urbanísticas", 44 },
                    { 860, "NORM-02", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al estudio de detalle", "Estudio de detalle: normas urbanísticas", 44 },
                    { 861, "NORM-03", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables a las instrucciones de planeamiento", "Instrucciones de planeamiento: norma / disposición", 44 },
                    { 862, "NORM-04", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables a las normas complementarias", "Normas complementarias: normas urbanísticas", 44 },
                    { 863, "NORM-05", "NORM", "Disposiciones con carácter general dictadas en el ámbito de las Administraciones públicas y dirigidas a los ciudadanos, en virtud de las que se reglamentan algunos aspectos propios del ámbito de su competencia.", "Normas subsidiarias: ordenanzas", 44 },
                    { 864, "NORM-06", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables a las normas subsidiarias", "Normas subsidiarias: normas urbanísticas", 44 },
                    { 865, "NORM-07", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables a las normas técnicas de planeamiento", "Normas técnicas de planeamiento: norma / disposición", 44 },
                    { 866, "NORM-08", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al proyecto", "Proyecto de delimitación de suelo urbano: normas urbanísticas", 44 },
                    { 867, "NORM-09", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan de sectorizacion/delimitación.", "Plan de sectorización/delimitación: normas urbanísticas", 44 },
                    { 868, "NORM-10", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan de singular interés.", "Plan de singular interés: normas urbanísticas", 44 },
                    { 869, "NORM-11", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan especial de reforma interior.", "Plan especial de reforma interior: normas urbanísticas", 44 },
                    { 870, "NORM-12", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan especial.", "Plan especial: normas urbanísticas", 44 },
                    { 871, "NORM-13", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan general de ordenación territorial.", "Plan general de ordenación territorial: normas urbanísticas", 44 },
                    { 872, "NORM-14", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan general.", "Plan general: normas urbanísticas", 44 },
                    { 873, "NORM-15", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al plan parcial.", "Plan parcial: normas urbanísticas", 44 },
                    { 874, "NORM-16", "NORM", "Conjunto de disposiciones que contiene la reglamentación detallada del uso del suelo, volumen y condiciones higiénico-sanitarias de los terrenos y construcciones, así como las características estéticas de la ordenación, de la edificación y de su entorno aplicables al proyecto de ejecución.", "Proyecto ejecución: normativa aplicable", 44 },
                    { 875, "NOTS-01", "NOTS", "Documento emitido por le Registro de la Propiedad a través de la cual se informa de los asientos de inscripción en vigor de una finca concreta y que proporciona información breve y concisa sobre la situación jurídica de una finca.: descripción, propietarios, derechos y cargas si existen y servidumbres.", "Registro Propiedad: nota simple / literal", 45 },
                    { 876, "NOTS-02", "NOTS", "Documento emitido por le Registro Central de Indices de la Propiedad a través de la cual se informa de derechos (propiedad y otros derechos sobre inmuebles) inscritos en los distintos Registos de la Propiedad a favor de una persona concreta.", "Registro de la propiedad: informacion de indices", 45 },
                    { 877, "NOVA-01", "NOVA", "Contrato o escritura pública a través del cual las partes acuerdan la cesión de un crédito referente a un contrato.", "Cesión crédito: escritura/ contrato", 46 },
                    { 878, "NOVA-02", "NOVA", "Certificado de levantamiento de traba sobre IPF (Pto Financiero)", "Documento liberación IPF", 46 },
                    { 879, "NOVA-03", "NOVA", "Escritura o contrato privado de novación en la que se libera del pago a los fiadores", "Liberación fiadores: escritura/ contrato", 46 },
                    { 880, "NOVA-04", "NOVA", "Documento público/privado que recoge los acuerdos adoptados entre las partes", "Novación: escritura/contrato", 46 },
                    { 881, "NOVA-05", "NOVA", "Documento público/privado que recoge los acuerdos adoptados entre las partes en una Quita", "Quita: escritura/ contrato", 46 },
                    { 882, "NOVA-06", "NOVA", "Documento público/privado que recoge los acuerdos adoptados entre las partes en una Refinanciación", "Refinanciación: escritura/contrato", 46 },
                    { 883, "OFER-01", "OFER", "Documento emitido por el interesado en la adquisición o arrendamiento de un bien en el que se especifican, entre otros, el precio y forma de pago y otras las condiciones vinculadas a la oferta.", "Oferta transacción", 47 },
                    { 884, "OFER-03", "OFER", "Documento que detalla la oferta recibida referente a un activo propiedad de una entidad.", "Venta activo entidad: oferta", 47 },
                    { 885, "OFER-04", "OFER", "Documento, presentado ante el órgano judicial competente, en el que se especifican las condiciones económicas ofertadas por el interesado para la adquisición de un bien en subasta.", "Subasta: hoja de puja", 47 },
                    { 886, "OFER-07", "OFER", "Documento que contiene un extracto de las ofertas existentes sobre un activo u operación", "Ofertas: resumen", 47 },
                    { 887, "PBLO-01", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las bases y estatutos", "Bases y estatutos: boletín oficial acuerdo aprobación", 48 },
                    { 888, "PBLO-02", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del catálogo de bienes y espacios protegidos", "Catálogo de bienes y espacios protegidos: publicación acuerdo información pública / aprobación", 48 },
                    { 889, "PBLO-03", "PBLO", "Copia del anuncio publicado en el boletín oficial referente a la declaración del concurso de acreedores", "Declaración concurso: publicación", 48 },
                    { 890, "PBLO-04", "PBLO", "Publicación de la convocatoria de la vista de oposición en procedimiento de ejecución", "Ejecución: publicación convocatoria vista de oposición", 48 },
                    { 891, "PBLO-05", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del estudio de detalle", "Estudio de detalle: publicación acuerdo información pública / aprobación", 48 },
                    { 892, "PBLO-06", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones de planeamiento", "Instrucciones de planeamiento: Publicación acuerdo información pública / aprobación", 48 },
                    { 893, "PBLO-07", "PBLO", "Publicación en el BOE o en el Registro Publico Concursal del Plan de Liquidación", "Liquidación concurso: publicación", 48 },
                    { 894, "PBLO-08", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las normas complementarias", "Normas complementarias: Publicación acuerdo información pública / aprobación", 48 },
                    { 895, "PBLO-09", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las normas subsidiarias", "Normas subsidiarias: Publicación acuerdo información pública / aprobación", 48 },
                    { 896, "PBLO-10", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones de las normas técnicas de planeamiento", "Normas técnicas de planeamiento: Publicación acuerdo información pública / aprobación", 48 },
                    { 897, "PBLO-11", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones de las ordenanzas", "Ordenanzas: Publicación acuerdo información pública / aprobación", 48 },
                    { 898, "PBLO-12", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del proyecto", "Proyecto de delimitación de suelo urbano: publicación acuerdo información pública / aprobación", 48 },
                    { 899, "PBLO-13", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan", "Plan de sectorización/delimitación: publicación acuerdo información pública / aprobación", 48 },
                    { 900, "PBLO-14", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan de singular  interés.", "Plan de singular interés: publicación acuerdo información pública / aprobación", 48 },
                    { 901, "PBLO-15", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan especial de reforma interior.", "Plan especial de reforma interior: publicación acuerdo información pública / aprobación", 48 },
                    { 902, "PBLO-16", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan especial.", "Plan especial: publicación acuerdo información pública / aprobación", 48 },
                    { 903, "PBLO-17", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan general de ordenación territorial.", "Plan general de ordenación territorial: publicación acuerdo información pública / aprobación", 48 },
                    { 904, "PBLO-18", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan general.", "Plan general: publicación acuerdo información pública / aprobación", 48 },
                    { 905, "PBLO-19", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación de las instrucciones del plan parcial.", "Plan parcial: publicación acuerdo información pública / aprobación", 48 },
                    { 906, "PBLO-20", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del programa del programa de actuación", "Programa de actuación: publicación acuerdo información pública / aprobación", 48 },
                    { 907, "PBLO-21", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del proyecto de actuación especial.", "Proyecto actuación especial: publicación acuerdo información pública / aprobación", 48 },
                    { 908, "PBLO-22", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del proyecto de calificación urbanística.", "Proyecto calificación urbanística: publicación acuerdo información pública/ aprobación", 48 },
                    { 909, "PBLO-23", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del proyecto de equidistribución.", "Proyecto equidistribución: publicación acuerdo información pública / aprobación", 48 },
                    { 910, "PBLO-24", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del proyecto de expropiación.", "Proyecto expropiación: publicación acuerdo información pública / aprobación", 48 },
                    { 911, "PBLO-25", "PBLO", "Copia del anuncio publicado en el boletín oficial referente al acuerdo de aprobación del proyecto de urbanización.", "Proyecto urbanización: publicación acuerdo información pública / aprobación", 48 },
                    { 912, "PBLO-26", "PBLO", "Documento emitido por el juzgado en el que se detallan las distintas bases que regirán la subasta del activo.", "Subasta: publicación bases", 48 },
                    { 913, "PBLO-27", "PBLO", "Documento emitido por el juzgado en el que se anuncia la celebración de la subasta del activo.", "Subasta: publicación convocatoria", 48 },
                    { 914, "PBLO-28", "PBLO", "Mandamiento o decreto publicado por el organo judicial a través del cual se publica la providencia acordada por el organo judicial competente respecto a la futura subasta de bienes del ejecutado, en el que se especifican los detalles de la subasta y del bien subastado.", "Subasta: edicto", 48 },
                    { 915, "PBLO-29", "PBLO", "Anuncio en el periódico oficial que proceda, atendiendo al ámbito territorial de competencia del órgano autor de la actividad administrativa recurrida, de la interposición del recurso contenciosa administrativo", "Contencioso administrativo: Publicación del recurso", 48 },
                    { 916, "PBLO-30", "PBLO", "Anuncio publicado en Diario Oficial a fin de que cualquier persona física o jurídica pueda examinar el expediente, o la parte del mismo que se acuerde. Sin que este hecho le otorgue condición de interesado", "Expediete Administrativo: publicación", 48 },
                    { 917, "PLAO-01", "PLAO", "Documentación gráfica asociada al informe técnico que se acompaña a la solicitud de licencia de actividad", "Actividad: plano Informe técnico", 49 },
                    { 918, "PLAO-02", "PLAO", "Documentación gráfica que se acompaña al anteproyecto.", "Anteproyecto: plano", 49 },
                    { 919, "PLAO-03", "PLAO", "Documentación gráfica que refleja el estado definitivo de la obra una vez que ésta se ha terminado en los que aparecen recogidos todos los cambios que haya habido a lo largo de toda la ejecución de la obra", "As built: plano", 49 },
                    { 920, "PLAO-04", "PLAO", "Documentación gráfica asociada al catálogo de bienes y espacios protegidos", "Catálogo de bienes y espacios protegidos: plano", 49 },
                    { 921, "PLAO-05", "PLAO", "Documentación gráfica que se acompaña al estudio arqueológico", "Estudio arqueológico: plano", 49 },
                    { 922, "PLAO-06", "PLAO", "Documentación gráfica asociada al estudio de detalle", "Estudio de detalle: plano", 49 },
                    { 923, "PLAO-07", "PLAO", "Documentación gráfica que se acompaña al estudio geotécnico.", "Estudio geotécnico: plano", 49 },
                    { 924, "PLAO-08", "PLAO", "Documentación gráfica que se acompaña al estudio topográfico", "Estudio topográfico: plano", 49 },
                    { 925, "PLAO-09", "PLAO", "Documento con la representación gráfica ilustrativa del proyecto sobre el cual se realiza un análisis orientado a determinar la rentabilidad económica y viabilidad (técnica, legal,…) del proyecto", "Estudio viabilidad: croquis", 49 },
                    { 926, "PLAO-10", "PLAO", "Documentación gráfica asociada a las normas complementarias", "Normas complementarias: Plano plan", 49 },
                    { 927, "PLAO-11", "PLAO", "Documentación gráfica asociada a las normas subsidiarias", "Normas subsidiarias: Plano plan", 49 },
                    { 928, "PLAO-12", "PLAO", "Documentación gráfica que se acompaña al plan de control de calidad de la urbanización.", "Plan control calidad urbanización: plano", 49 },
                    { 929, "PLAO-13", "PLAO", "Documentación gráfica asociada al proyecto", "Proyecto de delimitación de suelo urbano: plano", 49 },
                    { 930, "PLAO-14", "PLAO", "Documentación gráfica asociada al plan de sectorización/delimitación.", "Plan de sectorización/delimitación: planos - documentación gráfica", 49 },
                    { 931, "PLAO-15", "PLAO", "Documentación gráfica asociada al plan de singular interés.", "Plan de singular interés: plano", 49 },
                    { 932, "PLAO-16", "PLAO", "Documentación gráfica asociada al plan especial de reforma interior.", "Plan especial de reforma interior: planos", 49 },
                    { 933, "PLAO-17", "PLAO", "Documentación gráfica asociada al plan especial.", "Plan especial: planos - documentación gráfica", 49 },
                    { 934, "PLAO-18", "PLAO", "Documentación gráfica asociada al plan general de ordenación territorial.", "Plan general de ordenación territorial: plano", 49 },
                    { 935, "PLAO-19", "PLAO", "Documentación gráfica asociada al plan general.", "Plan general: plano", 49 },
                    { 936, "PLAO-20", "PLAO", "Documentación gráfica que se acompaña al plan de gestión de residuos.", "Plan gestión residuos: plano", 49 },
                    { 937, "PLAO-21", "PLAO", "Documentación gráfica asociada al plan parcial.", "Plan parcial: plano", 49 },
                    { 938, "PLAO-22", "PLAO", "Documentación gráfica que se acompaña al plan de seguridad y salud.", "Plan seguridad y salud: plano", 49 },
                    { 939, "PLAO-24", "PLAO", "Documentación gráfica del inmueble a efectos comerciales", "Plano comercial", 49 },
                    { 940, "PLAO-25", "PLAO", "Documentación gráfica que localiza y detalla el punto en concreto donde se ha detectado la contingencia", "Plano contingencia", 49 },
                    { 941, "PLAO-26", "PLAO", "Documentación gráfica del inmueble en el que se detalla la distribución del mismo", "Plano distribución", 49 },
                    { 942, "PLAO-27", "PLAO", "Documentación gráfica en el que se especifica la ubicación concreta del inmueble. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Plano situación", 49 },
                    { 943, "PLAO-28", "PLAO", "Documentación gráfica asociada al programa de actuación.", "Programa de actuación: plano", 49 },
                    { 944, "PLAO-29", "PLAO", "Documentación gráfica que se acompaña al plan de actuación especial.", "Proyecto actuación especial: plano", 49 },
                    { 945, "PLAO-30", "PLAO", "Documentación gráfica que se acompaña al proyecto básico.", "Proyecto básico: plano", 49 },
                    { 946, "PLAO-31", "PLAO", "Documentación gráfica que se acompaña al proyecto de calificación urbanística.", "Proyecto calificación urbanística: plano", 49 },
                    { 947, "PLAO-32", "PLAO", "Documentación gráfica que se acompaña al proyecto de ejecución.", "Proyecto ejecución: plano", 49 },
                    { 948, "PLAO-33", "PLAO", "Documentación gráfica que se acompaña al proyecto de equidistribución.", "Proyecto equidistribución: plano", 49 },
                    { 949, "PLAO-34", "PLAO", "Documentación gráfica que se acompaña al proyecto de expropiación.", "Proyecto expropiación: plano", 49 },
                    { 950, "PLAO-35", "PLAO", "Documentación gráfica que se acompaña al proyecto de parcelación / segregación / agrupación.", "Proyecto parcelación / segregación / agrupación: plano", 49 },
                    { 951, "PLAO-36", "PLAO", "Documentación gráfica que se acompaña al proyecto de urbanización.", "Proyecto urbanización: plano", 49 },
                    { 952, "PLAP-01", "PLAP", "Plan diseñado por la sociedad gestora del fondo para el traspaso de un conjunto de activos y pasivos a otro FAB", "Fusión / Escisión FAB: proyecto", 50 },
                    { 953, "PLAP-02", "PLAP", "Plan de liquidación presentado por la administración concursal iniciada la fase de liquidación de la concursada", "Liquidación concurso: plan liquidación", 50 },
                    { 954, "PLAP-03", "PLAP", "Documento a través del cual se detalla la planificación, en actividades y tiempos, definida para la ejecución de una o varias actividades contenidas en un contrato de mantenimiento", "Plan de mantenimiento", 50 },
                    { 955, "PLAP-04", "PLAP", "Documento en donde se describe y explica un negocio que se va a realizar, así como diferentes aspectos relacionados con éste, tales como sus objetivos, las estrategias que se van a utilizar para alcanzar dichos objetivos, el proceso productivo, la inversión requerida y la rentabilidad esperada.", "Plan de negocio", 50 },
                    { 956, "PLAP-05", "PLAP", "Documento que recoge una relación de actuaciones ordenadas temporalmente, a fin de coordinar las actuaciones e inversiones públicas y privadas necesarias para el desarrollo y ejecución de las determinaciones del Plan.", "Plan de singular interés: directrices organización y gestión de la ejecución / programa de actuación y compromisos", 50 },
                    { 957, "PLAP-06", "PLAP", "Documento con carácter normativo y vinculante, que tiene por objeto establecer criterios y estrategias para adecuar la ordenación municipal a la política territorial e identificar los objetivos del Plan General.", "Plan general: directrices evolución urbana y ocupación del territorio", 50 },
                    { 958, "PLAP-07", "PLAP", "Estimación de los ingresos y gastos de la promoción, personificados en el tiempo, con el objeto de facilitar el seguimiento de ésta y hacer posible la previsión las posibles dificultades con el objetivo de tomar las medidas correctoras a tiempo. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Planificación económica de la promoción", 50 },
                    { 959, "PLAP-08", "PLAP", "Documento en el que se detalla las acciones y medios para el mantenimiento de un activo", "Plan de Mantenimiento Individualizado", 50 },
                    { 960, "PLAP-09", "PLAP", "Documento en el que se detallan los pasos, acciones y medios previos a realizar el plan de acción.", "Plan Preliminar de Acción", 50 },
                    { 961, "PLAP-10", "PLAP", "Documento en el que se detallan los pasos, acciones y medios para emprender una acción", "Plan de Acción", 50 },
                    { 962, "PLAP-11", "PLAP", "Evaluación emitida por  el administrador concursal en relación a las propuestas de pago recogidas en el convenio  y la adecuación del plan de viabilidad de una actividad empresarial", "Código de conducta", 50 },
                    { 963, "PLAT-01", "PLAT", "Documentación técnica incluida en el proyecto en la que se especifica el plan de control de calidad definido para el proyecto.", "Plan control calidad urbanización: documento", 51 },
                    { 964, "PLAT-02", "PLAT", "Documento redactado por el técnico competente en el que se especifica el tratamiento que se le tendrá que dar, de acuerdo a la normativa aplicable, a los residuos de construcción y demolición que se generarán durante la ejecución de la obra", "Plan gestión residuos: memoria y anexos", 51 },
                    { 965, "PLAT-03", "PLAT", "Documento redactado por el técnico competente en el que se especifica, de acuerdo a la normativa aplicable, el que se establecen las condiciones mínimas de seguridad y salud que deberán tenerse presentes durante la ejecución de las obras.", "Plan seguridad y salud: memoria y anexos", 51 },
                    { 966, "PRES-01", "PRES", "Estimación económica detalla emitida por la empresa constructora, de servicios o suministradora que recoge los costes asociados al contrato", "Contrato: presupuesto", 52 },
                    { 967, "PRES-02", "PRES", "Estimación económica detalla emitida por la el gestor en el que recoge la previsión de costes asociados a la actividad de la entidad.", "Derramas y cuotas: presupuesto", 52 },
                    { 968, "PRES-03", "PRES", "Documento que detalla, una vez aceptado el presupuesto y dado comienzo la ejecución de las obras, el precio que se da en el caso de nuevos trabajos a realizar no previstos, o previstos de forma distinta, por el proyecto. Es por ello que el precio contratado para las partidas modificadas se ve alterado o incluso podría llegar a suponer precios completamente nuevos, precisando por tanto de un nuevo acuerdo entre el promotor y la contrata para la continuación de dichas tareas.", "Obra: adicionales y/o contradictorios", 52 },
                    { 969, "PRES-04", "PRES", "Documento que refleja una previsión o predicción de cómo serán los resultados y los flujos de dinero que se obtendrán en un periodo futuro. Incluye los presupuestos anuales de una entidad. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Contrato: Presupuesto", 52 },
                    { 970, "PRES-05", "PRES", "Estimación económica detalla emitida por la empresa constructora que recoge los costes asociados a la ejecución material de lo proyectado. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Presupuesto ejecución material", 52 },
                    { 971, "PRES-07", "PRES", "Estimación económica detalla emitida por la empresa constructora, de servicios o suministradora que recoge los costes asociados a la inversión planificada", "Presupuesto inversión", 52 },
                    { 972, "PRES-08", "PRES", "Propuesta realizada por el Agente urbanizador que recoge el desarrollo de las relaciones entre el urbanizador y los propietarios, la estimación de la totalidad de los gastos de urbanización, la retribución del urbanizador, y la incidencia económica, de los compromisos que interese adquirir el urbanizador.", "Programa de actuación: proposición jurídica-económica", 52 },
                    { 973, "PRES-09", "PRES", "Valoración aproximada de la ejecución material de la obra proyectada por capítulos en el proyecto básico.", "Proyecto básico: presupuesto", 52 },
                    { 974, "PRES-10", "PRES", "Valoración aproximada de la ejecución material de la obra proyectada por capítulos en el proyecto de urbanización.", "Proyecto urbanización: presupuesto", 52 },
                    { 975, "PRES-11", "PRES", "Estimación económica detallada de los costes asociados a una acción de mantenimiento correctivo o preventivo concreta, a realizar sobre un activo.", "Presupuesto Correctivo/ Preventivo", 52 },
                    { 976, "PRES-13", "PRES", "Conjunto de los gastos e ingresos previstos para un determinado período de tiempo.", "Presupuesto", 52 },
                    { 977, "PRES-14", "PRES", "Cómputo anticipado del coste de la ejecución de un proyecto.", "Proyecto ejecución: presupuesto", 52 },
                    { 978, "PRES-15", "PRES", "Estimación de gastos  a incurrir o  ingresos a percibir como consecuencia de la ejecución de una acción concreta. Dentro de este tipo documental, a título de ejemplo, se incorporarán las estimaciones de gastos (impuestos locales, deudas con la comunidad, ingresos por alquiler,…) relacionados con un activo inmobiliario (REO o Colateral)", "Estimacion gastos / ingresos", 52 },
                    { 979, "PRPE-01", "PRPE", "Propuesta de compra de terceros durante la tramitación del concurso", "Calificación concurso: propuesta compra terceros", 53 },
                    { 980, "PRPE-02", "PRPE", "Propuesta de operación financiera en el trascurso de la tramitación del concurso", "Calificación concurso: propuesta operación financiera", 53 },
                    { 981, "PRPE-03", "PRPE", "Solicitud de autorización de constitución de un FAB a la CNMV", "Constitución FAB: solicitud autorización a la CNMV", 53 },
                    { 982, "PRPE-04", "PRPE", "Propuesta de convenio de acreedores presentada por el deudor en la fase de convenio o de convenio anticipado", "Convenio acreedores: propuesta", 53 },
                    { 983, "PRPE-05", "PRPE", "Documento que recoge la propuesta de dación", "Dación: propuesta", 53 },
                    { 984, "PRPE-06", "PRPE", "Solicitud de declaración de concurso presentada por el deudor/o acreedor ante el Juzgado Mercantil", "Declaración concurso: solicitud", 53 },
                    { 985, "PRPE-07", "PRPE", "Documento bancario que informa de la concesión, por parte de la entidad bancaria correspondiente, de la financiación necesaria para llevar a cabo una operación determinada", "Préstamo financiación operación: comunicación concesión financiación bancaria", 53 },
                    { 986, "PRPE-08", "PRPE", "Documento Propuesta de concesión de operación financiera", "Propuesta aprobación operación financiera", 53 },
                    { 987, "PRPE-09", "PRPE", "Documento de propuesta de  una determinada operación financiera o propuesta de actuación.", "Propuesta operación /actuación", 53 },
                    { 988, "PRPE-10", "PRPE", "Presupuesto sobre propuesta de Venta Paquete de 1-5 Activos", "Propuesta PDV", 53 },
                    { 989, "PRPE-11", "PRPE", "Documento que recoge la propuesta sobre regularización de un activo (tanto de la información obrante en un préstamo , las liquidaciones efectuadas, los cobros concretos ocasionados por un impago,...)", "Propuesta regularización", 53 },
                    { 990, "PRPE-13", "PRPE", "Propuesta de cobro total o parcial de duda pendiente por la aportación de fondos líquidos", "Recuperación fondos líquidos: propuesta", 53 },
                    { 991, "PRPE-14", "PRPE", "Solicitud de Operación Financiera", "Solicitud de la operación", 53 },
                    { 992, "PRPE-15", "PRPE", "Documento que manifiesta la propuesta sobre la acción de adaptación de un inmueble a las medidas reglamentarias.", "Propuesta adecuación inmueble", 53 },
                    { 993, "PRPE-20", "PRPE", "Documento que manifiesta la proposición de iniciar un pleito o altercación en juicio.", "Propuesta litigio", 53 },
                    { 994, "PRPE-21", "PRPE", "Documento que manifiesta el interés y la proposición de obtener algo.", "Solicitud adjudicación", 53 },
                    { 995, "PRPE-22", "PRPE", "Documento en el que se propone la votación de un convenio de acreedores.", "Propuesta votación convenio", 53 },
                    { 996, "PRPE-23", "PRPE", "Documento firmado en el que se pide la realización de una disposición.", "Solicitud firmada disposiciones", 53 },
                    { 997, "PRPE-24", "PRPE", "Documento en el que se propone un convenio de acreedores, en la fase de preconcurso.", "Preconcurso: propuesta convenio", 53 },
                    { 998, "PRPE-25", "PRPE", "Documento en el que se propone la constitución de una entidad (p.e. comunidad de propietarios,…)", "Propuesta constitución.", 53 },
                    { 999, "PRPE-26", "PRPE", "Escrito emitido por la administración concursal evaluando la propuesta anticipada de convenio presentada por el deudor", "Convenio: Propuesta anticipada de convenio", 53 },
                    { 1000, "PRPI-02", "PRPI", "Documento referido al análisis de división/estructuración de un activo, y las medidas o vías de actuación a tomar para sanear la operación", "Propuesta de segmentación y estrategia", 54 },
                    { 1001, "PRPI-05", "PRPI", "Documento referido a las medidas de actuación a tomar, para solventar problemas técnicos detectados.", "Propuesta de depuración de problemas técnicos y puesta en valor", 54 },
                    { 1002, "PRPI-07", "PRPI", "Listado de solicitudes de información sobre el valor del activo y las ofertas recibidas por el mismo.", "Relación Peticiones de Precios y Ofertas", 54 },
                    { 1003, "PRPI-08", "PRPI", "Documento de presentación de ideas a desarrollar sobre la contratación de servicios de vigilancia y medidas de seguridad.", "Propuesta de aseguramiento y vigilancia", 54 },
                    { 1004, "PRPI-18", "PRPI", "Documento en el cual se solicita la revocación de una operación.", "Solicitud cancelación operación", 54 },
                    { 1005, "PRPI-19", "PRPI", "Documento en el que se propone una determinada resolución", "Propuesta resolución", 54 },
                    { 1006, "PRYT-01", "PRYT", "Documentación técnica del proyecto que refleja el estado definitivo de la obra una vez que ésta se ha terminado en los que aparecen recogidos todos los cambios que ha habido a lo largo de toda la ejecución de la obra", "As built: memoria y anexos", 55 },
                    { 1007, "PRYT-02", "PRYT", "Documentación técnica asociada al proyecto. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Documentación técnica proyecto", 55 },
                    { 1008, "PRYT-03", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del estudio de detalle.", "Estudio de detalle: anexos memoria", 55 },
                    { 1009, "PRYT-04", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del plan de sectorización/delimitación.", "Plan de sectorización/delimitación: anexos memoria", 55 },
                    { 1010, "PRYT-05", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del plan especial de reforma interior.", "Plan especial de reforma interior: anexos memoria", 55 },
                    { 1011, "PRYT-06", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del plan especial.", "Plan especial: anexos memoria", 55 },
                    { 1012, "PRYT-07", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del plan parcial.", "Plan parcial: anexos memoria", 55 },
                    { 1013, "PRYT-08", "PRYT", "Documentación técnica que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de actuación especial.", "Proyecto actuación especial: memoria y anexos", 55 },
                    { 1014, "PRYT-09", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto básico.", "Proyecto básico: anexos", 55 },
                    { 1015, "PRYT-10", "PRYT", "Documentación técnica que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de calificación urbanística.", "Proyecto calificación urbanística: memoria y anexos", 55 },
                    { 1016, "PRYT-11", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de ejecución.", "Proyecto ejecución: anexos memoria", 55 },
                    { 1017, "PRYT-12", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de equidistribución.", "Proyecto equidistribución: anexos a la memoria", 55 },
                    { 1018, "PRYT-13", "PRYT", "Documentación técnica que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de expropiación.", "Proyecto expropiación: memoria y anexos", 55 },
                    { 1019, "PRYT-14", "PRYT", "Documentación técnica que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de parcelación / segregación / agrupación.", "Proyecto parcelación/segregación/agrupación: memoria y anexos", 55 },
                    { 1020, "PRYT-15", "PRYT", "Documentación técnica adicional a la memoria que contiene el conjunto de planos, dibujos, esquemas y textos explicativos asociadas a diversos aspectos del proyecto de urbanización.", "Proyecto urbanización: anexos a la memoria", 55 },
                    { 1021, "PRYT-16", "PRYT", "Documentación de proyectos no includos en otras tipologías del presente cuadro de clasificación", "Otros proyectos", 55 },
                    { 1022, "PUBM-01", "PUBM", "Copia del anuncio publicado en prensa referente a la comercialización del activo", "Anuncio comercialización", 56 },
                    { 1023, "PUBM-02", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación de Bases y Estatutos", "Bases y estatutos: publicación prensa acuerdo aprobación", 56 },
                    { 1024, "PUBM-03", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del catálogo de bienes y espacios protegidos", "Catálogo de bienes y espacios protegidos: publicación prensa", 56 },
                    { 1025, "PUBM-04", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del estudio de detalle", "Estudio de detalle: publicación prensa", 56 },
                    { 1026, "PUBM-05", "PUBM", "Copia de la publicación de proyecto de Fusión/Escisión FAB", "Fusión / Escisión FAB: publicación proyecto", 56 },
                    { 1027, "PUBM-06", "PUBM", "Copia de la información publicada en prensa referente al activo.", "Información en prensa", 56 },
                    { 1028, "PUBM-07", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación de las normas complementarias", "Normas complementarias: publicación prensa", 56 },
                    { 1029, "PUBM-08", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación de las normas subsidiarias", "Normas subsidiarias: publicación prensa normas", 56 },
                    { 1030, "PUBM-09", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de delimitación de suelo urbano.", "Proyecto de delimitación de suelo urbano: publicación prensa", 56 },
                    { 1031, "PUBM-10", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan de sectorización/delimitación.", "Plan de sectorización/delimitación: publicación prensa", 56 },
                    { 1032, "PUBM-11", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan de singular  interés.", "Plan de singular interés: publicación prensa", 56 },
                    { 1033, "PUBM-12", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan especial de reforma interior.", "Plan especial de reforma interior: publicación prensa", 56 },
                    { 1034, "PUBM-13", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan especial.", "Plan especial: publicación prensa", 56 },
                    { 1035, "PUBM-14", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan general de ordenación territorial.", "Plan general de ordenación territorial: publicación prensa", 56 },
                    { 1036, "PUBM-15", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan general.", "Plan general: publicación prensa", 56 },
                    { 1037, "PUBM-16", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del plan parcial.", "Plan parcial: publicación prensa", 56 },
                    { 1038, "PUBM-17", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del programa de actuación", "Programa de actuación: publicación prensa", 56 },
                    { 1039, "PUBM-18", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de actuación especial.", "Proyecto actuación especial: publicación prensa", 56 },
                    { 1040, "PUBM-19", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de calificación urbanística.", "Proyecto calificación urbanística: publicación prensa", 56 },
                    { 1041, "PUBM-20", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de equidistribución.", "Proyecto equidistribución: publicación prensa", 56 },
                    { 1042, "PUBM-21", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de expropiación.", "Proyecto expropiación: publicación prensa", 56 },
                    { 1043, "PUBM-22", "PUBM", "Copia del anuncio publicado en prensa referente al acuerdo de aprobación del proyecto de urbanización.", "Proyecto urbanización: publicación prensa", 56 },
                    { 1044, "RETR-01", "RETR", "Documento justificativo del cobro de la prestación contributiva a la cual se tiene derecho en determinadas situaciones de pérdida del trabajo y cuya duración y cuantía está determinada por el tiempo que el trabajador haya cotizado por desempleo en el régimen de la Seguridad Social que contemple este tipo de prestación.", "Desempleo", 57 },
                    { 1045, "RETR-02", "RETR", "Salarios o sueldos que se pagan al trabajador en dinero o en especie por el empresario privado o público, dependiendo de lo establecido contractualmente y dentro de las exigencias legales que el derecho laboral del país marque", "Nómina", 57 },
                    { 1046, "RETR-03", "RETR", "Pago, temporal o de por vida, que recibe una persona cuando se encuentra en una situación, establecida por ley en cada país, que la hace acreedora de hecho de una cantidad económica, ya sea de los sistemas públicos de previsión nacionales o de entidades privadas.", "Pensión", 57 },
                    { 1047, "SEGU-01", "SEGU", "Póliza seguro de robo que cubre un bien mobiliario", "Bien mobiliario: seguro robo", 58 },
                    { 1048, "SEGU-02", "SEGU", "Póliza seguro de daños que cubre un bien mobiliario", "Bien mobiliario: seguro daños", 58 },
                    { 1049, "SEGU-03", "SEGU", "Póliza de seguro obligatoria que, de acuerdo a la ley 57/1968 sobre el percibo de las cantidades anticipadas en la construcción y venta de viviendas, deberá ser suscrita por el promotor y que garantiza las cantidades entregadas a cuenta por los compradores ante los posibles incumplimientos.", "Cantidades entregadas en cuenta por el cliente: póliza", 58 },
                    { 1050, "SEGU-04", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de hogar", "Seguro daño y hogar: póliza", 58 },
                    { 1051, "SEGU-05", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de caución", "Seguro de caución: póliza", 58 },
                    { 1052, "SEGU-06", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza seguro Todo Riesgo a la Construcción", "Seguro de construcción (TRC): póliza", 58 },
                    { 1053, "SEGU-07", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro multirriesgo", "Seguro multirriesgo: póliza", 58 },
                    { 1054, "SEGU-08", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de paralización de obra", "Seguro obra paralizada: póliza", 58 },
                    { 1055, "SEGU-09", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de responsabilidad civil o de indemnización", "Seguro responsabilidad civil o indemnización: póliza", 58 },
                    { 1056, "SEGU-10", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de responsabilidad civil", "Seguro responsabilidad civil: póliza", 58 },
                    { 1057, "SEGU-11", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de responsabilidad decenal. Para aquellos casos en que el documento a incorporar sea referente a un colateral, la información de éste, una  vez formalizada la operación y por tanto constituida la garantía, deberá estar asignada a los tipos documentales específicos de garantías, es decir, los pertenecientes a la serie \"\"07 - Garantías\"\" del cuadro de Activos Financieros (con código de TDN2 comenzado por AF-07-…)", "Seguro responsabilidad decenal: póliza", 58 },
                    { 1058, "SEGU-12", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de responsabilidad medioambiental", "Seguro responsabilidad medioambiental: póliza", 58 },
                    { 1059, "SEGU-13", "SEGU", "Relación documentación asociada al seguro de responsabilidad civil que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro responsabilidad civil: otra documentación", 58 },
                    { 1060, "SEGU-14", "SEGU", "Relación documentación asociada al seguro de construcción que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro de construcción: otra documentación", 58 },
                    { 1061, "SEGU-15", "SEGU", "Relación documentación asociada al seguro de construcción (TRC) que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro de construcción (TRC): otra documentación", 58 },
                    { 1062, "SEGU-16", "SEGU", "Relación documentación asociada al seguro multirriesgo que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro multirriesgo: otra documentación", 58 },
                    { 1063, "SEGU-17", "SEGU", "Relación documentación asociada al seguro de responsabilidad decenal que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro responsabilidad decenal: otra documentación", 58 },
                    { 1064, "SEGU-18", "SEGU", "Relación documentación asociada al seguro de responsabilidad medioambiental que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro responsabilidad medioambiental: otra documentación", 58 },
                    { 1065, "SEGU-19", "SEGU", "Relación documentación asociada al seguro de obra paralizada que no se encuentra especificada como documento independiente en la presente clasificacion", "Seguro obra paralizada: otra documentación", 58 },
                    { 1066, "SEGU-20", "SEGU", "Documento que recoge el contrato y clausulado aplicable a la póliza de seguro de percepción de rentas.", "Seguro percepción rentas: póliza", 58 },
                    { 1067, "SEGU-21", "SEGU", "Relación de documentación asociada al seguro de percepción de rentas, que no se encuentra especificada como documento independiente en la presente clasificación.", "Seguro percepción rentas: otra documentación", 58 },
                    { 1068, "SERE-01", "SERE", "Resolución judicial respecto al incidente concursal incoado por alguna de las partes del procedimiento (p.e. recusación de administradores, anulación de actos del deudor, acción rescisoria, oposición calificación concurso,…)", "Incidente concursal: resolución demanda", 59 },
                    { 1069, "SERE-02", "SERE", "Resolución sobre la impugnación solicitada por las partes personadas respecto a la lista de bienes y obligaciones.", "Administración concursal: resolución impugnación lista bienes y obligaciones", 59 },
                    { 1070, "SERE-03", "SERE", "Documento donde se detalla la resolución emitida por un juez o un tribunal con la cual se concluye el procedimiento sobre la calificación de Concurso de Acreedores", "Calificación concurso: sentencia", 59 },
                    { 1071, "SERE-04", "SERE", "Auto de conclusión del concurso por cumplimiento del Convenio de Acreedores", "Convenio acreedores: auto conclusión por cumplimiento", 59 },
                    { 1072, "SERE-05", "SERE", "Resolución judicial con la que se aprueba la propuesta de convenio una vez estudiado el informe de la administración concursal sobre la viabilidad del mismo,  o se concluye el procedimiento sobre la aprobación del convenio de Acreedores", "Convenio acreedores: auto judicial aprobación", 59 },
                    { 1073, "SERE-06", "SERE", "Declaración Judicial de Cumplimiento de convenio y finalización del Concurso de Acreedores", "Convenio acreedores: declaración judicial cumplimiento", 59 },
                    { 1074, "SERE-07", "SERE", "Escrito de acreedor/es oponiéndose al texto del convenio de acreedores", "Convenio acreedores: oposición al convenio", 59 },
                    { 1075, "SERE-08", "SERE", "Resolución Judicial sobre la oposición al convenio de acreedores", "Convenio acreedores: resolución sobre la oposición a la aprobación", 59 },
                    { 1076, "SERE-09", "SERE", "Documento donde se detalla la resolución emitida por un juez o un tribunal con la cual se concluye el procedimiento sobre incumplimiento del convenio e acreedores", "Convenio acreedores: sentencia sobre incumplimiento", 59 },
                    { 1077, "SERE-10", "SERE", "Auto y nombramiento del Administrador Concursal", "Declaración concurso: auto y nombramiento administrador", 59 },
                    { 1078, "SERE-11", "SERE", "Documento donde se detalla la resolución emitida por un juez o un tribunal con la cual se concluye el procedimiento sobre el desahucio instado por la propiedad del activo", "Desahucio: sentencia", 59 },
                    { 1079, "SERE-12", "SERE", "Documento por el que el juez o letrado de la administración de justicia  admite o inadmite a trámite la demanda o el escrito de solicitud presentada por las partes, o bien declara la falta de jurisdicción y/o competencia", "Ejecución: auto admisión a tramite demanda", 59 },
                    { 1080, "SERE-13", "SERE", "Documento por el que el juez o tribunal acuerda las medidas de embargo, localización de bienes y requerimiento de pago en un procedimiento de ejecución", "Ejecución: decreto con medidas concretas embargo, localización de bienes y, requerimiento pago", 59 },
                    { 1081, "SERE-14", "SERE", "Documento por el que el juez o tribunal entiende que no concurren los presupuestos y requisitos legalmente exigidos para el despacho de la ejecución.", "Ejecución: denegación del despacho", 59 },
                    { 1082, "SERE-15", "SERE", "Documento que contiene el auto con el que el juez o tribunal acuerdan la orden general de ejecución y despacho de la misma (incluyendo la ejecución provisional)", "Ejecución: orden general", 59 },
                    { 1083, "SERE-16", "SERE", "Documento emitido por el juez o tribunal que contiene la extensión del embargo previamente acordado o bien el embargo de un bien ya previamente embargado o de bienes que sobren en la realización forzosa de bienes celebrada en otra ejecución ya despachada", "Ejecución: resolución mejora de embargo", 59 },
                    { 1084, "SERE-17", "SERE", "Documento emitido por el juez o tribunal que decide sobre la oposición presentada por el demandado argumentando y justificando documentalmente el pago o cumplimiento de lo ordenado en la sentencia", "Ejecución: resolución oposición", 59 },
                    { 1085, "SERE-18", "SERE", "Documento emitido por el juez o tribunal que decide sobre lo acaecido en la vista de oposición celebrada", "Ejecución: resolución vista de oposición", 59 },
                    { 1086, "SERE-19", "SERE", "Auto de Apertura de la Liquidación en el ámbito del concurso de acreedores", "Liquidación concurso: auto apertura", 59 },
                    { 1087, "SERE-20", "SERE", "Auto de aprobación del Plan de Liquidación presentado por la Administración Concursal", "Liquidación concurso: auto aprobación del plan", 59 },
                    { 1088, "SERE-21", "SERE", "Auto de adjudicación de un bien o unidad productiva en el ámbito del procedimiento concursal", "Liquidación concurso: auto de adjudicación concursal", 59 },
                    { 1089, "SERE-22", "SERE", "Resolución y archivo de actuaciones en el marco de la Fase de Liquidación del Concurso de Acreedores", "Liquidación concurso: resolución y archivo de actuaciones", 59 },
                    { 1090, "SERE-23", "SERE", "Documento de naturaleza judicial redactado por el Secretario Judicial a través del cual se deja constancia de la celebración del remate con su desarrollo e incidentes, especificando, entre otros, los participantes en la subasta, las posturas formuladas y se concreta la mejor oferta recibida. Dentro de este tipo documental se incorpora tanto el acta como la certificación de cierre de la subasta", "Subasta: acta", 59 },
                    { 1091, "SERE-24", "SERE", "Auto/Decreto de adjudicación emitido por el juzgado en el procedimiento de ejecución.", "Subasta: auto / decreto adjudicación", 59 },
                    { 1092, "SERE-25", "SERE", "Mandamiento remitido a los registros públicos por los que se dictamina el levantamiento de las cargas que figuraban inscritas", "Subasta: mandamiento cancelación cargas", 59 },
                    { 1093, "SERE-26", "SERE", "Testimonio del decreto de adjudicación emitido por el juzgado en un procedimiento de ejecución", "Subasta: testimonio decreto adjudicación", 59 },
                    { 1094, "SERE-27", "SERE", "Resolución judicial referente a la tercería de dominio presentada por un tercero que alega poseer dominio de un bien sobre el que se ha instado la ejecución", "Tercería de dominio: resolución", 59 },
                    { 1095, "SERE-28", "SERE", "Resolucion judiuical referente a  un procedinmiento judicial de usurpación", "Usurpación: sentencia", 59 },
                    { 1096, "SERE-29", "SERE", "Acto procesal proveniente de un tribunal por el que se resuelve un recurso", "Recurso apelación: resolución.", 59 },
                    { 1097, "SERE-30", "SERE", "Acto procesal proveniente de un tribunal por el que se resuelve la aprobación del preconcurso.", "Preconcurso: resolución", 59 },
                    { 1098, "SERE-31", "SERE", "Documento que acredita la resolución judicial de suspensión de subasta.", "Subasta: resolución suspensión", 59 },
                    { 1099, "SERE-32", "SERE", "Resolución del Tribunal por la cual fija el día y hora para comparecer o realizar determinado trámite procesal, como juicio , vista, práctica prueba, subasta, audiencia,", "Auto señalamiento", 59 },
                    { 1100, "SERE-33", "SERE", "Documento judicial, edicto/testimonio/… que acredita la titularidad del activo a favor de un tercero anterior a Sareb  (p.e. la entidad cedente)", "Subasta: documento de titularidad anterior a Sareb", 59 },
                    { 1101, "SERE-34", "SERE", "Resoluciones  dictadas bien por el juez,  que deciden definitivamente sobre el procedimiento, recurso, cuestión incidental o declaración de  firmeza de la sentencia ,  bien  por letrado de la administración de justicia en los supuestos que la ley establezca que deba resolver el procedimiento.", "Fin procedimiento: Sentencia, auto y decreto", 59 },
                    { 1102, "SERE-35", "SERE", "Resolución judicial emitida por el letrado de la administración de justicia acordando la suspensión del procedimiento en los casos previstos en la ley.", "Decreto acordando la suspensión", 59 },
                    { 1103, "SERE-36", "SERE", "Resolución del juez que decide sobre la falta de jurisdicción o competencia,", "Auto resolviendo declinatoria", 59 },
                    { 1104, "SERE-37", "SERE", "Acto por el cual el órgano judicial comunica a las partes, testigos, peritos etc.  hora y día para que se presenten o  comparezcan en la sede del tribunal para realizar un determinado acto", "Requerimiento, citación y emplazamiento", 59 },
                    { 1105, "SERE-38", "SERE", "Resolución judicial por lo que se comunica el ejecutado la actuación de embargo practicada", "Ejecución: Notificación diligencia de embargo", 59 },
                    { 1106, "SERE-39", "SERE", "Acto procesal del letrado de la administración de justicia mediente el cual agrupa los bienes para su posterior subasta", "Subasta: Confección/Asignación de lotes", 59 },
                    { 1107, "SERE-40", "SERE", "Resolución judicial (Decreto) por la que se acepta la mejor postura ofertada en la subasta, otorgando al postor preferencia en la adjudicación del bien subastado", "Subasta: Aprobación del remate", 59 },
                    { 1108, "SERE-41", "SERE", "Resolución del juez por la que admite a trámite la solicitud de homologación", "Preconcurso: Providencia admitiendo la homologación de acuerdos de refinanciación", 59 },
                    { 1109, "SERE-42", "SERE", "Resolución del juez en la que se pronuncia sobre la declaración formal del concurso. Abriendo la fase común de tramitación", "Preconcurso: Auto de declaración del concurso", 59 },
                    { 1110, "SERE-43", "SERE", "Aceptación por parte de los acreedores de la propuesta de convenio", "Preconcurso: aceptación propuesta de convenio", 59 },
                    { 1111, "SERE-44", "SERE", "Resolución del juez por la cual aprueba o modifica  el plan de liquidación presentado por la administración concursal o bien por el deudor o acreedores", "Concurso: Auto aprobando o modificando el plan de liquidación", 59 },
                    { 1112, "SERE-45", "SERE", "Resolución del juez por la que suspende de manera parcial/total las operaciones de liquidación", "Concurso: Auto supensión parcial fase liquidación", 59 },
                    { 1113, "SERE-46", "SERE", "Resolución del juez por la que acuerda la prórroga del plan de liquidación solicitada por la administración concursal, debido a la complejidad del concurso", "Concurso: Prórroga plan liquidación", 59 },
                    { 1114, "SERE-47", "SERE", "Resolución del juez por la que da por finalizado la fase común y abriendo la fase de convenio, en el caso de que el concursado no hubiese optado previamente por la liquidación ni se hubiera aprobado convenio anticipado. En el auto también se ordenará la convocatoria de la junta.", "Fase común: auto apertura fase de convenio", 59 },
                    { 1115, "SERE-48", "SERE", "Resolución del juez por la que aprueba la rendición de cuentas presentada por la administración concursal", "Administración concursal: auto aprobación de cuentas", 59 },
                    { 1116, "SERE-49", "SERE", "Resolución judicial por la que el juez admite la propuesta de convenio al reunir los requisitos exigidos por la ley, o la inadmite por no cumplir con los requisitos de adhesión, por infringir exigencias legales  o el deudor estar incurso el prohibición legal", "Convenio: Admisión/ inadmisión a trámite de la propuesta anticipada de convenio", 59 },
                    { 1117, "SERE-50", "SERE", "Resolución del juez por la que admite a trámite la propuesta de convenio, una vez realizado control judicial previo de temporaneidad y adecuación de la propuesta a los requisitos legales de forma y contenido", "Convenio acreedores: providencia admisión a tramite", 59 },
                    { 1118, "SERE-51", "SERE", "Resolución del juez por la que aprueba al apertura de la fase de liquidación solicitada bien por el deudor, los acreedores o bien de oficio por el juez", "Concurso: Auto apertura fase de liquidación", 59 },
                    { 1119, "SERE-52", "SERE", "Resolución del juez por la que aprueba la liquidación anticipada presentada por el deudor, acordando al apertura de la fase de liquidación", "Liquidación concurso: Auto aprobando liquidación anticipada.", 59 },
                    { 1120, "SERE-53", "SERE", "Acto procesal en virtud del cual el letrado de la administración de justicia instruye al ofendido  o perjudicado por el delito de su derecho a mostrarse parte en el proceso y ejercitar las acciones penales y civiles que procedan", "Procedimiento abreviado: Ofrecimiento acciones", 59 },
                    { 1121, "SERE-54", "SERE", "Resolución por la que el juez ordena la comparecencia del investigado o el testigo para la toma de declaración durante la tramitación de las diligencias previas", "Procedimiento abreviado: Diligencia para la toma declaración de investigado / testigo", 59 },
                    { 1122, "SERE-55", "SERE", "Resolución en la que juez comunica la fecha para la comparencia del investigado o testigos en la tramitación de la diligencias previas", "Procedimiento abreviado Fecha señalamiento para declaración del investigado / testigo", 59 },
                    { 1123, "SERE-56", "SERE", "Resolución del juez por la que acuerda la conclusión de las diligencias previas y la continuación de la causa por los trámites del procedimiento abreviado e inicio de la fase intermedia, y donde se define el objeto del juicio, hechos materia de acusación, y medios de pruebas admitidos", "Procedimiento abreviado: Auo de apertura", 59 },
                    { 1124, "SERE-57", "SERE", "Resolución del juez por la que admite o inadmite nuevas pruebas presentadas con posterioridad al escrito de calificación", "Procedimiento abreviado: Auto admisión /inadmisión de pruebas", 59 },
                    { 1125, "SERE-58", "SERE", "Resolución del juez en la que pone en conocimiento del ministerio fiscal un hecho con apariencia de delito o falta perseguible de oficio, por si ha lugar al ejercicio de la acción penal", "Providencia prejudicial penal", 59 },
                    { 1126, "SERE-59", "SERE", "Resolución del juez en la que acuerda la suspensión del proceso civil por concurrir una serie de circustancias durante la tramitación de las cuestiones prejudiciales : existencia de causa criminal, que la decisión del tribunal penal acerca del hecho pueda tener influencia decisiva en la resolución del asunto civil", "Auto Suspensión actuaciones en el caso de prejudicialidad penal", 59 },
                    { 1127, "SERE-60", "SERE", "Resolución del letrado de la administración de justicia por la que solicita el alzamiento de la suspensión declarada por el juez durante las cuestiones prejudiciales, cuando se acredita que el juicio criminal ha finalizado", "Alzamiento de la suspensión en caso de prejudicialidad penal", 59 },
                    { 1128, "SERE-61", "SERE", "Resolución judicial por la que se acepta o rechaza a trámite el recurso interpuesto", "Recurso: Admisión/ Inadmisión", 59 },
                    { 1129, "SERE-62", "SERE", "Resolución judicial por la que se acuerda el alzamiento de embargo practicado sobre los bienes por satisfacción de la deuda", "Levantamiento del embargo", 59 },
                    { 1130, "SERE-63", "SERE", "Acto por el cual el letrado de la administración de justicia requiere al deudor porderdante el pago de los honorarios que hubieran devengado durante el procedimiento", "Cuenta Abogado y Procurador: Requerimiento de pago al poderdante", 59 },
                    { 1131, "SERE-64", "SERE", "Resolución judicial por la no se aprueban las alegaciones presentadas por las partes impugnando las costas como indebidas", "Decreto de inadmisión de la impugnación tasación de costas", 59 },
                    { 1132, "SERE-65", "SERE", "Resolución del letrado de la administración de justicia aprobando los honorarios presentados por el abogado", "Cuenta Abogado y Procurador: Decreto de aprobación honorarios abogado", 59 },
                    { 1133, "SERE-66", "SERE", "Documento que aprueba la revocación del administrador concursal", "Administración concursal: Auto declarando la separación del administrador", 59 },
                    { 1134, "SERE-67", "SERE", "Documento que resuelve la permanencia o no  de los ocupantes no ejecutados en el inmueble objeto de la ejecución", "Subasta: Auto por el cual el Juzgado declara que los ocupantes tienen derecho o no a permanecer en el inmueble (art 661 LEC)", 59 },
                    { 1135, "SERE-68", "SERE", "Resolución de juez  en la que decide  de la medida cuatelar (embargo de bienes, intervención o administración, depósito…..),  con el fin de asegurar la efectividad de la decisión judicial o ordenando su levantamiento.", "Medidas cautelares: Auto ordenando o denegando las medidas cautelares", 59 },
                    { 1136, "SERE-69", "SERE", "Resolución judicial por la que se dejan sin efecto las medidas adoptadas por circustancias sobrevenidas", "Medidas cautelares: Decreto ordenando alzamiento de las medidas cautelares", 59 },
                    { 1137, "SERE-70", "SERE", "Documento que aprueba o desaprueba el procedimiento para dar cumplimiento a una resolución judicial que todavia no ha ganado firmeza", "Ejecución: Auto estimando / desestimando la ejecución provisional", 59 },
                    { 1138, "SERE-71", "SERE", "Resolución judicial acordando el traslado de la demanda de tercería al acreedor ejecutante para que se oponga razonadamente", "Tercería de dominio: Traslado al afectado para oposición", 59 },
                    { 1139, "SERE-72", "SERE", "Escrito solicitando al ejecutado relación de bienes de su titularidad con el fin de determinar cuales de ellos son embargables", "Ejecución: Diligencia de ordenación requiriendo al ejecutado para manifestar relación de bienes y derechos", 59 },
                    { 1140, "SERE-73", "SERE", "Resolución del juez decidiendo sobre la cuestión incidental planteada durante el procedimiento, indicando si deber considerarse de previo o especial pronunciamiento", "Cuestiones incidentales: Auto resolviendo sobre la cuestión", 59 },
                    { 1141, "SERE-74", "SERE", "Resolución del juez que acuerda la continuación del proceso de ejecución, que se encontraba suspendido por circuntancias recogidas en la ley", "Ejecución: Auto acordando la continuación de la ejecución", 59 },
                    { 1142, "SERE-75", "SERE", "Documento propio de la ejecución forzosa, impone al sujeto obligado una carga economica con el fin de posibilitar el cumplimiento de lo ordenado (ej imponer una multa por una obra ilegal y ademas ordenar su demolición)", "Ejecución: Decreto imponiendo multas coercitivas", 59 },
                    { 1143, "SERE-76", "SERE", "Trámite dirigido a comprobar hechos no demostrados por las partes en un proceso o aclarar las discrepancias existentes entre ellas.", "Práctica prueba- Admisión-Inadmisión", 59 },
                    { 1144, "SERE-77", "SERE", "Documento que decide sobre la comparecencia (intervención)  de los ocupantes a fin de resolver la toma de posesión de un inmueble", "Subasta: Auto resolviendo la vista de los ocupantes", 59 },
                    { 1145, "SERE-78", "SERE", "Resolución judicial por la que se acuerda suspendar temporalmente la ejecución, bien por disposición legal, bien por acuerdo entre las partes interesadas", "Ejecución: Suspensión ejecución", 59 },
                    { 1146, "SERE-79", "SERE", "Resolución del letrado de la administración de justicia por la que se nombre persona encargada de  de guardar el bien o los términos para la constitución de la administración judicial de los bienes embargados", "Medidas cautelares: Decreto designación depositario o de la administración judicial", 59 },
                    { 1147, "SERE-80", "SERE", "Acto del tribunal designando y comunicando el profesional que va a valorar los bienes objeto de embargo así como su comparencia durante el procedimiento", "Ejecución: Nombramiento de perito tasador, notificación y comparecencia.", 59 },
                    { 1148, "SERE-81", "SERE", "Documento que  acuerda el depósito del bien, en los casos en los que existe incumplimiento en las obligaciones de retención o ingreso, o lo aconsejen las circunstancias  del deudor o la naturaleza del bien o derecho", "Ejecución: Decreto aprobando depósito del bien en poder del acreedor o 3", 59 },
                    { 1149, "SERE-82", "SERE", "Documento emitido por el letrado de la administración de justicia en el que se recoge parte de las actuacioens de un procesos, dando fe que se contenido se corresponde con los originales, en este caso, con el decreto de adjudicación emitido en la subasta", "Subasta: Testimonio de decreto de aprbación del remate", 59 },
                    { 1150, "SERE-83", "SERE", "Documento que decide sobre la toma de posesión de un inmueble", "Subasta: Auto resolviendo lanzamiento", 59 },
                    { 1151, "SERE-84", "SERE", "Acto procesal con comparecencia de las partes con el fin de resolver la posesión de un inmueble", "Subasta: Vista para la ejecución del lanzamiento", 59 },
                    { 1152, "SERE-85", "SERE", "Documento que detiene el lanzamiento de un inmueble en un procedimiento de ejecución, cuando el mismo supone vivienda habitual y se encuentran en sItuación de vulnerabilidad", "Subasta: Suspensión lanzamiento", 59 },
                    { 1153, "SERE-86", "SERE", "Resolución del juez validando el informe pericial", "Ejecución: Providencia/decreto aprobación definitiva informes de valoración", 59 },
                    { 1154, "SERE-87", "SERE", "Resolución judicial validando o denegando la realización (venta) forzosa del bien embargado, bien mediante administración para pago bien mediante enajenación forzosa (a través de subasta, convenio de realización o venta por entidad o persona especializada).", "Ejecución: Resolución judicial decidiendo sobre la realización (venta) del bien", 59 },
                    { 1155, "SERE-88", "SERE", "Resolución del letrado de la administración de justicia acordando la entrega de los bien hipotecado al ejecutante para su administración", "Ejecución: Decreto poniendo al ejecutante en posesión de los bienes", 59 },
                    { 1156, "SERE-89", "SERE", "Resolución del juez o letrado de la administración de justicia aprobando la liquidación los daños y perjuicios  en los casos de ejecuciones no dineraria", "Ejecución: Decretpo/auto aprobación de daños y perjuicios", 59 },
                    { 1157, "SERE-90", "SERE", "Resoluciones judiciales emitidas por el letrado de la administración de justicia que dan a los autos el curso que corresponda, según lo ordenado por la ley, e impulsar formalmente el procedimiento", "Diligencia de ordenación", 59 },
                    { 1158, "SERE-91", "SERE", "Providencias, autos dictadas por los jueces y tribunales así como las diligencias y decretos del letrado de la administración de justicia, dictadas durante el procedimiento judicial que no estén incluidos en el el resto de TDN2 definidos en la clasificación documental.", "Resoluciones judiciales", 59 },
                    { 1159, "SERE-92", "SERE", "Documento que deniega la aprobación de remate por no cumplir los requisitos del articulo 670 LEC (la mejor postura del ejecutante era inferior al credito reclamado)", "Subasta: Decreto denegación remate", 59 },
                    { 1160, "SERE-93", "SERE", "Documento librado al registrador  para la remisión del certificado de dominio y cargas de determinadas fincas registrales", "Subasta: mandamiento Certificación dominio y cargas", 59 },
                    { 1161, "SERE-94", "SERE", "Resolución judicial del incidente previsto en el art. 661 y 675 LEC  donde fija la situación posesoria del inmueble que ha de ser subastado", "Resolución vista ocupantes", 59 },
                    { 1162, "TASA-01", "TASA", "Documento emitido por un perito que viene a demostrar frente a terceros y argumentar el por qué de un valor concreto, especificando en que elementos o bases se apoyan para poder dar un valor frente a otros. Tomando como referente siempre un bien concreto individualizado y poniéndolo en relación con el conjunto, atendiendo de forma inequívoca a las cualidades específicas de ese bien.", "Bien mobiliario: informe pericial", 60 },
                    { 1163, "TASA-02", "TASA", "Valoración de técnico independiente sobre bien mueble", "Bien mobiliario: tasación", 60 },
                    { 1164, "TASA-03", "TASA", "Valoración sobre la calidad de los activos financieros pignorados", "Calificación crediticia de los valores pignorados", 60 },
                    { 1165, "TASA-04", "TASA", "Documento emitido por un perito, en el marco de una ejecución, que viene a demostrar frente a terceros y argumentar el por qué de un valor concreto, especificando en que elementos o bases se apoyan para poder dar un valor frente a otros. Tomando como referente siempre un bien concreto individualizado y poniéndolo en relación con el conjunto, atendiendo de forma inequívoca a las cualidades específicas de ese bien.", "Ejecución: Informe pericial valoración bienes", 60 },
                    { 1166, "TASA-05", "TASA", "Informe de valoración sobre elementos incompatibles en el marco de un proyecto urbanístico", "Indemnización: informe valoración elementos incompatibles", 60 },
                    { 1167, "TASA-06", "TASA", "Documento emitido por un perito que viene a demostrar frente a terceros y argumentar el por qué de un valor concreto, especificando en que elementos o bases se apoyan para poder dar un valor frente a otros. Tomando como referente siempre un bien concreto individualizado y poniéndolo en relación con el conjunto, atendiendo de forma inequívoca a las cualidades específicas de ese bien.", "Informe pericial valoración bienes", 60 },
                    { 1168, "TASA-07", "TASA", "Documento de estimación del valor de un activo por el departamento de patrimonio de Sareb.", "Informe valoración patrimonio", 60 },
                    { 1169, "TASA-08", "TASA", "Documento de estimación del valor de un activo realizada por el técnico competente realizada sobre el activo una vez finalizada la obra (100% de ejecución)", "Tasación 100% obra finalizada", 60 },
                    { 1170, "TASA-09", "TASA", "Documento emitido por un perito que viene a demostrar frente a terceros y argumentar el porqué de un valor concreto, especificando en que elementos o bases se apoyan para poder dar un valor frente a otros. Tomando como referente siempre un bien concreto individualizado y poniéndolo en relación con el conjunto, atendiendo de forma inequívoca a las cualidades específicas de ese bien.", "Tasación: Informe activo", 60 },
                    { 1171, "TASA-10", "TASA", "Documento de estimación del valor de un activo realizada por el técnico competente de forma previa a la venta del activo", "Tasación para la venta", 60 },
                    { 1172, "TASA-11", "TASA", "Documento emitido por un perito que viene a demostrar frente a terceros y argumentar el porqué de un valor concreto, especificando en que elementos o bases se apoyan para poder dar un valor frente a otros. Tomando como referente siempre un bien concreto individualizado y poniéndolo en relación con el conjunto, atendiendo de forma inequívoca a las cualidades específicas de ese bien.", "Tasación: informe activo", 60 },
                    { 1173, "TASA-12", "TASA", "Documento de Valoración realizado por la unidad de valoraciones de SAREB sobre una garantía.", "Valoración colateral UVA", 60 },
                    { 1174, "TASA-13", "TASA", "Documento interno que determina la valoración de un préstamo en un momento determinado.", "Valoración Préstamos", 60 },
                    { 1175, "CERJ-70", "CERJ", "Documento obligatorio para vivienda residencial, en cual se indica si se entrega o no licencia de primera ocupación o cédula de habitabilidad.", "Declaración responsable", 12 },
                    { 1176, "ESIN-AK", "ESIN", "Documento final obligatorio de la certificadora en el que se refleja el resultado del traspaso (APTO / NO APTO) antes del cierre del expediente.", "Informe certificación", 29 },
                    { 1177, "ESIN-AL", "ESIN", "Documento obligatorio en el que se recogen los pormenores del análisis del expediente.", "Informe resumen de certificación", 29 },
                    { 1178, "COMU-91", "COMU", "Documento formal enviado por una de las partes (habitualmente el arrendador o gestor de un inmueble) al inquilino, mediante un servicio que acredita fehacientemente el envío, el contenido y la recepción, con el fin de notificar tanto la extensión temporal de los beneficios o reducciones, como la supresión o no aplicación de las mismas, que el arrendatario venía disfrutando en virtud de un contrato o acuerdo previo", "Alquiler: burofax bonificaciones", 15 },
                    { 1179, "COMU-92", "COMU", "Documento formal enviado por una de las partes a la otra, mediante un servicio que acredita fehacientemente el envío, el contenido y la recepción, con el fin de notificar que se ha cumplido la condición suspensiva prevista en un contrato, provocando así la entrada en vigor o plena eficacia de las obligaciones contractuales.", "Alquiler: burofax condición suspensiva", 15 },
                    { 1180, "COMU-93", "COMU", "Documento formal remitido por el arrendador o la entidad gestora al arrendatario, mediante un servicio que acredita fehacientemente el envío, el contenido y la recepción, con el fin de notificar cuestiones relativas a los servicios de suministros del inmueble (como agua, electricidad, gas u otros).", "Alquiler: burofax suministros", 15 },
                    { 1181, "COMU-94", "COMU", "Documento formal remitido por una de las partes a la otra, mediante un servicio que acredita fehacientemente el envío, el contenido y la recepción, con el fin de advertir de un incumplimiento contractual o conducta irregular y requerir su subsanación en un plazo determinado, bajo la advertencia de posibles consecuencias legales.", "Alquiler: burofax apercibimiento", 15 },
                    { 1182, "COMU-95", "COMU", "Comunicación formal y fehaciente que el propietario envía al inquilino para informarle de su intención de vender la vivienda y ofrecerle la posibilidad de ejercer su derecho de adquisición preferente (tanteo o retracto), tal como establece la Ley de Arrendamientos Urbanos (LAU) en España.", "Alquiler: burofax propuesta de venta", 15 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 171);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 173);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 174);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 175);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 176);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 177);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 178);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 179);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 180);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 181);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 182);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 183);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 184);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 185);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 186);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 187);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 188);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 189);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 190);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 191);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 192);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 193);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 194);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 195);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 196);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 197);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 198);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 199);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 200);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 201);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 202);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 203);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 204);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 205);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 206);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 207);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 208);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 209);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 210);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 211);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 212);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 213);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 214);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 215);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 216);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 217);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 218);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 219);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 220);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 221);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 222);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 223);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 224);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 225);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 226);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 227);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 228);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 229);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 230);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 231);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 232);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 233);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 234);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 235);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 236);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 237);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 238);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 239);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 240);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 241);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 242);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 243);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 244);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 245);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 246);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 247);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 248);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 249);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 250);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 251);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 252);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 253);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 254);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 255);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 256);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 257);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 258);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 259);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 260);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 261);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 262);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 263);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 264);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 265);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 266);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 267);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 268);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 269);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 270);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 271);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 272);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 273);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 274);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 275);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 276);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 277);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 278);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 279);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 280);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 281);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 282);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 283);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 284);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 285);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 286);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 287);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 288);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 289);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 290);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 291);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 292);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 293);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 294);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 295);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 296);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 297);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 298);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 299);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 300);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 301);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 302);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 303);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 304);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 305);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 306);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 307);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 308);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 309);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 310);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 311);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 312);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 313);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 314);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 315);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 316);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 317);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 318);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 319);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 320);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 321);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 322);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 323);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 324);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 325);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 326);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 327);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 328);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 329);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 330);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 331);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 332);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 333);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 334);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 335);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 336);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 337);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 338);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 339);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 340);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 341);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 342);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 343);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 344);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 345);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 346);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 347);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 348);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 349);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 350);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 351);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 352);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 353);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 354);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 355);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 356);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 357);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 358);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 359);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 360);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 361);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 362);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 363);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 364);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 365);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 366);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 367);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 368);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 369);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 370);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 371);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 372);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 373);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 374);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 375);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 376);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 377);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 378);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 379);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 380);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 381);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 382);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 383);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 384);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 385);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 386);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 387);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 388);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 389);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 390);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 391);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 392);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 393);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 394);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 395);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 396);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 397);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 398);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 399);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 400);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 401);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 402);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 403);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 404);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 405);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 406);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 407);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 408);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 409);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 410);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 411);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 412);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 413);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 414);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 415);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 416);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 417);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 418);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 419);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 420);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 421);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 422);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 423);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 424);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 425);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 426);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 427);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 428);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 429);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 430);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 431);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 432);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 433);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 434);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 435);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 436);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 437);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 438);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 439);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 440);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 441);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 442);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 443);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 444);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 445);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 446);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 447);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 448);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 449);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 450);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 451);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 452);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 453);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 454);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 455);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 456);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 457);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 458);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 459);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 460);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 461);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 462);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 463);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 464);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 465);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 466);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 467);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 468);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 469);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 470);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 471);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 472);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 473);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 474);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 475);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 476);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 477);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 478);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 479);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 480);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 481);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 482);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 483);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 484);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 485);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 486);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 487);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 488);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 489);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 490);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 491);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 492);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 493);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 494);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 495);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 496);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 497);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 498);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 499);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 500);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 501);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 502);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 503);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 504);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 505);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 506);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 507);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 508);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 509);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 510);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 511);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 512);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 513);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 514);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 515);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 516);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 517);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 518);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 519);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 520);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 521);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 522);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 523);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 524);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 525);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 526);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 527);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 528);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 529);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 530);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 531);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 532);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 533);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 534);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 535);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 536);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 537);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 538);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 539);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 540);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 541);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 542);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 543);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 544);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 545);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 546);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 547);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 548);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 549);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 550);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 551);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 552);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 553);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 554);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 555);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 556);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 557);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 558);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 559);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 560);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 561);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 562);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 563);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 564);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 565);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 566);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 567);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 568);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 569);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 570);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 571);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 572);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 573);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 574);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 575);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 576);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 577);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 578);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 579);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 580);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 581);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 582);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 583);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 584);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 585);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 586);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 587);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 588);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 589);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 590);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 591);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 592);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 593);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 594);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 595);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 596);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 597);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 598);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 599);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 600);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 601);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 602);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 603);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 604);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 605);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 606);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 607);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 608);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 609);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 610);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 611);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 612);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 613);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 614);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 615);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 616);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 617);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 618);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 619);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 620);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 621);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 622);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 623);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 624);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 625);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 626);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 627);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 628);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 629);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 630);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 631);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 632);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 633);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 634);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 635);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 636);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 637);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 638);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 639);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 640);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 641);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 642);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 643);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 644);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 645);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 646);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 647);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 648);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 649);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 650);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 651);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 652);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 653);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 654);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 655);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 656);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 657);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 658);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 659);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 660);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 661);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 662);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 663);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 664);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 665);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 666);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 667);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 668);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 669);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 670);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 671);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 672);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 673);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 674);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 675);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 676);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 677);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 678);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 679);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 680);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 681);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 682);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 683);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 684);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 685);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 686);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 687);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 688);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 689);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 690);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 691);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 692);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 693);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 694);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 695);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 696);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 697);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 698);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 699);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 700);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 701);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 702);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 703);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 704);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 705);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 706);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 707);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 708);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 709);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 710);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 711);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 712);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 713);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 714);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 715);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 716);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 717);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 718);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 719);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 720);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 721);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 722);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 723);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 724);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 725);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 726);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 727);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 728);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 729);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 730);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 731);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 732);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 733);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 734);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 735);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 736);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 737);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 738);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 739);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 740);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 741);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 742);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 743);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 744);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 745);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 746);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 747);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 748);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 749);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 750);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 751);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 752);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 753);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 754);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 755);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 756);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 757);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 758);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 759);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 760);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 761);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 762);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 763);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 764);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 765);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 766);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 767);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 768);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 769);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 770);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 771);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 772);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 773);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 774);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 775);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 776);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 777);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 778);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 779);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 780);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 781);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 782);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 783);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 784);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 785);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 786);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 787);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 788);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 789);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 790);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 791);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 792);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 793);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 794);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 795);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 796);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 797);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 798);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 799);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 800);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 801);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 802);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 803);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 804);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 805);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 806);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 807);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 808);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 809);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 810);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 811);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 812);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 813);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 814);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 815);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 816);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 817);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 818);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 819);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 820);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 821);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 822);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 823);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 824);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 825);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 826);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 827);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 828);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 829);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 830);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 831);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 832);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 833);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 834);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 835);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 836);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 837);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 838);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 839);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 840);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 841);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 842);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 843);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 844);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 845);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 846);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 847);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 848);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 849);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 850);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 851);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 852);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 853);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 854);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 855);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 856);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 857);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 858);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 859);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 860);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 861);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 862);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 863);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 864);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 865);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 866);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 867);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 868);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 869);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 870);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 871);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 872);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 873);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 874);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 875);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 876);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 877);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 878);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 879);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 880);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 881);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 882);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 883);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 884);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 885);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 886);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 887);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 888);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 889);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 890);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 891);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 892);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 893);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 894);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 895);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 896);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 897);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 898);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 899);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 900);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 901);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 902);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 903);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 904);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 905);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 906);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 907);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 908);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 909);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 910);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 911);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 912);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 913);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 914);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 915);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 916);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 917);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 918);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 919);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 920);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 921);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 922);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 923);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 924);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 925);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 926);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 927);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 928);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 929);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 930);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 931);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 932);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 933);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 934);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 935);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 936);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 937);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 938);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 939);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 940);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 941);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 942);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 943);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 944);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 945);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 946);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 947);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 948);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 949);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 950);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 951);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 952);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 953);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 954);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 955);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 956);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 957);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 958);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 959);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 960);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 961);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 962);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 963);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 964);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 965);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 966);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 967);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 968);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 969);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 970);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 971);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 972);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 973);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 974);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 975);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 976);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 977);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 978);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 979);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 980);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 981);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 982);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 983);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 984);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 985);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 986);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 987);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 988);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 989);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 990);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 991);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 992);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 993);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 994);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 995);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 996);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 997);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 998);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 999);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1000);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1005);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1006);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1007);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1008);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1009);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1010);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1011);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1012);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1013);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1014);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1015);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1016);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1017);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1018);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1019);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1020);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1021);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1022);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1023);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1024);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1025);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1026);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1027);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1028);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1029);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1030);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1031);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1032);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1033);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1034);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1035);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1036);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1037);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1038);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1039);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1040);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1041);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1042);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1043);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1044);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1045);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1046);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1047);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1048);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1049);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1050);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1051);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1052);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1053);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1054);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1055);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1056);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1057);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1058);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1059);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1060);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1061);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1062);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1063);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1064);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1065);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1066);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1067);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1068);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1069);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1070);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1071);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1072);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1073);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1074);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1075);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1076);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1077);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1078);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1079);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1080);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1081);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1082);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1083);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1084);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1085);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1086);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1087);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1088);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1089);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1090);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1091);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1092);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1093);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1094);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1095);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1096);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1097);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1098);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1099);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1100);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1101);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1102);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1103);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1104);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1105);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1106);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1107);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1108);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1109);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1110);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1111);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1112);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1113);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1114);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1115);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1116);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1117);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1118);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1119);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1120);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1121);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1122);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1123);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1124);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1125);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1126);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1127);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1128);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1129);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1130);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1131);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1132);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1133);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1134);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1135);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1136);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1137);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1138);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1139);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1140);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1141);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1142);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1143);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1144);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1145);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1146);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1147);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1148);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1149);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1150);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1151);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1152);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1153);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1154);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1155);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1156);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1157);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1158);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1159);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1160);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1161);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1162);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1163);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1164);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1165);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1166);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1167);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1168);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1169);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1170);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1171);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1172);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1173);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1174);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1175);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1176);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1177);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1178);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1179);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1180);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1181);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn2",
                keyColumn: "Id",
                keyValue: 1182);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "CatalogoTdn1",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 21, 13, 52, 46, 674, DateTimeKind.Utc).AddTicks(9085), new DateTime(2026, 5, 21, 13, 52, 46, 674, DateTimeKind.Utc).AddTicks(9075) });
        }
    }
}
