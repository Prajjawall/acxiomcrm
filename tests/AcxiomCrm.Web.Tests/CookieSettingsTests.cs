using AcxiomCrm.Web.Composition;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CookieSettingsTests
{
    private sealed class FakeEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AcxiomCrm.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void Cookie_options_are_secure_and_configured()
    {
        var services = new ServiceCollection();
        var env = new FakeEnvironment(Environments.Development);

        services.AddAuthentication();
        services.AddAppAuthentication(env);

        var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var options = monitor.Get(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);

        options.Cookie.Name.ShouldBe(".AcxiomCrm.Auth");
        options.Cookie.HttpOnly.ShouldBeTrue();
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        options.LoginPath.Value.ShouldBe("/Account/Login");
        options.LogoutPath.Value.ShouldBe("/Account/Logout");
        options.AccessDeniedPath.Value.ShouldBe("/Account/AccessDenied");
    }

    [Fact]
    public void Production_uses_secure_cookies_always()
    {
        var services = new ServiceCollection();
        var env = new FakeEnvironment(Environments.Production);

        services.AddAuthentication();
        services.AddAppAuthentication(env);

        var sp = services.BuildServiceProvider();
        var monitor = sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var options = monitor.Get(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);

        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }
}
