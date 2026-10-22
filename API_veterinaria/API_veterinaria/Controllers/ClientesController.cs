using API_veterinaria.Data;
using API_veterinaria.Models;
using Microsoft.AspNetCore.Mvc;

namespace API_veterinaria.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(IVeterinariaRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cliente>>> Consultar(CancellationToken cancellationToken) =>
        Ok(await repository.ConsultarClientesAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarCliente cliente, CancellationToken cancellationToken)
    {
        var id = await repository.RegistrarClienteAsync(cliente, cancellationToken);
        // El procedimiento devuelve -1 cuando la cedula ya existe, sin insertar ni lanzar excepciones.
        if (id == -1)
        {
            Response.Headers.CacheControl = "no-store";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "No se puede registrar el cliente: ya existe un cliente con esa cédula."
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return Conflict(problem);
        }
        return StatusCode(StatusCodes.Status201Created, new { IdCliente = id });
    }
}
