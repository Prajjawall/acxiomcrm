namespace AcxiomCrm.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public bool MigrateOnStartup { get; set; }

    public bool SeedDemoData { get; set; }
}
