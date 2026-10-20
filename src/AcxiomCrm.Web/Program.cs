using AcxiomCrm.Infrastructure;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.Web.Composition;
using AcxiomCrm.Web.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
LoggingSetup.ConfigureSerilog(builder);

// Infrastructure and typed options
builder.Services.AddInfrastructure(builder.Configuration);

// HTTP Context & Current User
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AcxiomCrm.Application.Abstractions.ICurrentUser, AcxiomCrm.Web.Services.HttpCurrentUser>();
builder.Services.AddScoped<AcxiomCrm.Web.Services.IFlash, AcxiomCrm.Web.Services.TempDataFlash>();

// Authentication & Authorization
builder.Services.AddAppAuthentication(builder.Environment);
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAppAuthorization();

// Anti-forgery with header support
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Health Checks
builder.Services.AddAppHealthChecks();

// OpenAPI / Swagger
builder.Services.AddAppOpenApi(builder.Environment);

// Controllers and views with auto-antiforgery and JSON options
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.DataAnnotations.IValidationAttributeAdapterProvider, AcxiomCrm.Web.Validation.AxAttributeAdapterProvider>();


var app = builder.Build();

// Database migration and seeding on startup if enabled
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
if (databaseOptions.MigrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    await db.Database.MigrateAsync();

    var roleSeeder = scope.ServiceProvider.GetRequiredService<AcxiomCrm.Infrastructure.Identity.RoleSeeder>();
    await roleSeeder.SeedAsync();

    var adminSeeder = scope.ServiceProvider.GetRequiredService<AcxiomCrm.Infrastructure.Identity.BootstrapAdminSeeder>();
    await adminSeeder.SeedAsync();

    if (databaseOptions.SeedDemoData)
    {
        var demoSeeder = scope.ServiceProvider.GetRequiredService<AcxiomCrm.Infrastructure.Seeding.DemoDataSeeder>();
        await demoSeeder.SeedAsync();
    }
}

// Global exception handling and status codes
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Request logging & Correlation ID
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI / Swagger UI
app.UseAppOpenApi();

// Health check endpoints
app.MapAppHealthChecks();

app.MapControllers();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
