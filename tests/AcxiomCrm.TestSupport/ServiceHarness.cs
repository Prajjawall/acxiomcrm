using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Users;
using AcxiomCrm.Infrastructure.Audit;
using AcxiomCrm.Infrastructure.Persistence;
using AcxiomCrm.Infrastructure.Scope;
using AcxiomCrm.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AcxiomCrm.TestSupport;

public sealed class ServiceHarness : IDisposable
{
    public IServiceProvider Services { get; }
    private readonly ServiceProvider _provider;

    private ServiceHarness(ServiceProvider provider)
    {
        _provider = provider;
        Services = provider;
    }

    public static ServiceHarness Create(TestDatabase db, string userId, string role, FakeClock? clock = null, Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        var fakeClock = clock ?? new FakeClock(new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
        var fakeUser = new FakeCurrentUser
        {
            UserId = userId,
            Role = role,
            IsAuthenticated = true
        };

        services.AddSingleton<IClock>(fakeClock);
        services.AddSingleton<ICurrentUser>(fakeUser);

        services.AddScoped<IAuditActionHint, AcxiomCrm.Application.Common.AuditActionHint>();
        services.AddScoped(sp => new AuditSaveChangesInterceptor(
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ICurrentUser>(),
            sp.GetService<IAuditActionHint>()));

        services.AddDbContext<CrmDbContext>((sp, options) =>
        {
            options.UseSqlServer(db.ConnectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            foreach (var customInterceptor in sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>())
            {
                options.AddInterceptors(customInterceptor);
            }
        });
        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IOwnerScope, OwnerScopeResolver>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<AcxiomCrm.Application.Customers.ICustomerService, AcxiomCrm.Infrastructure.Customers.CustomerService>();
        services.AddScoped<AcxiomCrm.Application.Opportunities.IOpportunityService, AcxiomCrm.Infrastructure.Opportunities.OpportunityService>();
        services.AddScoped<AcxiomCrm.Application.Leads.IOpportunityFactory, AcxiomCrm.Infrastructure.Opportunities.OpportunityFactory>();
        services.AddScoped<AcxiomCrm.Application.Leads.ILeadService, AcxiomCrm.Infrastructure.Leads.LeadService>();
        services.AddScoped<AcxiomCrm.Application.FollowUps.IFollowUpService, AcxiomCrm.Infrastructure.FollowUps.FollowUpService>();
        services.AddScoped<AcxiomCrm.Application.Activities.IActivityService, AcxiomCrm.Infrastructure.Activities.ActivityService>();

        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        return new ServiceHarness(provider);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
