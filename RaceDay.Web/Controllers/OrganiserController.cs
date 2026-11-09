namespace RaceDay.Web.Controllers;

// Organiser dashboard.
public class OrganiserController : OrganiserBase
{
    public OrganiserController(ApiClient api) : base(api) { }

    public async Task<IActionResult> Index()
    {
        var events = await Api.GetAsync<List<EventModel>>($"api/organisers/{CurrentUser!.ProfileId}/events");
        if (!events.Ok)
        {
            ViewBag.LoadError = events.Error;
            return View(new DashboardVm());
        }

        var list = events.Data ?? new();
        var counts = await Task.WhenAll(list.Select(async e =>
        {
            var regs = await Api.GetAsync<List<RegistrationModel>>($"api/events/{e.Id}/registrations");
            return new DashboardRow(e, regs.Data?.Count(r => r.RegistrationStatus != "Cancelled") ?? 0);
        }));

        return View(new DashboardVm { Rows = counts.OrderBy(r => r.Event.EventDate).ToList() });
    }
}
