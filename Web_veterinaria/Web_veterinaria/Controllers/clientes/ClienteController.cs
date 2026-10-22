using Microsoft.AspNetCore.Mvc;
using Web_veterinaria.Models.Cliente;
using Web_veterinaria.Services;
namespace Web_veterinaria.Controllers.clientes;
public sealed class ClienteController(VeterinariaApi api, ILogger<ClienteController> logger) : Controller
{
    private const string Vista = "~/Views/Clientes/registrarClientes.cshtml";
    [HttpGet]
    public IActionResult RegistrarClientes() => View(Vista, new clienteModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarClientes(clienteModel model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(Vista, model);
        try
        {
            var error = await api.RegistrarClienteAsync(model, token);
            if (error is null)
            {
                TempData["Exito"] = "Cliente registrado correctamente.";
                return RedirectToAction(nameof(RegistrarClientes));
            }
            ModelState.AddModelError("", error);
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !token.IsCancellationRequested))
        {
            logger.LogError(exception, "No se pudo registrar el cliente");
            ModelState.AddModelError("", "No se pudo contactar con la API. Verifica que esté iniciada e inténtalo nuevamente.");
        }
        return View(Vista, model);
    }
}
