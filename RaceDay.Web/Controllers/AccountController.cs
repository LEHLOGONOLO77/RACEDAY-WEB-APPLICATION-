namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly ApiClient _api;
    public AccountController(ApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (CurrentUser is not null) return RedirectToHome(CurrentUser);
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var login = await _api.PostAsync<LoginResponse>("api/auth/login", new { email = vm.Email, password = vm.Password });
        if (!login.Ok || login.Data is null)
        {
            ModelState.AddModelError("", login.Status == System.Net.HttpStatusCode.Unauthorized
                ? "Invalid email or password." : login.Error ?? "We couldn't log you in. Please try again.");
            return View(vm);
        }

        // Fetch the name for the header. The profile endpoints depend on role.
        var name = vm.Email;
        if (login.Data.Role == "Participant")
        {
            var p = await _api.GetAsync<ParticipantModel>($"api/participants/{login.Data.ProfileId}");
            if (p.Ok && p.Data is not null) name = p.Data.Name;
        }
        else
        {
            var o = await _api.GetAsync<OrganiserNameModel>($"api/organisers/{login.Data.ProfileId}");
            if (o.Ok && o.Data is not null) name = o.Data.Name;
        }

        HttpContext.Session.SetUser(new SessionUser(login.Data.ProfileId, login.Data.Role, vm.Email.Trim().ToLowerInvariant(), name));

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return LocalRedirect(vm.ReturnUrl);
        return RedirectToHome(CurrentUser!);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (CurrentUser is not null) return RedirectToHome(CurrentUser);
        return View(new RegisterVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.PostAsync("api/auth/register", new
        {
            name = vm.Name.Trim(),
            surname = vm.Surname.Trim(),
            email = vm.Email.Trim(),
            password = vm.Password,
            phone = vm.Phone.Trim(),
            role = vm.Role,
            dateOfBirth = vm.Role == "Participant" ? vm.DateOfBirth?.ToString("yyyy-MM-dd") : null
        });
        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error ?? "We couldn't create your account. Please try again.");
            return View(vm);
        }

        Success("Account created. You can log in now.");
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (CurrentUser is not null)
            await _api.PostAsync("api/auth/logout");   // ends the API session too
        HttpContext.Session.Clear();
        Success("You have been signed out.");
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Denied() => View();

    private IActionResult RedirectToHome(SessionUser user) =>
        user.IsOrganiser ? RedirectToAction("Index", "Organiser") : RedirectToAction("Index", "Home");
}

public record OrganiserNameModel(int Id, string Name);
