using API_veterinaria.Data;
using API_veterinaria.Models;
using Microsoft.AspNetCore.Mvc;

namespace API_veterinaria.Controllers;

[ApiController]
[Route("api/mascotas")]
public sealed class MascotasController(IVeterinariaRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Mascota>>> Consultar(CancellationToken cancellationToken) =>
        Ok(await repository.ConsultarMascotasAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarMascota mascota, CancellationToken cancellationToken)
    {
        var id = await repository.RegistrarMascotaAsync(mascota, cancellationToken);
        // El procedimiento devuelve -2 cuando se alcanza el limite, sin insertar ni lanzar excepciones.
        if (id == -2)
        {
            Response.Headers.CacheControl = "no-store";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria."
            };
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return Conflict(problem);
        }
        return StatusCode(StatusCodes.Status201Created, new { IdMascota = id });
    }
}
