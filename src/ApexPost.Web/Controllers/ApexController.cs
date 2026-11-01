using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Controllers;

/// <summary>
/// Shared controller behaviour: flash messages, and turning the exceptions of the services into action results.
/// A failed form POST follows Post/Redirect/Get: the message goes into a flash and the browser returns to the form.
/// </summary>
public abstract class ApexController : Controller
{
    private const string IndexAction = "Index";
    private const string ConflictMessage = "This record was changed by someone else. Check it and try again.";
    private const string InvalidFormMessage = "One or more fields are not valid.";

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (IsFormPost && !ModelState.IsValid)
        {
            FlashError(InvalidFormMessage);
            context.Result = AfterFormError();
            return;
        }

        var executed = await next();
        if (executed.Exception is { } exception && !executed.ExceptionHandled && ResultFor(exception) is { } result)
        {
            executed.Result = result;
            executed.ExceptionHandled = true;
        }
    }

    /// <summary>Where a failed form POST returns to.</summary>
    protected virtual IActionResult AfterFormError() => RedirectToAction(IndexAction);

    protected void FlashSuccess(string message) => TempData[FlashKeys.Success] = message;

    protected void FlashError(string message) => TempData[FlashKeys.Error] = message;

    /// <summary>False when the text is not a branch code; a missing code gives true and a null branch.</summary>
    protected static bool TryReadBranch(string? code, out BranchCode? branch)
    {
        branch = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        try
        {
            branch = new BranchCode(code);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    private bool IsFormPost => HttpMethods.IsPost(Request.Method);

    private IActionResult? ResultFor(Exception exception)
    {
        switch (exception)
        {
            case ForbiddenException:
                return Forbid();
            case NotFoundException:
                return NotFound();
        }

        if (!IsFormPost || FormMessage(exception) is not { } message)
        {
            return null;
        }

        FlashError(message);
        return AfterFormError();
    }

    private static string? FormMessage(Exception exception) => exception switch
    {
        ValidationFailedException { Errors.Count: > 0 } invalid =>
            string.Join(' ', invalid.Errors.Values.SelectMany(messages => messages)),
        ValidationFailedException or DomainException => exception.Message,
        ConcurrencyException => ConflictMessage,
        _ => null,
    };
}
