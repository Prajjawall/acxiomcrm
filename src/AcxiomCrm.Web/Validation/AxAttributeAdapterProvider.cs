using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace AcxiomCrm.Web.Validation;

public sealed class AxAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _inner = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
    {
        return attribute switch
        {
            AxRequiredAttribute req => new AxRequiredAttributeAdapter(req, stringLocalizer),
            AxMaxLengthAttribute max => new AxMaxLengthAttributeAdapter(max, stringLocalizer),
            AxEmailAttribute email => new AxEmailAttributeAdapter(email, stringLocalizer),
            AxPhoneAttribute phone => new AxPhoneAttributeAdapter(phone, stringLocalizer),
            AxFutureDateAttribute future => new AxFutureDateAttributeAdapter(future, stringLocalizer),
            _ => _inner.GetAttributeAdapter(attribute, stringLocalizer)
        };
    }
}

public sealed class AxRequiredAttributeAdapter(AxRequiredAttribute attribute, IStringLocalizer? stringLocalizer)
    : AttributeAdapterBase<AxRequiredAttribute>(attribute, stringLocalizer)
{
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-required", GetErrorMessage(context));
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Attribute.ErrorMessage ?? GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}

public sealed class AxMaxLengthAttributeAdapter(AxMaxLengthAttribute attribute, IStringLocalizer? stringLocalizer)
    : AttributeAdapterBase<AxMaxLengthAttribute>(attribute, stringLocalizer)
{
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-length", GetErrorMessage(context));
        MergeAttribute(context.Attributes, "data-val-length-max", Attribute.Length.ToString(CultureInfo.InvariantCulture));
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Attribute.ErrorMessage ?? GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}

public sealed class AxEmailAttributeAdapter(AxEmailAttribute attribute, IStringLocalizer? stringLocalizer)
    : AttributeAdapterBase<AxEmailAttribute>(attribute, stringLocalizer)
{
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-axemail", GetErrorMessage(context));
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Attribute.ErrorMessage ?? GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}

public sealed class AxPhoneAttributeAdapter(AxPhoneAttribute attribute, IStringLocalizer? stringLocalizer)
    : AttributeAdapterBase<AxPhoneAttribute>(attribute, stringLocalizer)
{
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-axphone", GetErrorMessage(context));
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Attribute.ErrorMessage ?? GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}

public sealed class AxFutureDateAttributeAdapter(AxFutureDateAttribute attribute, IStringLocalizer? stringLocalizer)
    : AttributeAdapterBase<AxFutureDateAttribute>(attribute, stringLocalizer)
{
    public override void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = context.ActionContext.HttpContext?.RequestServices?.GetService(typeof(IClock)) as IClock
            ?? throw new InvalidOperationException("IClock is required for AxFutureDate validation.");
        var today = clock.Today;

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-axfuturedate", GetErrorMessage(context));
        MergeAttribute(context.Attributes, "data-val-axfuturedate-today", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        return Attribute.ErrorMessage ?? GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
    }
}
