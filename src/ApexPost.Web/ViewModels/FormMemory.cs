using ApexPost.Application.Services;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ApexPost.Web.ViewModels;

/// <summary>
/// What a refused form leaves for the page it returns to, which it reaches by redirect: the error per field and
/// the typed values. Sensitive fields are never kept, and the form's name keeps it apart from the other forms of a
/// page such as the counter screen. The fields only peek; the layout releases it, so it lasts exactly one page.
/// </summary>
public static class FormMemory
{
    private const string FormKey = "form.name";
    private const string ErrorsKey = "form.errors";
    private const string ValuesKey = "form.values";

    extension(ITempDataDictionary tempData)
    {
        public void RememberForm(
            string form,
            IReadOnlyDictionary<string, string[]> errors,
            IEnumerable<KeyValuePair<string, string>> fields)
        {
            tempData[FormKey] = form;
            tempData[ErrorsKey] = errors.ToDictionary(pair => pair.Key, pair => string.Join(' ', pair.Value));
            tempData[ValuesKey] = fields
                .Where(pair => !AuditRedactor.IsSensitive(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>Reading a TempData entry marks it for removal at the end of the request.</summary>
        public void ReleaseForm()
        {
            _ = tempData[FormKey];
            _ = tempData[ErrorsKey];
            _ = tempData[ValuesKey];
        }

        public string? FieldError(string form, string field) => Lookup(tempData, ErrorsKey, form, field);

        public string? FieldValue(string form, string field) => Lookup(tempData, ValuesKey, form, field);
    }

    private static string? Lookup(ITempDataDictionary tempData, string key, string form, string field) =>
        tempData.Peek(FormKey) as string == form && tempData.Peek(key) is IDictionary<string, string> entries
            ? entries.TryGetValue(field, out var value) ? value : null
            : null;
}
