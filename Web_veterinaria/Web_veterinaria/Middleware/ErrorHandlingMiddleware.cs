using System.Text.Encodings.Web;

namespace Web_veterinaria.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error no controlado en {Path}. Referencia: {TraceId}",
                context.Request.Path, context.TraceIdentifier);
            // Una respuesta ya enviada no se puede reemplazar sin corromperla.
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            var status = exception switch
            {
                BadHttpRequestException request => request.StatusCode,
                TimeoutException => StatusCodes.Status504GatewayTimeout,
                HttpRequestException => StatusCodes.Status503ServiceUnavailable,
                OperationCanceledException => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status500InternalServerError
            };
            await WriteErrorAsync(context, status);
        }
    }

    public static async Task WriteErrorAsync(HttpContext context, int status)
    {
        var (titulo, mensaje) = status switch
        {
            400 => ("La solicitud no es válida", "Revisa los datos e inténtalo nuevamente. Si el formulario llevaba mucho tiempo abierto, recarga la página."),
            401 or 403 => ("No tienes acceso a esta página", "No se pudo autorizar la solicitud."),
            404 => ("No encontramos esta página", "Comprueba la dirección o vuelve al inicio."),
            405 => ("Esta operación no está permitida", "Vuelve a la página y utiliza sus formularios y enlaces."),
            408 or 504 => ("La solicitud tardó demasiado", "No se pudo confirmar el resultado. Consulta los registros antes de intentar enviar el formulario otra vez."),
            413 => ("La solicitud es demasiado grande", "Reduce el tamaño de los datos enviados e inténtalo nuevamente."),
            429 => ("Hay demasiadas solicitudes", "Espera un momento antes de intentarlo nuevamente."),
            502 or 503 => ("El servicio no está disponible", "No se pudo confirmar el resultado. Consulta los registros cuando se restablezca el servicio antes de repetir un registro."),
            _ => ("No se pudo completar la solicitud", "Ocurrió un error inesperado. Inténtalo nuevamente.")
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsHead(context.Request.Method)) return;
        var referencia = HtmlEncoder.Default.Encode(context.TraceIdentifier);
        await context.Response.WriteAsync($"""
            <!doctype html>
            <html lang="es">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Error {status} - Veterinaria</title>
                <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
                <link rel="stylesheet" href="/css/site.css">
            </head>
            <body>
                <main class="container error-shell"><section class="surface error-state">
                    <h1>{titulo}</h1>
                    <p>{mensaje}</p>
                    <p>Si el problema continúa, indica esta referencia: <code>{referencia}</code>.</p>
                    <a class="btn btn-success" href="/">Volver al inicio</a>
                </section></main>
            </body>
            </html>
            """, context.RequestAborted);
    }
}
