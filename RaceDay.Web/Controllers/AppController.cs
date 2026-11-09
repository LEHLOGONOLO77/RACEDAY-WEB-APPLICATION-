using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public abstract class AppController : Controller
{
    protected SessionUser? CurrentUser => HttpContext.Session.GetUser();

    protected void Success(string message) => TempData["Success"] = message;
    protected void Failure(string? message) => TempData["Error"] = message ?? "Something went wrong. Please try again.";

    /// <summary>If the API said the session ended, send the user back to log in instead of showing an error.</summary>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (HttpContext.Items.ContainsKey("SessionExpired"))
        {
            HttpContext.Session.Clear();
            TempData["Error"] = "Your session has ended. Please log in again.";
            context.Result = RedirectToAction("Login", "Account");
        }
        base.OnActionExecuted(context);
    }
}
