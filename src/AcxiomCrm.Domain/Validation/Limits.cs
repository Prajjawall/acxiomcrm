namespace AcxiomCrm.Domain.Validation;

public static class Limits
{
    public const int Name = 150;
    public const int Email = 254;
    public const int Phone = 10;
    public const int Address = 300;
    public const int CityState = 100;
    public const int Notes = 2000;
    public const int Subject = 200;
    public const int Code = 20;
    public const int CloseReason = 500;
    public const decimal LeadMaxValue = 100_000_000m;
    public const decimal OpportunityMaxAmount = 1_000_000_000m;
}
