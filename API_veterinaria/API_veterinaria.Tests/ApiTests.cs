using System.Net;
using System.Net.Http.Json;
using API_veterinaria.Data;
using API_veterinaria.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace API_veterinaria.Tests;

public sealed class ApiTests
{
    [Theory]
    [InlineData("/api/no-existe", "GET", 404)]
    [InlineData("/api/clientes", "DELETE", 405)]
    public async Task ErroresHttpDevuelvenProblemDetails(string path, string method, int expected)
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
        if (expected == 405) Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task TimeoutDevuelve504SinDetallesInternos()
    {
        using var factory = new ApiFactory();
        factory.Repository.Error = new TimeoutException("SECRETO");
        var response = await factory.CreateClient().GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("SECRETO", body);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task JsonMalFormadoDevuelve400()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsync("/api/clientes",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.Registros);
    }
    [Theory]
    [InlineData("", "Nombre", "correo@example.com")]
    [InlineData("123", "   ", "correo@example.com")]
    [InlineData("123", "Nombre", "invalido")]
    public async Task ClienteInvalidoDevuelve400(string cedula, string nombre, string correo)
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/clientes", new { cedula, nombre, correo });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.Registros);
    }

    [Fact]
    public async Task ClienteConCedulaLargaDevuelve400()
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/clientes",
            new { Cedula = new string('1', 51), Nombre = "Cliente", Correo = "correo@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1000000, 1)]
    [InlineData(1.234, 1)]
    [InlineData(1, 0)]
    public async Task MascotaInvalidaDevuelve400(double peso, long idCliente)
    {
        using var factory = new ApiFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/mascotas",
            new { Nombre = "Luna", Especie = "Perro", Raza = "Mixta", Peso = peso, IdCliente = idCliente });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.Registros);
    }

    [Fact]
    public async Task RegistrosValidosDevuelven201YConsultas200()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/clientes",
            new { Cedula = "123", Nombre = "Cliente", Correo = "correo@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/mascotas",
            new { Nombre = "Luna", Especie = "Perro", Raza = "Mixta", Peso = 1.25m, IdCliente = 1 })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<Cliente>>("/api/clientes"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<Mascota>>("/api/mascotas"))!);
        Assert.Equal(2, factory.Repository.Registros);
    }

    [Fact]
    public async Task ErrorInternoNoExponeDetalles()
    {
        using var factory = new ApiFactory();
        factory.Repository.Fallar = true;
        var response = await factory.CreateClient().GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("SECRETO", await response.Content.ReadAsStringAsync());
    }
}

internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeRepository Repository { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVeterinariaRepository>();
            services.AddSingleton<IVeterinariaRepository>(Repository);
        });
    }
}

internal sealed class FakeRepository : IVeterinariaRepository
{
    public int Registros { get; private set; }
    public bool Fallar { get; set; }
    public Exception? Error { get; set; }
    public Task<IEnumerable<Cliente>> ConsultarClientesAsync(CancellationToken token) =>
        Error is not null ? throw Error : Fallar ? throw new InvalidOperationException("SECRETO") : Task.FromResult<IEnumerable<Cliente>>([]);
    public Task<IEnumerable<Mascota>> ConsultarMascotasAsync(CancellationToken token) => Task.FromResult<IEnumerable<Mascota>>([]);
    public Task<long> RegistrarClienteAsync(RegistrarCliente model, CancellationToken token) { Registros++; return Task.FromResult(1L); }
    public Task<long> RegistrarMascotaAsync(RegistrarMascota model, CancellationToken token) { Registros++; return Task.FromResult(1L); }
}
