using System.Text.Json;
using Web_veterinaria.Models.Cliente;
using Web_veterinaria.Models.Mascota;
namespace Web_veterinaria.Services;
public sealed class VeterinariaApi(HttpClient client, ILogger<VeterinariaApi> logger)
{
    public Task<List<clienteModel>> ConsultarClientesAsync(CancellationToken token) => ConsultarAsync<clienteModel>("api/clientes", token);
    public Task<List<mascotaModel>> ConsultarMascotasAsync(CancellationToken token) => ConsultarAsync<mascotaModel>("api/mascotas", token);
    private async Task<List<T>> ConsultarAsync<T>(string route, CancellationToken token)
    {
        try
        {
            return await client.GetFromJsonAsync<List<T>>(route, token)
                ?? throw new JsonException("La API devolvió una lista nula.");
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException("La API devolvió una respuesta no válida.", exception);
        }
    }
    public Task<string?> RegistrarClienteAsync(clienteModel model, CancellationToken token) =>
        RegistrarAsync("api/clientes", new { model.Cedula, model.Nombre, model.Correo }, token);
    public Task<string?> RegistrarMascotaAsync(mascotaModel model, CancellationToken token) =>
        RegistrarAsync("api/mascotas", new { model.Nombre, model.Especie, model.Raza, model.Peso, model.IdCliente }, token);
    private async Task<string?> RegistrarAsync<T>(string route, T model, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync(route, model, token);
        if (response.IsSuccessStatusCode) return null;
        logger.LogWarning("La API devolvió {Status} en {Route}", response.StatusCode, route);
        if ((int)response.StatusCode is 400 or 409)
        {
            try
            {
                using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(token);
                if (body is not null && body.RootElement.TryGetProperty("title", out var title))
                    return title.GetString() ?? "Revisa los datos ingresados.";
            }
            catch (JsonException) { return "Revisa los datos ingresados."; }
        }
        return "No se pudo completar el registro. Inténtalo nuevamente.";
    }
}
