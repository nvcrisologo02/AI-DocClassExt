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
                   "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                   "<soap:Body>" + body + "</soap:Body></soap:Envelope>";
        }

        private string BuildIdentityElement()
        {
            return "<ns0:applicationId>" + SecurityElement.Escape(settings.ApplicationId) + "</ns0:applicationId>" +
                   "<ns0:nominalUser>" + SecurityElement.Escape(settings.NominalUser) + "</ns0:nominalUser>" +
                   "<ns0:username>" + SecurityElement.Escape(settings.Username) + "</ns0:username>";
        }

        private string BuildCreateRequest(SubirGDCInput input)
        {
            // WSDL operation: create(arg0: Identity, arg1: Entity) -> string (objectId)
            // Namespaces: ns0=auth.model, ns2=data.model, ns3=field.data.model, ns4=fieldvalue.data.model
            // Mandatory fields per SINTWS spec: origen_documento (v4.0+), expediente (folder placement)
            // Binary content field name is configurable (ContentFieldName)
            var expedienteXml = string.IsNullOrEmpty(settings.ClaseExpediente)
                ? string.Empty
                : BuildExpedienteField(input.IdActivo);
            var origenDocumentoXml = string.IsNullOrEmpty(settings.OrigenDocumento)
                ? string.Empty
                : BuildStringField("origen_documento", settings.OrigenDocumento);

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
                BuildStringField("id_activo", input.IdActivo) +
                BuildStringField("matricula", input.Matricula) +
                BuildStringField("nombre_fichero", input.NombreArchivo) +
                BuildStringField("md5", input.MD5) +
                BuildStringField("sha256", input.SHA256) +
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
                "<ns2:Expression xsi:type=\"ns2:FieldExpression\">" +
                "<ns2:condition>EQUAL</ns2:condition>" +
                "<ns2:fieldName>id_activo</ns2:fieldName>" +
                "<ns2:value xsi:type=\"ns2:StringValue\">" + safeIdActivo + "</ns2:value>" +
                "</ns2:Expression>" +
                "<ns2:Expression xsi:type=\"ns2:FieldExpression\">" +
                "<ns2:condition>EQUAL</ns2:condition>" +
                "<ns2:fieldName>md5</ns2:fieldName>" +
                "<ns2:value xsi:type=\"ns2:StringValue\">" + safeMd5 + "</ns2:value>" +
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
                "<ns1:string>id_activo</ns1:string>" +
                "<ns1:string>md5</ns1:string>" +
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
            var content = new StringContent(envelope, Encoding.UTF8, "text/xml");

            using var resp = await httpClient.PostAsync(this.settings.Endpoint ?? string.Empty, content, cancellationToken);
            var xml = await resp.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var objectId = ExtractFirstNonEmptyTagValue(doc, "ObjectId", "objectId", "object_id", "entityId", "id");
                if (!string.IsNullOrWhiteSpace(objectId))
                {
                    return (true, objectId);
                }

                var entities = doc.GetElementsByTagName("entities");
                if (entities.Count > 0)
                {
                    return (true, null);
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
            var content = new StringContent(envelope, Encoding.UTF8, "text/xml");

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
                // Try namespace-qualified <soap:Fault> (SOAP 1.1) then unqualified fallback
                var fault = doc.GetElementsByTagName("Fault", "http://schemas.xmlsoap.org/soap/envelope/");
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
                    var faultstring = doc.GetElementsByTagName("faultstring");
                    result.Mensaje = faultstring.Count > 0 ? (faultstring[0]?.InnerText ?? "SoapFault") : "SoapFault";
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
