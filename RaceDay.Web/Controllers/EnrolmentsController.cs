namespace RaceDay.Web.Controllers;

[SessionAuth("Participant")]
public class EnrolmentsController : AppController
{
    private readonly ApiClient _api;
    public EnrolmentsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<RegistrationModel>>($"api/participants/{CurrentUser!.ProfileId}/registrations");
        if (!result.Ok) ViewBag.LoadError = result.Error;
        return View(result.Data ?? new List<RegistrationModel>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id)
    {
        var result = await _api.PutAsync($"api/registrations/{id}/payment", new { paymentStatus = "Paid" });
        if (result.Ok) Success("Payment received. Your place is confirmed.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.DeleteAsync($"api/registrations/{id}");
        if (result.Ok) Success("Your enrolment has been cancelled.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index));
    }
}
