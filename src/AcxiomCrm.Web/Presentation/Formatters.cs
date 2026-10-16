using System.Globalization;
using AcxiomCrm.Application.Abstractions;

namespace AcxiomCrm.Web.Presentation;

public static class Formatters
{
    private static readonly CultureInfo IndianCulture = new("en-IN");

    public static string Money(decimal amount)
    {
        var isNegative = amount < 0;
        var abs = Math.Abs(amount);

        // Check if has fractional cents
        var hasFractions = (abs % 1) != 0;
        var formatted = hasFractions
            ? abs.ToString("#,##,##0.00", IndianCulture)
            : abs.ToString("#,##,##0", IndianCulture);

        return isNegative ? $"-₹{formatted}" : $"₹{formatted}";
    }

    public static string MoneyCompact(decimal amount)
    {
        var isNegative = amount < 0;
        var abs = Math.Abs(amount);

        string compact;
        if (abs >= 10_000_000m)
        {
            var cr = abs / 10_000_000m;
            compact = cr.ToString("0.#", CultureInfo.InvariantCulture) + "Cr";
        }
        else if (abs >= 100_000m)
        {
            var l = abs / 100_000m;
            compact = l.ToString("0.#", CultureInfo.InvariantCulture) + "L";
        }
        else if (abs >= 1_000m)
        {
            var k = abs / 1_000m;
            compact = k.ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }
        else
        {
            compact = abs.ToString("0", CultureInfo.InvariantCulture);
        }

        return isNegative ? $"-₹{compact}" : $"₹{compact}";
    }

    public static string Date(DateOnly date)
    {
        return date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    public static string DateTime(DateTime utc, IClock clock)
    {
        var local = clock.ToBusinessLocal(utc);
        return local.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
    }
}
