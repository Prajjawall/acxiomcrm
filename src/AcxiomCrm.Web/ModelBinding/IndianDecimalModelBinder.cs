using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCrm.Web.ModelBinding;

public sealed class IndianDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var rawValue = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Task.CompletedTask;
        }

        // Strip commas, spaces, non-breaking spaces, and currency symbol (₹)
        var cleaned = rawValue
            .Replace(",", "")
            .Replace(" ", "")
            .Replace("\u00A0", "")
            .Replace("₹", "")
            .Trim();

        // Check decimal places
        var dotIndex = cleaned.IndexOf('.');
        if (dotIndex >= 0)
        {
            var fraction = cleaned[(dotIndex + 1)..];
            if (fraction.Length > 2)
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "Enter an amount with at most 2 decimal places.");
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }
        }

        if (decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                "Enter a valid amount.");
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}
