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

        private string BuildSearchEntitiesRequest(string idActivo, string md5)
        {
            var safeIdActivo = SecurityElement.Escape(idActivo) ?? string.Empty;
            var safeMd5 = SecurityElement.Escape(md5) ?? string.Empty;

            return
                "<ns1:searchEntities xmlns:ns1=\"http://services.api.sint.sareb.es/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<ns1:arg0 xsi:nil=\"true\" />" +
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
                "<ns3:fieldNames><ns1:string>id_activo</ns1:string></ns3:fieldNames>" +
                "<ns3:fieldNames><ns1:string>md5</ns1:string></ns3:fieldNames>" +
                "<ns3:fieldNames><ns1:string>object_id</ns1:string></ns3:fieldNames>" +
                "<ns3:ignoreContent>true</ns3:ignoreContent>" +
                "<ns3:ignoreMetadata>false</ns3:ignoreMetadata>" +
                "</ns2:resultsProfile>" +
                "</ns1:arg1>" +
                "<ns1:arg2><ns1:string>AAAA</ns1:string></ns1:arg2>" +
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
            var body = $"<SubirDocumentoRequest xmlns=\"http://sintws.example.org/\">" +
                       $"<IdActivo>{SecurityElement.Escape(input.IdActivo)}</IdActivo>" +
                       $"<Matricula>{SecurityElement.Escape(input.Matricula)}</Matricula>" +
                       $"<NombreArchivo>{SecurityElement.Escape(input.NombreArchivo)}</NombreArchivo>" +
                       $"<ContenidoBase64>{SecurityElement.Escape(input.ContenidoBase64)}</ContenidoBase64>" +
                       $"<SHA256>{SecurityElement.Escape(input.SHA256)}</SHA256>" +
                       $"<CorrelationId>{SecurityElement.Escape(input.CorrelationId)}</CorrelationId>" +
                       "</SubirDocumentoRequest>";

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
                var nodes = doc.GetElementsByTagName("ObjectId");
                if (nodes.Count > 0)
                {
                    var objectId = nodes[0]?.InnerText;
                    if (!string.IsNullOrWhiteSpace(objectId))
                    {
                        result.Exitoso = true;
                        result.ObjectId = objectId!;
                        result.Mensaje = "OK";
                        result.Intentos = 1;
                        return result;
                    }
                }

                var exists = doc.GetElementsByTagName("AlreadyExists");
                if (exists != null && exists.Count > 0)
                {
                    result.Exitoso = true;
                    result.YaExistia = true;
                    result.Mensaje = "AlreadyExists";
                    return result;
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
                logger.LogError(ex, "Error calling SubirDocumento");
            }

            return result;
        }
    }
}
