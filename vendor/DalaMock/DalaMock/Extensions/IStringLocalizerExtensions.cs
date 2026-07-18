namespace DalaMock.Core.Extensions;

public static class IStringLocalizerExtensions
{
    public static string GetString(this IStringLocalizer localizer, string key, string defaultValue)
    {
        var result = localizer[key];
        return result.ResourceNotFound ? defaultValue : result.Value;
    }
}
