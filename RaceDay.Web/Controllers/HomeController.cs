using System.Diagnostics;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly ApiClient _api;
    public HomeController(ApiClient api) => _api = api;

    // Public: anyone can browse upcoming events. Enrolling needs a Participant login.
    public async Task<IActionResult> Index(string? q, string? type, DateTime? from, DateTime? to)
    {
        if (CurrentUser is { IsOrganiser: true })
            return RedirectToAction("Index", "Organiser");

        var vm = new HomeVm { Q = q, Type = type, From = from, To = to };
        var result = await _api.GetAsync<List<EventModel>>("api/events");
        if (!result.Ok)
        {
            ViewBag.LoadError = result.Error;
            return View(vm);
        }

        var events = (result.Data ?? new()).Where(e =>
            e.EventDate >= DateTime.Today && (e.Status == "Open" || e.Status == "Closed"));
        if (!string.IsNullOrWhiteSpace(q))
            events = events.Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || e.Location.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type)) events = events.Where(e => e.EventType == type);
        if (from is not null) events = events.Where(e => e.EventDate.Date >= from.Value.Date);
        if (to is not null) events = events.Where(e => e.EventDate.Date <= to.Value.Date);

        vm.Events = events.OrderBy(e => e.EventDate).ToList();
        return View(vm);
    }

    [ResponseCache(Duration = 0, NoStore = true)]
    public IActionResult Error() => View();

    // Friendly pages for 404 etc. (wired up with UseStatusCodePagesWithReExecute)
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        ViewBag.Code = code;
        return View();
    }
}
