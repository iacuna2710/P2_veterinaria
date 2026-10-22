using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace API_veterinaria.Tests;

public sealed class WebTests
{
    [Theory]
    [InlineData("/pagina-inexistente", "GET", 404, "No encontramos esta página")]
    [InlineData("/Cliente/RegistrarClientes", "DELETE", 405, "Esta operación no está permitida")]
    public async Task EstadosHttpMuestranPaginaSegura(string path, string method, int expected, string message)
    {
        using var factory = new WebFactory();
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RespuestaApiInvalidaNoSeMuestraComoListaVacia()
    {
        using var factory = new WebFactory();
        factory.Handler.InvalidJson = true;
        var response = await factory.CreateClient().GetAsync("/Mascota/ConsultarMascotas");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("No se pudo contactar", html);
        Assert.DoesNotContain("No hay mascotas registradas", html);
        Assert.DoesNotContain("JsonException", html);
    }
    [Theory]
    [InlineData("/", "Bienvenido a la veterinaria")]
    [InlineData("/Cliente/RegistrarClientes", "Registro de Clientes")]
    [InlineData("/Mascota/RegistrarMascotas", "Selecciona un cliente")]
    [InlineData("/Mascota/ConsultarMascotas", "No hay mascotas registradas.")]
    public async Task PaginasResuelvenSusVistas(string path, string expected)
    {
        using var factory = new WebFactory();
        var response = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, html);
        Assert.Contains("Bienvenido al sistema de la veterinaria.", html);
    }

    [Fact]
    public async Task ConflictoConservaDatosDelCliente()
    {
        using var factory = new WebFactory();
        factory.Handler.Conflict = true;
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/Cliente/RegistrarClientes");
        var response = await client.PostAsync("/Cliente/RegistrarClientes", Form(page,
            ("Cedula", "123"), ("Nombre", "Cliente conservado"), ("Correo", "correo@example.com")));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cliente conservado", html);
        Assert.Contains("Registro duplicado", html);
    }

    [Fact]
    public async Task LimiteDeEspecieSeMuestraEnPantallaYConservaFormulario()
    {
        using var factory = new WebFactory();
        factory.Handler.Conflict = true;
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/Mascota/RegistrarMascotas");
        var response = await client.PostAsync("/Mascota/RegistrarMascotas", Form(page,
            ("Nombre", "Luna"), ("Especie", "Perro"), ("Raza", "Mixta"), ("Peso", "2.50"), ("IdCliente", "1")));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria.", html);
        Assert.Contains("validation-summary-errors", html);
        Assert.Contains("Luna", html);
        Assert.Contains("Perro", html);
        Assert.Contains("Cliente de prueba", html);
    }

    [Fact]
    public async Task MascotaInvalidaConservaFormularioYSelector()
    {
        using var factory = new WebFactory();
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/Mascota/RegistrarMascotas");
        var response = await client.PostAsync("/Mascota/RegistrarMascotas", Form(page,
            ("Nombre", "Luna"), ("Especie", "Perro"), ("Raza", "Mixta"), ("Peso", "0"), ("IdCliente", "1")));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Luna", html);
        Assert.Contains("Cliente de prueba", html);
        Assert.Contains("0.01", html);
        Assert.Equal(0, factory.Handler.Posts);
    }

    [Fact]
    public async Task RegistroExitosoRedirigeYNoReenviaFormulario()
    {
        using var factory = new WebFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Cliente/RegistrarClientes");
        var response = await client.PostAsync("/Cliente/RegistrarClientes", Form(page,
            ("Cedula", "123"), ("Nombre", "Cliente"), ("Correo", "correo@example.com")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, factory.Handler.Posts);
        var redirected = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Cliente registrado correctamente", redirected);
    }

    [Fact]
    public async Task AntifalsificacionRechazaPostSinToken()
    {
        using var factory = new WebFactory();
        var response = await factory.CreateClient().PostAsync("/Cliente/RegistrarClientes", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApiCaidaMuestraErrorYConservaDatos()
    {
        using var factory = new WebFactory();
        factory.Handler.Offline = true;
        var client = factory.CreateClient();
        var page = await client.GetStringAsync("/Cliente/RegistrarClientes");
        var response = await client.PostAsync("/Cliente/RegistrarClientes", Form(page,
            ("Cedula", "123"), ("Nombre", "Conservar"), ("Correo", "correo@example.com")));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Conservar", html);
        Assert.Contains("No se pudo contactar", html);
        Assert.DoesNotContain("No hay mascotas registradas", await client.GetStringAsync("/Mascota/ConsultarMascotas"));
    }

    [Fact]
    public async Task MiddlewareControlaErroresInesperadosSinExponerDetalles()
    {
        using var factory = new WebFactory();
        factory.Handler.UnexpectedFailure = true;
        var response = await factory.CreateClient().GetAsync("/Mascota/RegistrarMascotas");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("No se pudo completar la solicitud", html);
        Assert.Contains("referencia", html);
        Assert.Contains("Volver al inicio", html);
        Assert.DoesNotContain("SECRETO", html);
        Assert.DoesNotContain("InvalidOperationException", html);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    internal static FormUrlEncodedContent Form(string page, params (string Key, string Value)[] values)
    {
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return new FormUrlEncodedContent(values.Select(x => new KeyValuePair<string, string>(x.Key, x.Value))
            .Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(token))));
    }
}

internal sealed class WebFactory : WebApplicationFactory<Web_veterinaria.Program>
{
    public ApiHandler Handler { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services => services.AddHttpClient<Web_veterinaria.Services.VeterinariaApi>()
            .ConfigurePrimaryHttpMessageHandler(() => Handler));
    }
}

internal sealed class ApiHandler : HttpMessageHandler
{
    public bool Conflict { get; set; }
    public bool Offline { get; set; }
    public bool UnexpectedFailure { get; set; }
    public bool InvalidJson { get; set; }
    public int Posts { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (UnexpectedFailure) throw new InvalidOperationException("SECRETO: detalle interno");
        if (Offline) throw new HttpRequestException("API no disponible");
        if (InvalidJson) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[", System.Text.Encoding.UTF8, "application/json") });
        if (request.Method == HttpMethod.Post)
        {
            Posts++;
            return Task.FromResult(new HttpResponseMessage(Conflict ? HttpStatusCode.Conflict : HttpStatusCode.Created)
                { Content = JsonContent.Create(new { title = request.RequestUri!.AbsolutePath.EndsWith("mascotas")
                    ? "No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria."
                    : "Registro duplicado" }) });
        }
        var json = request.RequestUri!.AbsolutePath.EndsWith("clientes")
            ? "[{\"idCliente\":1,\"cedula\":\"123\",\"nombre\":\"Cliente de prueba\",\"correo\":\"correo@example.com\",\"estado\":true}]"
            : "[]";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}
