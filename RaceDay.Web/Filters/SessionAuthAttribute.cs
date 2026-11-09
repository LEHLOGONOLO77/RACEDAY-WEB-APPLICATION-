using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>Blocks anonymous visitors (and the wrong role) from protected pages.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthAttribute : ActionFilterAttribute
{
    private readonly string? _role;
    public SessionAuthAttribute(string? role = null) => _role = role;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";   // the back button must not reveal pages after logout

        var user = http.Session.GetUser();
        if (user is null)
        {
            var returnUrl = http.Request.Method == "GET" ? http.Request.Path + http.Request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }
        if (_role is not null && user.Role != _role)
            context.Result = new RedirectToActionResult("Denied", "Account", null);
    }
}
