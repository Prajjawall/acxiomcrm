namespace AcxiomCrm.Web.Services;

public enum FlashType
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record FlashMessage(FlashType Type, string Message);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Contract defined in plan")]
public interface IFlash
{
    void Success(string msg);
    void Error(string msg);
    void Warning(string msg);
    void Info(string msg);
    IReadOnlyList<FlashMessage> Drain();
}
