using ApexPost.Web.Controllers;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>
/// Stops an action whose input did not bind. It is a filter because model state only exists after model binding.
/// A form returns to its page with a message, a browser link gets the 400 page and any other caller a
/// ValidationProblemDetails. The sign-in form is not an ApexController: it keeps its one generic message itself.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private const string InvalidFormMessage = "One or more fields are not valid.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var request = context.HttpContext.Request;
        var isForm = HttpMethods.IsPost(request.Method) && request.HasFormContentType;
        if (context.ModelState.IsValid || (isForm && context.Controller is not ApexController))
        {
            await next();
            return;
        }

        context.Result = isForm ? BackToForm(context) : Rejection(context);
    }

    private static IActionResult BackToForm(ActionExecutingContext context)
    {
        var page = (ApexController)context.Controller;
        AuditOutcome.Set(context.HttpContext, StatusCodes.Status400BadRequest);
        page.FlashError(InvalidFormMessage);
        return page.AfterFormError();
    }

    private static IActionResult Rejection(ActionExecutingContext context) =>
        ProblemResponses.WantsHtml(context.HttpContext)
            ? new BadRequestResult()
            : ProblemResponses.ToActionResult(
                ProblemResponses.CreateValidation(context.HttpContext, context.ModelState));
}
