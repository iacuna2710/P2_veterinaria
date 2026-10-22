using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Web_veterinaria.Models.Mascota;
using Web_veterinaria.Services;
namespace Web_veterinaria.Controllers;
public sealed class MascotaController(VeterinariaApi api, ILogger<MascotaController> logger) : Controller
{
    private const string Registro = "~/Views/Mascotas/registrarMascotas.cshtml";
    [HttpGet]
    public async Task<IActionResult> RegistrarMascotas(CancellationToken token)
    {
        var model = new mascotaModel();
        await CargarClientesAsync(model, token);
        return View(Registro, model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarMascotas(mascotaModel model, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var error = await api.RegistrarMascotaAsync(model, token);
                if (error is null)
                {
                    TempData["Exito"] = "Mascota registrada correctamente.";
                    return RedirectToAction(nameof(ConsultarMascotas));
                }
                ModelState.AddModelError("", error);
            }
            catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !token.IsCancellationRequested))
            {
                MostrarErrorConexion(exception);
            }
        }
        await CargarClientesAsync(model, token);
        return View(Registro, model);
    }
    [HttpGet]
    public async Task<IActionResult> ConsultarMascotas(CancellationToken token)
    {
        List<mascotaModel> mascotas = [];
        try { mascotas = await api.ConsultarMascotasAsync(token); }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !token.IsCancellationRequested))
        {
            MostrarErrorConexion(exception);
        }
        return View("~/Views/Mascotas/consultarMascotas.cshtml", mascotas);
    }
    private async Task CargarClientesAsync(mascotaModel model, CancellationToken token)
    {
        try
        {
            var clientes = await api.ConsultarClientesAsync(token);
            model.Clientes = clientes.Select(c => new SelectListItem($"{c.Cedula} — {c.Nombre}", c.IdCliente.ToString())).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !token.IsCancellationRequested))
        {
            MostrarErrorConexion(exception);
        }
    }
    private void MostrarErrorConexion(Exception exception)
    {
        logger.LogError(exception, "No se pudo contactar con la API");
        ModelState.AddModelError("", "No se pudo contactar con la API. Verifica que esté iniciada e inténtalo nuevamente.");
    }
}
