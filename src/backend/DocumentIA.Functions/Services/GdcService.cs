using System;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services
{
    public class GdcService : IGdcService
    {
        private readonly HttpClient httpClient;
        private readonly GdcSettings settings;
        private readonly ILogger<GdcService> logger;

        public GdcService(IHttpClientFactory httpClientFactory, IOptions<GdcSettings> options, ILogger<GdcService> logger)
        {
            this.httpClient = httpClientFactory.CreateClient("GDC");
            this.settings = options.Value;
            this.logger = logger;

            if (!string.IsNullOrEmpty(this.settings.Endpoint))
            {
                try { this.httpClient.BaseAddress = new Uri(this.settings.Endpoint); } catch { }
            }
            this.httpClient.Timeout = TimeSpan.FromSeconds(this.settings.TimeoutSeconds);
        }

        private static string WrapSoapEnvelope(string body)
        {
            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\">" +
                   "<soap:Body>" + body + "</soap:Body></soap:Envelope>";
        }

        private string BuildIdentityElement()
        {
            return "<ns0:applicationId>" + SecurityElement.Escape(settings.ApplicationId) + "</ns0:applicationId>" +
                   "<ns0:nominalUser>" + SecurityElement.Escape(settings.NominalUser) + "</ns0:nominalUser>" +
                   "<ns0:password>" + SecurityElement.Escape(settings.Password) + "</ns0:password>" +
                   "<ns0:username>" + SecurityElement.Escape(settings.Username) + "</ns0:username>";
        }

        private string BuildCreateRequest(SubirGDCInput input)
        {
            // WSDL operation: create(arg0: Identity, arg1: Entity) -> string (objectId)
            // Namespaces: ns0=auth.model, ns2=data.model, ns3=field.data.model, ns4=fieldvalue.data.model
            // Mandatory fields per SINTWS spec v4.0+:
            //   origen_documento, entidad_origen, proceso_carga, publico, servicer,
            //   tipo_expediente, serie, tipo_documento, expediente, Content
            // Optional: subtipo_documento, nombre_documento, nombre_fichero, checksum, matricula_doc

            var expedienteXml = string.IsNullOrEmpty(settings.ClaseExpediente)
                ? string.Empty
                : BuildExpedienteField(input.IdActivo);

            var origenDocumentoXml = string.IsNullOrEmpty(settings.OrigenDocumento)
                ? string.Empty
                : BuildStringField("origen_documento", settings.OrigenDocumento);

            // nombre_documento: use explicit value if set, otherwise fall back to filename
            var nombreDocumento = !string.IsNullOrWhiteSpace(input.NombreDocumento)
                ? input.NombreDocumento
                : input.NombreArchivo;

            // Optional fields — only emitted when value is non-empty
            var serieXml = string.IsNullOrEmpty(input.Serie)
                ? string.Empty
                : BuildStringField("serie", input.Serie);

            var tipoDocumentoXml = string.IsNullOrEmpty(input.TipoDocumento)
                ? string.Empty
                : BuildStringField("tipo_documento", input.TipoDocumento);

            var subtipoDocumentoXml = string.IsNullOrEmpty(input.SubtipoDocumento)
                ? string.Empty
                : BuildStringField("subtipo_documento", input.SubtipoDocumento);

            var entidadOrigenXml = string.IsNullOrEmpty(settings.EntidadOrigen)
                ? string.Empty
                : BuildStringField("entidad_origen", settings.EntidadOrigen);

            var procesoCargaXml = string.IsNullOrEmpty(settings.ProcesoCarga)
                ? string.Empty
                : BuildStringField("proceso_carga", settings.ProcesoCarga);

            var servicerXml = string.IsNullOrEmpty(settings.Servicer)
                ? string.Empty
                : BuildStringField("servicer", settings.Servicer);

            var tipoExpedienteXml = string.IsNullOrEmpty(settings.TipoExpediente)
                ? string.Empty
                : BuildStringField("tipo_expediente", settings.TipoExpediente);

            return
                "<ns1:create xmlns:ns1=\"http://services.api.sint.sareb.es/\"" +
                " xmlns:ns0=\"http://auth.model.api.sint.sareb.es\"" +
                " xmlns:ns2=\"http://data.model.api.sint.sareb.es\"" +
                " xmlns:ns3=\"http://field.data.model.api.sint.sareb.es\"" +
                " xmlns:ns4=\"http://fieldvalue.data.model.api.sint.sareb.es\"" +
                " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<ns1:arg0 xsi:type=\"ns0:Identity\">" + BuildIdentityElement() + "</ns1:arg0>" +
                "<ns1:arg1 xsi:type=\"ns2:Entity\">" +
                "<ns2:typeId>" + SecurityElement.Escape(settings.DocumentTypeId) + "</ns2:typeId>" +
                "<ns2:fields>" +
                origenDocumentoXml +
                entidadOrigenXml +
                procesoCargaXml +
                BuildStringField("publico", string.IsNullOrEmpty(settings.Publico) ? "verdadero" : settings.Publico) +
                servicerXml +
                tipoExpedienteXml +
                serieXml +
                tipoDocumentoXml +
                subtipoDocumentoXml +
                BuildStringField("nombre_documento", nombreDocumento) +
                BuildStringField("nombre_fichero", input.NombreArchivo) +
                BuildStringField("matricula_doc", input.Matricula) +
                BuildStringField("checksum", input.MD5) +
                expedienteXml +
                BuildFileContentField(settings.ContentFieldName, input.ContenidoBase64) +
                "</ns2:fields>" +
                "</ns1:arg1>" +
                "</ns1:create>";
        }

        private string BuildExpedienteField(string idActivo)
        {
            // Places document in the OTCS activo folder: expediente.id_expediente = idActivo
            return
                "<ns3:Field xsi:type=\"ns3:SingleField\">" +
                "<ns3:name>expediente</ns3:name>" +
                "<ns3:fieldValue xsi:type=\"ns4:EntityFieldValue\">" +
                "<ns4:extraFields>" +
                "<ns3:Field xsi:type=\"ns3:SingleField\">" +
                "<ns3:name>id_expediente</ns3:name>" +
                "<ns3:fieldValue xsi:type=\"ns4:StringFieldValue\">" +
                "<ns4:value>" + SecurityElement.Escape(idActivo) + "</ns4:value>" +
                "</ns3:fieldValue>" +
                "</ns3:Field>" +
                "<ns3:Field xsi:type=\"ns3:SingleField\">" +
                "<ns3:name>clase_expediente</ns3:name>" +
                "<ns3:fieldValue xsi:type=\"ns4:StringFieldValue\">" +
                "<ns4:value>" + SecurityElement.Escape(settings.ClaseExpediente) + "</ns4:value>" +
                "</ns3:fieldValue>" +
                "</ns3:Field>" +
                "</ns4:extraFields>" +
                "</ns3:fieldValue>" +
                "</ns3:Field>";
        }

        private static string BuildStringField(string name, string value)
        {
            return
                "<ns3:Field xsi:type=\"ns3:SingleField\">" +
                "<ns3:name>" + SecurityElement.Escape(name) + "</ns3:name>" +
                "<ns3:fieldValue xsi:type=\"ns4:StringFieldValue\">" +
                "<ns4:value>" + SecurityElement.Escape(value) + "</ns4:value>" +
                "</ns3:fieldValue>" +
                "</ns3:Field>";
        }

        private static string BuildFileContentField(string name, string base64Content)
        {
            // base64 alphabet contains no XML special characters — no escaping needed on dataSource
            return
                "<ns3:Field xsi:type=\"ns3:SingleField\">" +
                "<ns3:name>" + SecurityElement.Escape(name) + "</ns3:name>" +
                "<ns3:fieldValue xsi:type=\"ns4:FileContentFieldValue\">" +
                "<ns4:dataSource>" + base64Content + "</ns4:dataSource>" +
                "</ns3:fieldValue>" +
                "</ns3:Field>";
        }

        private string BuildSearchEntitiesRequest(string idActivo, string md5)
        {
            var safeIdActivo = SecurityElement.Escape(idActivo) ?? string.Empty;
            var safeMd5 = SecurityElement.Escape(md5) ?? string.Empty;

            // DocRepository arg2: if configured, scope to that repository; otherwise omit (server searches all)
            var repoXml = string.IsNullOrEmpty(settings.RepositoryId)
                ? "<ns1:arg2/>"
                : "<ns1:arg2>" +
                  "<ns2:DocRepository xmlns:ns2=\"http://doc.model.api.sint.sareb.es\" xsi:type=\"ns2:DocRepository\">" +
                  "<ns2:id>" + SecurityElement.Escape(settings.RepositoryId) + "</ns2:id>" +
                  "<ns2:name>" + SecurityElement.Escape(settings.RepositoryName) + "</ns2:name>" +
                  "</ns2:DocRepository>" +
                  "</ns1:arg2>";

            return
                "<ns1:searchEntities xmlns:ns1=\"http://services.api.sint.sareb.es/\" xmlns:ns0=\"http://auth.model.api.sint.sareb.es\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<ns1:arg0 xsi:type=\"ns0:Identity\">" + BuildIdentityElement() + "</ns1:arg0>" +
                "<ns1:arg1 xmlns:ns2=\"http://search.model.api.sint.sareb.es\" xmlns:ns3=\"http://data.model.api.sint.sareb.es\" xsi:type=\"ns2:Query\">" +
                "<ns2:entityTypeId>document</ns2:entityTypeId>" +
                "<ns2:filter xsi:type=\"ns2:SetExpression\">" +
                "<ns2:expressions>" +
                // EntityExpression IN: search by expediente.id_expediente (confirmed working in POC)
                "<ns2:Expression xsi:type=\"ns2:EntityExpression\">" +
                "<ns2:condition>IN</ns2:condition>" +
                "<ns2:entityName>expediente</ns2:entityName>" +
                "<ns2:fieldName>id_expediente</ns2:fieldName>" +
                "<ns2:value xsi:type=\"ns2:StringValueList\">" +
                "<ns2:values><ns1:string>" + safeIdActivo + "</ns1:string></ns2:values>" +
                "</ns2:value>" +
                "</ns2:Expression>" +
                "<ns2:Expression xsi:type=\"ns2:FieldExpression\">" +
                "<ns2:condition>EQUALS</ns2:condition>" +
                "<ns2:fieldName>checksum</ns2:fieldName>" +
                "<ns2:value xsi:type=\"ns2:StringValue\">" +
                "<ns2:value>" + safeMd5 + "</ns2:value>" +
                "</ns2:value>" +
                "</ns2:Expression>" +
                "</ns2:expressions>" +
                "<ns2:operator>AND</ns2:operator>" +
                "</ns2:filter>" +
                "<ns2:firstResultIndex>1</ns2:firstResultIndex>" +
                "<ns2:maxResults>1</ns2:maxResults>" +
                "<ns2:orderingField xsi:type=\"ns2:OrderingField\">" +
                "<ns2:ascending>false</ns2:ascending>" +
                "<ns2:fieldName>create_date</ns2:fieldName>" +
                "</ns2:orderingField>" +
                "<ns2:resultsProfile xsi:type=\"ns3:EntityProfile\">" +
                "<ns3:fieldNames>" +
                "<ns1:string>checksum</ns1:string>" +
                "<ns1:string>nombre_fichero</ns1:string>" +
                "</ns3:fieldNames>" +
                "<ns3:ignoreContent>true</ns3:ignoreContent>" +
                "<ns3:ignoreMetadata>false</ns3:ignoreMetadata>" +
                "</ns2:resultsProfile>" +
                "</ns1:arg1>" +
                repoXml +
                "</ns1:searchEntities>";
        }

        private static string? ExtractFirstNonEmptyTagValue(XmlDocument doc, params string[] tagNames)
        {
            foreach (var tag in tagNames)
            {
                var nodes = doc.GetElementsByTagName(tag);
                if (nodes?.Count > 0)
                {
                    var value = nodes[0]?.InnerText;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        public async Task<(bool Exists, string? ObjectId)> ConsultarDocumentoAsync(string idActivo, string md5, string matricula, CancellationToken cancellationToken = default)
        {
            var body = BuildSearchEntitiesRequest(idActivo, md5);

            var envelope = WrapSoapEnvelope(body);
            var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");

            using var resp = await httpClient.PostAsync(this.settings.Endpoint ?? string.Empty, content, cancellationToken);
            var xml = await resp.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                // Check for SOAP Fault before parsing results
                var fault = doc.GetElementsByTagName("Fault", "http://www.w3.org/2003/05/soap-envelope");
                if (fault.Count == 0) fault = doc.GetElementsByTagName("Fault");
                if (fault.Count > 0) return (false, null);

                // totalItemsResult > 0 means document exists; Entity/id is the GDC object id
                var totalItems = doc.GetElementsByTagName("totalItemsResult");
                if (totalItems.Count > 0 && int.TryParse(totalItems[0]?.InnerText, out var total) && total > 0)
                {
                    var entityIdNodes = doc.GetElementsByTagName("id");
                    var objectId = entityIdNodes.Count > 0 ? entityIdNodes[0]?.InnerText : null;
                    return (true, string.IsNullOrWhiteSpace(objectId) ? null : objectId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed parsing searchEntities response. XML: {XmlResponse}", xml);
            }

            return (false, null);
        }

        public async Task<ResultadoGDC> SubirDocumentoAsync(SubirGDCInput input, CancellationToken cancellationToken = default)
        {
            var result = new ResultadoGDC();
            var body = BuildCreateRequest(input);
            var envelope = WrapSoapEnvelope(body);
            var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var resp = await httpClient.PostAsync(this.settings.Endpoint ?? string.Empty, content, cancellationToken);
                var xml = await resp.Content.ReadAsStringAsync(cancellationToken);
                sw.Stop();
                result.DuracionMs = (int)sw.ElapsedMilliseconds;

                var doc = new XmlDocument();
                doc.LoadXml(xml);

                // SOAP Fault — DOC_OBJECT_EXISTS means document already exists in GDC
                // SOAP 1.2 uses http://www.w3.org/2003/05/soap-envelope; fall back to 1.1 and unqualified
                var fault = doc.GetElementsByTagName("Fault", "http://www.w3.org/2003/05/soap-envelope");
                if (fault.Count == 0)
                    fault = doc.GetElementsByTagName("Fault", "http://schemas.xmlsoap.org/soap/envelope/");
                if (fault.Count == 0)
                    fault = doc.GetElementsByTagName("Fault");
                if (fault.Count > 0)
                {
                    var errorCode = doc.GetElementsByTagName("errorCode");
                    if (errorCode.Count > 0 && errorCode[0]?.InnerText == "DOC_OBJECT_EXISTS")
                    {
                        result.Exitoso = true;
                        result.YaExistia = true;
                        result.Mensaje = "AlreadyExists";
                        return result;
                    }
                    result.Exitoso = false;
                    result.ErrorDetalle = xml;
                    // SOAP 1.2: <Reason><Text>, SOAP 1.1: <faultstring>
                    var reasonText = doc.GetElementsByTagName("Text", "http://www.w3.org/2003/05/soap-envelope");
                    if (reasonText.Count == 0) reasonText = doc.GetElementsByTagName("faultstring");
                    result.Mensaje = reasonText.Count > 0 ? (reasonText[0]?.InnerText ?? "SoapFault") : "SoapFault";
                    return result;
                }

                // createResponse.return = objectId string (try namespace-qualified first, then unqualified)
                var returnNode = doc.GetElementsByTagName("return", "http://services.api.sint.sareb.es/");
                if (returnNode.Count == 0)
                    returnNode = doc.GetElementsByTagName("return");
                if (returnNode.Count > 0)
                {
                    var objectId = returnNode[0]?.InnerText;
                    if (!string.IsNullOrWhiteSpace(objectId))
                    {
                        result.Exitoso = true;
                        result.ObjectId = objectId!;
                        result.Mensaje = "OK";
                        result.Intentos = 1;
                        return result;
                    }
                }

                result.Exitoso = false;
                result.ErrorDetalle = xml;
                result.Mensaje = resp.IsSuccessStatusCode ? "UnknownResponse" : $"HTTP {resp.StatusCode}";
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.DuracionMs = (int)sw.ElapsedMilliseconds;
                result.Exitoso = false;
                result.ErrorDetalle = ex.ToString();
                result.Mensaje = "Exception";
                logger.LogError(ex, "Error calling GDC create");
            }

            return result;
        }
    }
}
