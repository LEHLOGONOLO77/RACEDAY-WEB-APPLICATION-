namespace RaceDay.Web.Controllers;

[Route("organiser/events")]
public class OrganiserEventsController : OrganiserBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    public OrganiserEventsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetAsync<List<EventModel>>($"api/organisers/{CurrentUser!.ProfileId}/events");
        if (!result.Ok) ViewBag.LoadError = result.Error;
        return View(result.Data ?? new List<EventModel>());
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new EventFormVm { EventDate = DateTime.Today.AddDays(30).AddHours(8) });

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormVm vm)
    {
        CheckBanner(vm.Banner);
        if (!ModelState.IsValid) return View("Form", vm);

        var created = await Api.PostAsync<EventModel>("api/events", ToDto(vm));
        if (!created.Ok || created.Data is null)
        {
            ModelState.AddModelError("", created.Error ?? "We couldn't create the event.");
            return View("Form", vm);
        }

        var warning = await UploadBannerAsync(created.Data.Id, vm.Banner);
        if (warning is null) Success("Event created.");
        else Failure($"Event created, but the banner wasn't saved: {warning}");
        return RedirectToAction("Index", "OrganiserCategories", new { eventId = created.Data.Id });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await LoadOwnEventAsync(id);
        if (ev is null) return RedirectToAction(nameof(Index));
        return View("Form", new EventFormVm
        {
            Id = ev.Id, Name = ev.Name, Description = ev.Description, EventDate = ev.EventDate, Location = ev.Location,
            Distance = ev.Distance, EventType = ev.EventType, Status = ev.Status, BannerUrl = ev.BannerUrl
        });
    }

    [HttpPost("{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormVm vm)
    {
        vm.Id = id;
        CheckBanner(vm.Banner);
        if (!ModelState.IsValid) return View("Form", vm);

        var updated = await Api.PutAsync<EventModel>($"api/events/{id}", ToDto(vm));
        if (!updated.Ok)
        {
            ModelState.AddModelError("", updated.Error ?? "We couldn't save the event.");
            return View("Form", vm);
        }

        var warning = await UploadBannerAsync(id, vm.Banner);
        if (warning is null) Success("Event updated.");
        else Failure($"Event updated, but the banner wasn't saved: {warning}");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteAsync($"api/events/{id}");
        if (result.Ok) Success("Event deleted.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index));
    }

    private static object ToDto(EventFormVm vm) => new
    {
        name = vm.Name.Trim(),
        description = vm.Description.Trim(),
        eventDate = vm.EventDate!.Value,
        location = vm.Location.Trim(),
        distance = vm.Distance!.Value,
        eventType = vm.EventType,
        status = vm.Status
    };

    private void CheckBanner(IFormFile? file)
    {
        if (file is { Length: > MaxImageBytes })
            ModelState.AddModelError(nameof(EventFormVm.Banner), "The banner image must be 5 MB or smaller.");
    }

    /// <summary>Returns null on success (or when no file was chosen), otherwise a friendly message.</summary>
    private async Task<string?> UploadBannerAsync(int eventId, IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;
        var result = await Api.UploadAsync<EventModel>($"api/events/{eventId}/banner", file);
        return result.Ok ? null : result.Error;
    }
}
