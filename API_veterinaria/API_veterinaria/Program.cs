
namespace API_veterinaria
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddScoped<API_veterinaria.Data.IVeterinariaRepository, API_veterinaria.Data.VeterinariaRepository>();
            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            builder.Services.AddProblemDetails();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();
            app.UseExceptionHandler();
            app.UseStatusCodePages(async statusContext =>
            {
                var context = statusContext.HttpContext;
                context.Response.Headers.CacheControl = "no-store";
                await Results.Problem(statusCode: context.Response.StatusCode,
                    title: context.Response.StatusCode switch
                    {
                        404 => "El recurso solicitado no existe.",
                        405 => "El método HTTP no está permitido.",
                        401 or 403 => "No se pudo autorizar la solicitud.",
                        _ => "No se pudo procesar la solicitud."
                    }, extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
                    .ExecuteAsync(context);
            });

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
