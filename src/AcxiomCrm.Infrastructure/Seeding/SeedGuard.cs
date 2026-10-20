using AcxiomCrm.Infrastructure.Options;
using Microsoft.Extensions.Hosting;

namespace AcxiomCrm.Infrastructure.Seeding;

public static class SeedGuard
{
    public static void EnsureAllowed(IHostEnvironment env, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(options);

        if (env.IsProduction() && options.SeedDemoData)
        {
            throw new InvalidOperationException("Demo data seeding is not permitted in Production.");
        }
    }
}
