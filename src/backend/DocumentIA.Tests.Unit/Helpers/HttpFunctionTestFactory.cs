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
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var context = new Mock<FunctionContext>().Object;
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
            Body = new MemoryStream();
            Headers = new HttpHeadersCollection();
        }

        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; }
        public override Stream Body { get; set; }
        public override HttpCookies Cookies { get; } = null!;
    }
}
