using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ApexPost.Web.ViewModels;

/// <summary>
/// Marks a field of the named form: when that form was refused, the field gets its typed value back and shows its
/// error beside it. A select only shows the error; its view chooses the option with <see cref="FormMemory"/>.
/// </summary>
[HtmlTargetElement("input", Attributes = FormAttribute)]
[HtmlTargetElement("select", Attributes = FormAttribute)]
[HtmlTargetElement("textarea", Attributes = FormAttribute)]
public sealed class RememberedFieldTagHelper : TagHelper
{
    private const string FormAttribute = "apex-form";
    private const string NameAttribute = "name";
    private const string IdAttribute = "id";
    private const string ValueAttribute = "value";
    private const string TextareaTag = "textarea";
    private const string SelectTag = "select";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName(FormAttribute)]
    public string Form { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var field = output.Attributes[NameAttribute]?.Value?.ToString() ?? string.Empty;
        var tempData = ViewContext.TempData;
        if (tempData.FieldValue(Form, field) is { } value)
        {
            RestoreValue(output, value);
        }

        if (tempData.FieldError(Form, field) is { } error)
        {
            var errorId = $"{output.Attributes[IdAttribute]?.Value}-error";
            output.Attributes.SetAttribute("aria-invalid", "true");
            output.Attributes.SetAttribute("aria-describedby", errorId);
            var message = new TagBuilder("span");
            message.Attributes[IdAttribute] = errorId;
            message.AddCssClass("field-error");
            message.InnerHtml.Append(error);
            output.PostElement.AppendHtml(message);
        }
    }

    private static void RestoreValue(TagHelperOutput output, string value)
    {
        if (output.TagName == TextareaTag)
        {
            output.Content.SetContent(value);
        }
        else if (output.TagName != SelectTag)
        {
            output.Attributes.SetAttribute(ValueAttribute, value);
        }
    }
}
