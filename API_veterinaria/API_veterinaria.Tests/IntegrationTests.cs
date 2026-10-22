using System.Net;
using System.Net.Http.Json;
using System.Transactions;
using API_veterinaria.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace API_veterinaria.Tests;

[Collection("SQL")]
public sealed class IntegrationTests
{
    private static WebApplicationFactory<Program> CreateApi() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Practica2", Environment.GetEnvironmentVariable("PRACTICA2_TEST_CONNECTION")));

    [SqlFact]
    public async Task WebApiYSqlRegistranYConsultanSinDejarDatos()
    {
        using var api = CreateApi();
        api.Server.PreserveExecutionContext = true;
        using var apiClient = api.CreateClient();
        using var web = new WebApplicationFactory<Web_veterinaria.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services => services.AddHttpClient<Web_veterinaria.Services.VeterinariaApi>()
                .ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler()));
        });
        web.Server.PreserveExecutionContext = true;
        using var client = web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var cedula = "web_" + Guid.NewGuid().ToString("N");
        var especie = "especie_" + Guid.NewGuid().ToString("N");
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            var page = await client.GetStringAsync("/Cliente/RegistrarClientes");
            var response = await client.PostAsync("/Cliente/RegistrarClientes", WebTests.Form(page,
                ("Cedula", cedula), ("Nombre", "Cliente integrado"), ("Correo", "integracion@example.com")));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var clientes = await apiClient.GetFromJsonAsync<List<Cliente>>("/api/clientes");
            var cliente = Assert.Single(clientes!, c => c.Cedula == cedula);
            page = await client.GetStringAsync("/Cliente/RegistrarClientes");
            response = await client.PostAsync("/Cliente/RegistrarClientes", WebTests.Form(page,
                ("Cedula", " " + cedula + " "), ("Nombre", "Cliente duplicado rechazado"), ("Correo", "integracion@example.com")));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var alertaCliente = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("No se puede registrar el cliente: ya existe un cliente con esa cédula.", alertaCliente);
            Assert.Contains("validation-summary-errors", alertaCliente);
            Assert.Contains("Cliente duplicado rechazado", alertaCliente);
            Assert.Contains(cedula, alertaCliente);
            Assert.Single((await apiClient.GetFromJsonAsync<List<Cliente>>("/api/clientes"))!, c => c.Cedula == cedula);
            page = await client.GetStringAsync("/Mascota/RegistrarMascotas");
            Assert.Contains("Cliente integrado", page);
            response = await client.PostAsync("/Mascota/RegistrarMascotas", WebTests.Form(page,
                ("Nombre", "Luna integrada"), ("Especie", especie), ("Raza", "Mixta"), ("Peso", "2.50"),
                ("IdCliente", cliente.IdCliente.ToString())));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var consulta = await client.GetStringAsync("/Mascota/ConsultarMascotas");
            Assert.Contains("Luna integrada", consulta);
            Assert.Contains(cedula, consulta);
            Assert.Contains("2.50", consulta);
            Assert.Equal(HttpStatusCode.Created, (await apiClient.PostAsJsonAsync("/api/mascotas",
                new { Nombre = "Segunda", Especie = especie, Raza = "Mixta", Peso = 1.25m, cliente.IdCliente })).StatusCode);
            page = await client.GetStringAsync("/Mascota/RegistrarMascotas");
            response = await client.PostAsync("/Mascota/RegistrarMascotas", WebTests.Form(page,
                ("Nombre", "Tercera rechazada"), ("Especie", especie), ("Raza", "Mixta"), ("Peso", "3.50"),
                ("IdCliente", cliente.IdCliente.ToString())));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var alerta = await response.Content.ReadAsStringAsync();
            Assert.Contains("No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria.", alerta);
            Assert.Contains("validation-summary-errors", alerta);
            Assert.Contains("Tercera rechazada", alerta);
            Assert.Contains("Cliente integrado", alerta);
            var mascotas = await apiClient.GetFromJsonAsync<List<Mascota>>("/api/mascotas");
            Assert.Equal(2, mascotas!.Count(m => m.Especie == especie));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Mascota/ConsultarMascotas")).StatusCode);
        }
        Assert.DoesNotContain((await apiClient.GetFromJsonAsync<List<Cliente>>("/api/clientes"))!, c => c.Cedula == cedula);
        Assert.DoesNotContain((await apiClient.GetFromJsonAsync<List<Mascota>>("/api/mascotas"))!, m => m.Especie == especie);
    }

    [SqlFact]
    public async Task ApiRealDevuelve400ParaClienteInexistente()
    {
        using var api = CreateApi();
        var response = await api.CreateClient().PostAsJsonAsync("/api/mascotas",
            new { Nombre = "Luna", Especie = "Prueba", Raza = "Mixta", Peso = 1, IdCliente = long.MaxValue });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("seleccionado no existe", await response.Content.ReadAsStringAsync());
    }

    [SqlFact]
    public async Task ApiRealDevuelve409ParaCedulaDuplicada()
    {
        using var api = CreateApi();
        api.Server.PreserveExecutionContext = true;
        using var client = api.CreateClient();
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var datos = new { Cedula = "api_" + Guid.NewGuid().ToString("N"), Nombre = "Prueba", Correo = "prueba@example.com" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/clientes", datos)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/clientes", datos);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal("No se puede registrar el cliente: ya existe un cliente con esa cédula.", problem!.Title);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/clientes")).StatusCode);
    }

    [SqlFact]
    public async Task ApiRealDevuelve409ParaTerceraMascota()
    {
        using var api = CreateApi();
        api.Server.PreserveExecutionContext = true;
        using var client = api.CreateClient();
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var cedula = "api_" + Guid.NewGuid().ToString("N");
        var registro = await client.PostAsJsonAsync("/api/clientes", new { Cedula = cedula, Nombre = "Prueba", Correo = "prueba@example.com" });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        var cliente = Assert.Single((await client.GetFromJsonAsync<List<Cliente>>("/api/clientes"))!, c => c.Cedula == cedula);
        var datos = new { Nombre = "Luna", Especie = Guid.NewGuid().ToString("N"), Raza = "Mixta", Peso = 1.25m, cliente.IdCliente };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/mascotas", datos)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/mascotas", datos)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/mascotas", datos);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria.",
            await response.Content.ReadAsStringAsync());
    }
}
