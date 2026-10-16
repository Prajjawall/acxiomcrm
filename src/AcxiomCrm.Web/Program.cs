using AcxiomCrm.Infrastructure;
using AcxiomCrm.Infrastructure.Options;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.Web.Composition;
using AcxiomCrm.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics;
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

// Health Checks
builder.Services.AddAppHealthChecks();

// Controllers and views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Database migration on startup if enabled
var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
if (databaseOptions.MigrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    await db.Database.MigrateAsync();
}

// Global exception handling and status codes
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Request logging & Correlation ID
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseRouting();
app.UseAuthorization();

// Health check endpoints
app.MapAppHealthChecks();

// Error handling stubs
app.MapGet("/Error", (HttpContext context) =>
{
    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    if (exceptionHandlerPathFeature?.Error != null)
    {
        Log.Error(exceptionHandlerPathFeature.Error, "Unhandled exception captured by error shell");
    }
    return Results.Text("An error occurred.", "text/plain", statusCode: 500);
});

app.MapGet("/Error/{code:int}", (int code) => Results.Text($"Error {code}.", "text/plain", statusCode: code));

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
