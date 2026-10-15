using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace DocumentIA.Tests.Unit.Helpers;

/// <summary>
/// Factory for creating in-memory HTTP request/response fakes for Azure Functions isolated-worker tests.
/// </summary>
public static class HttpFunctionTestFactory
{
    public static HttpRequestData CreateRequest(
        string method = "GET",
        string url = "http://localhost/api/test",
        string? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        bool conSerializador = false)
    {
        var mockContext = new Mock<FunctionContext>();
        if (conSerializador)
        {
            // InstanceServices con serializador: permite que el codigo bajo prueba use WriteAsJsonAsync.
            var services = new ServiceCollection();
            services.AddOptions();
            services.Configure<WorkerOptions>(opts => opts.Serializer = new Azure.Core.Serialization.JsonObjectSerializer());
            mockContext.Setup(c => c.InstanceServices).Returns(services.BuildServiceProvider());
        }

        var context = mockContext.Object;
        return new FakeHttpRequestData(context, method, new Uri(url), body, headers);
    }

    public static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class FakeHttpRequestData : HttpRequestData
    {
        private readonly string _method;
        private readonly Uri _url;
        private readonly Stream _body;
        private readonly HttpHeadersCollection _headers;

        public FakeHttpRequestData(
            FunctionContext context,
            string method,
            Uri url,
            string? body,
            IReadOnlyDictionary<string, string>? headers) : base(context)
        {
            _method = method;
            _url = url;
            _body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? string.Empty));
            _headers = new HttpHeadersCollection();

            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    _headers.Add(header.Key, header.Value);
                }
            }
        }

        public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);

        public override Stream Body => _body;
        public override HttpHeadersCollection Headers => _headers;
        public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
        public override Uri Url => _url;
        public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
        public override string Method => _method;
    }

    private sealed class FakeHttpResponseData : HttpResponseData
    {
        public FakeHttpResponseData(FunctionContext context) : base(context)
        {
            Body = new SoloEscrituraAsincronaStream();
            Headers = new HttpHeadersCollection();
        }

        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; }
        public override Stream Body { get; set; }
        public override HttpCookies Cookies { get; } = null!;
    }

    /// <summary>
    /// Cuerpo de respuesta que rechaza las escrituras sincronas, como hace Kestrel con la
    /// integracion ASP.NET Core del worker (AllowSynchronousIO=false): un trigger que use
    /// Body.Write o WriteString pasa en memoria pero devuelve 500 en Azure (AB#100968).
    /// </summary>
    private sealed class SoloEscrituraAsincronaStream : Stream
    {
        private const string Mensaje = "Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.";
        private readonly MemoryStream _inner = new();

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => throw new InvalidOperationException(Mensaje);
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException(Mensaje);
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException(Mensaje);
        public override void WriteByte(byte value) => throw new InvalidOperationException(Mensaje);

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(buffer, cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
