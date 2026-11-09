namespace RaceDay.Web.Controllers;

[SessionAuth("Participant")]
public class ProfileController : AppController
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private readonly ApiClient _api;
    public ProfileController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<ParticipantModel>($"api/participants/{CurrentUser!.ProfileId}");
        if (!result.Ok || result.Data is null)
        {
            Failure(result.Error);
            return RedirectToAction("Index", "Home");
        }
        return View(ToVm(result.Data));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ProfileVm vm)
    {
        if (!ModelState.IsValid) return View("Index", vm);

        var result = await _api.PutAsync<ParticipantModel>($"api/participants/{CurrentUser!.ProfileId}", new
        {
            name = vm.Name.Trim(),
            surname = vm.Surname.Trim(),
            email = vm.Email.Trim(),
            phone = vm.Phone.Trim(),
            dateOfBirth = vm.DateOfBirth!.Value.ToString("yyyy-MM-dd")
        });
        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error ?? "We couldn't save your changes.");
            return View("Index", vm);
        }

        HttpContext.Session.SetUser(CurrentUser with { Name = vm.Name.Trim(), Email = vm.Email.Trim().ToLowerInvariant() });
        Success("Profile updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Picture(IFormFile? file)
    {
        if (file is null || file.Length == 0) Failure("Please choose an image to upload.");
        else if (file.Length > MaxImageBytes) Failure("The image must be 5 MB or smaller.");
        else
        {
            var result = await _api.UploadAsync<ParticipantModel>($"api/participants/{CurrentUser!.ProfileId}/picture", file);
            if (result.Ok) Success("Profile picture updated.");
            else Failure(result.Error);
        }
        return RedirectToAction(nameof(Index));
    }

    private static ProfileVm ToVm(ParticipantModel p) => new()
    {
        Name = p.Name, Surname = p.Surname, Email = p.Email, Phone = p.Phone,
        DateOfBirth = p.DateOfBirth.ToDateTime(TimeOnly.MinValue), ProfilePictureUrl = p.ProfilePictureUrl
    };
}
