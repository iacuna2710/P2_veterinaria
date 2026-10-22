using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_veterinaria;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return false;
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        var (status, message) = exception switch
        {
            SqlException { Number: 50001 } => (409, "Ya existe un cliente con esa cédula."),
            SqlException { Number: 50002 } => (409, "No se puede registrar la mascota: ya hay 2 mascotas de esta especie en la veterinaria."),
            SqlException { Number: 50003 } => (400, "El cliente seleccionado no existe."),
            SqlException { Number: 50004 } => (400, "Los datos enviados no son válidos."),
            SqlException { Number: -2 } => (504, "SQL Server tardó demasiado en responder. Consulta los registros antes de repetir un registro."),
            SqlException { Number: 1205 or -1 or 2 or 53 or 233 or 4060 or 18456 } => (503, "No se pudo acceder a SQL Server. Inténtalo más tarde."),
            BadHttpRequestException request => (request.StatusCode, "La solicitud no es válida."),
            TimeoutException or OperationCanceledException => (504, "La operación tardó demasiado. Consulta los registros antes de repetir un registro."),
            _ => (500, "No se pudo completar la operación. Inténtalo nuevamente.")
        };
        if (status >= 500) logger.LogError(exception, "Error al procesar {Path}. Referencia: {TraceId}", context.Request.Path, context.TraceIdentifier);
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        var problem = new ProblemDetails { Status = status, Title = message };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json", cancellationToken);
        return true;
    }
}
