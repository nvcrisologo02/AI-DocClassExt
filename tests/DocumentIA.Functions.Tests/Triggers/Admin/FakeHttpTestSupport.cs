using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DocumentIA.Functions.Tests.Triggers.Admin;

/// <summary>
/// Fake concrete implementation of <see cref="HttpResponseData"/> used by admin function tests.
/// Moq cannot mock <c>WriteAsJsonAsync</c> because it is an extension method (<see cref="HttpResponseDataExtensions"/>),
/// so a real subclass is required to exercise the real extension logic (which resolves the
/// <see cref="Azure.Core.Serialization.ObjectSerializer"/> from <see cref="WorkerOptions"/> via <see cref="FunctionContext.InstanceServices"/>).
/// </summary>
internal sealed class FakeHttpResponseData : HttpResponseData
{
    public FakeHttpResponseData(FunctionContext functionContext)
        : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Cookies = new FakeHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; }

    public override Stream Body { get; set; }

    public override HttpCookies Cookies { get; }
}

/// <summary>
/// Trivial <see cref="HttpCookies"/> implementation; no test in this suite exercises cookies.
/// </summary>
internal sealed class FakeHttpCookies : HttpCookies
{
    public override void Append(string name, string value)
    {
    }

    public override void Append(IHttpCookie cookie)
    {
    }

    public override IHttpCookie CreateNew() => new HttpCookie(string.Empty, string.Empty);
}

/// <summary>
/// Fake concrete implementation of <see cref="HttpRequestData"/> used by admin function tests.
/// </summary>
internal sealed class FakeHttpRequestData : HttpRequestData
{
    public FakeHttpRequestData(FunctionContext functionContext, string method, string? body = null)
        : base(functionContext)
    {
        Method = method;
        Url = new Uri("http://localhost/api/test");
        Headers = new HttpHeadersCollection();
        Identities = Array.Empty<ClaimsIdentity>();
        Cookies = Array.Empty<IHttpCookie>();
        Body = body != null
            ? new MemoryStream(Encoding.UTF8.GetBytes(body))
            : new MemoryStream();
    }

    public override Stream Body { get; }

    public override HttpHeadersCollection Headers { get; }

    public override IReadOnlyCollection<IHttpCookie> Cookies { get; }

    public override Uri Url { get; }

    public override IEnumerable<ClaimsIdentity> Identities { get; }

    public override string Method { get; }

    public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);
}
