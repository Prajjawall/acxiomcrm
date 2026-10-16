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
builder.Services.AddAppAuthorization();

// Health Checks
builder.Services.AddAppHealthChecks();

// Controllers and views with auto-antiforgery
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

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

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapAppHealthChecks();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
