using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace AcxiomCrm.Web.Logging;

public class SensitiveDataMaskingPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordHash",
        "token",
        "refreshToken",
        "accessToken",
        "secret",
        "authorization",
        "securityStamp"
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
        {
            result = null;
            return false;
        }

        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var logProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            if (!prop.CanRead)
            {
                continue;
            }

            var propValue = prop.GetValue(value);
            if (SensitiveKeys.Contains(prop.Name))
            {
                logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue("***")));
            }
            else
            {
                logProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propValue, true)));
            }
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}
