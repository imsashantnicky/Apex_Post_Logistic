using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

/// <summary>
/// Shared controller behaviour: flash messages, and where a failed form returns to. The filters use both; they run
/// around the action, which the controller's own OnActionExecutionAsync cannot do (it runs before every filter).
/// </summary>
public abstract class ApexController : Controller
{
    private const string IndexAction = "Index";

    /// <summary>Where a failed form POST returns to.</summary>
    protected internal virtual IActionResult AfterFormError() => RedirectToAction(IndexAction);

    protected void FlashSuccess(string message) => TempData[FlashKeys.Success] = message;

    protected internal void FlashError(string message) => TempData[FlashKeys.Error] = message;

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
}
