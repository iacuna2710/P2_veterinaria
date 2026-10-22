namespace Web_veterinaria
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpClient<Web_veterinaria.Services.VeterinariaApi>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5132/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            var app = builder.Build();
            app.UseMiddleware<Web_veterinaria.Middleware.ErrorHandlingMiddleware>();
            app.UseStatusCodePages(context => Web_veterinaria.Middleware.ErrorHandlingMiddleware.WriteErrorAsync(
                context.HttpContext, context.HttpContext.Response.StatusCode));

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
            // Los campos HTML de tipo number envían los decimales con punto.
            app.UseRequestLocalization("en-US");
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
