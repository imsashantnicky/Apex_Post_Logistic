using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApexPost.Web.ViewModels;

/// <summary>Readable enum names for the views, for example "Branch Manager" and "Office Supplies".</summary>
public static partial class EnumText
{
    private const string Space = " ";

    public static string Label<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        WordBoundary().Replace(value.ToString(), Space);

    public static IReadOnlyList<SelectListItem> Options<TEnum>(string? selected = null)
        where TEnum : struct, Enum =>
        [
            .. Enum.GetValues<TEnum>().Select(value =>
                new SelectListItem(Label(value), value.ToString(), value.ToString() == selected)),
        ];

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();
}
