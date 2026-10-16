using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AcxiomCrm.Web.Services;

public sealed class TempDataFlash(
    IHttpContextAccessor httpContextAccessor,
    ITempDataDictionaryFactory tempDataDictionaryFactory) : IFlash
{
    private const string TempDataKey = "_FlashMessages";

    private ITempDataDictionary? TempData
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            return context != null ? tempDataDictionaryFactory.GetTempData(context) : null;
        }
    }

    public void Success(string msg) => Add(FlashType.Success, msg);
    public void Error(string msg) => Add(FlashType.Error, msg);
    public void Warning(string msg) => Add(FlashType.Warning, msg);
    public void Info(string msg) => Add(FlashType.Info, msg);

    private void Add(FlashType type, string msg)
    {
        var list = Load();
        list.Add(new FlashMessage(type, msg));
        Save(list);
    }

    public IReadOnlyList<FlashMessage> Drain()
    {
        var list = Load();
        TempData?.Remove(TempDataKey);
        return list;
    }

    private List<FlashMessage> Load()
    {
        if (TempData?.TryGetValue(TempDataKey, out var raw) == true && raw is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<FlashMessage>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
        return [];
    }

    private void Save(List<FlashMessage> list)
    {
        if (TempData != null)
        {
            TempData[TempDataKey] = JsonSerializer.Serialize(list);
        }
    }
}
