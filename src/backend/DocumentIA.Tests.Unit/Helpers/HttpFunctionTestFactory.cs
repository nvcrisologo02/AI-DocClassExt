using Moq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Claims;

namespace DocumentIA.Tests.Unit.Helpers;

/// <summary>
/// Factory for creating in-memory HTTP request/response fakes for Azure Functions isolated-worker tests.
/// </summary>
public static class HttpFunctionTestFactory
{
    public static HttpRequestData CreateRequest(string method = "GET", string url = "http://localhost/api/test")
    {
        var context = new Mock<FunctionContext>().Object;
        return new FakeHttpRequestData(context, method, new Uri(url));
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

        public FakeHttpRequestData(FunctionContext context, string method, Uri url) : base(context)
        {
            _method = method;
            _url = url;
        }

        public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);

        public override Stream Body => Stream.Null;
        public override HttpHeadersCollection Headers => new HttpHeadersCollection();
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
