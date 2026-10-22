using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Web_veterinaria.Middleware;
using Xunit;

namespace API_veterinaria.Tests;

public sealed class MiddlewareTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(408)]
    [InlineData(413)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task PaginaDeErrorConservaEstadoYEscapaReferencia(int status)
    {
        var context = Context();
        context.TraceIdentifier = "<script>secreto</script>";
        context.Response.Headers.Allow = "GET";
        await ErrorHandlingMiddleware.WriteErrorAsync(context, status);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("GET", context.Response.Headers.Allow);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var body = await Body(context);
        Assert.Contains("Volver al inicio", body);
        Assert.DoesNotContain("<script>", body);
        Assert.Contains("&lt;script&gt;", body);
    }

    [Fact]
    public async Task CancelacionDelUsuarioNoSeConvierteEnError500()
    {
        var context = Context();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        var middleware = Middleware(_ => throw new OperationCanceledException(cancellation.Token));
        await middleware.InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task TimeoutSeDistingueDeErrorInterno()
    {
        var context = Context();
        await Middleware(_ => throw new TimeoutException("SECRETO")).InvokeAsync(context);
        Assert.Equal(504, context.Response.StatusCode);
        Assert.DoesNotContain("SECRETO", await Body(context));
    }

    [Fact]
    public async Task NoReemplazaUnaRespuestaQueYaComenzo()
    {
        var context = Context();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());
        var exception = new InvalidOperationException("SECRETO");
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Middleware(_ => throw exception).InvokeAsync(context));
        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task HeadNoEscribeCuerpo()
    {
        var context = Context();
        context.Request.Method = "HEAD";
        await ErrorHandlingMiddleware.WriteErrorAsync(context, 404);
        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static ErrorHandlingMiddleware Middleware(RequestDelegate next) => new(next, NullLogger<ErrorHandlingMiddleware>.Instance);
    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
    private static async Task<string> Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
