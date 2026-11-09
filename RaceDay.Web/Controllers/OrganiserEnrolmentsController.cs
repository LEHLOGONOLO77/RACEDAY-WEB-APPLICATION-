namespace RaceDay.Web.Controllers;

[Route("organiser/events/{eventId:int}/enrolments")]
public class OrganiserEnrolmentsController : OrganiserBase
{
    public OrganiserEnrolmentsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await LoadOwnEventAsync(eventId);
        if (ev is null) return RedirectToAction("Index", "OrganiserEvents");

        var regs = await Api.GetAsync<List<RegistrationModel>>($"api/events/{eventId}/registrations");
        if (!regs.Ok) ViewBag.LoadError = regs.Error;
        return View(new EnrolmentsVm { Event = ev, Registrations = regs.Data ?? new() });
    }

    [HttpPost("{id:int}/confirm"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int eventId, int id, string paymentStatus)
    {
        var result = await Api.PutAsync($"api/registrations/{id}", new { paymentStatus, registrationStatus = "Confirmed" });
        if (result.Ok) Success("Enrolment confirmed.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/bib"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Bib(int eventId, int id, int? bibNumber)
    {
        if (bibNumber is null or < 1 or > 99999)
        {
            Failure("Enter a bib number between 1 and 99999.");
            return RedirectToAction(nameof(Index), new { eventId });
        }
        var result = await Api.PutAsync($"api/registrations/{id}/bib-number", new { bibNumber = bibNumber.Value });
        if (result.Ok) Success($"Bib {bibNumber} assigned.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
