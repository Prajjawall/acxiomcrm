namespace AcxiomCrm.Web.Composition;

public static class AuthenticationSetup
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".AcxiomCrm.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddScoped<Services.ISessionSigner, Services.SessionSigner>();

        return services;
    }
}
