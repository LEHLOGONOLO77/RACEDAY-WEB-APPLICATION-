namespace RaceDay.Web.Controllers;

// "My Results": the participant's race history.
[SessionAuth("Participant")]
public class ResultsController : AppController
{
    private readonly ApiClient _api;
    public ResultsController(ApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var mine = await _api.GetAsync<List<ResultModel>>($"api/participants/{CurrentUser!.ProfileId}/results");
        if (!mine.Ok)
        {
            ViewBag.LoadError = mine.Error;
            return View(new List<MyResultRow>());
        }

        var results = mine.Data ?? new();
        var eventIds = results.Select(r => r.EventId).Distinct().ToList();

        // For each event: its date, and everyone's results so we can say "47th of 312 finishers".
        var details = await Task.WhenAll(eventIds.Select(async id => new
        {
            Id = id,
            Event = await _api.GetAsync<EventModel>($"api/events/{id}"),
            All = await _api.GetAsync<List<ResultModel>>($"api/events/{id}/results")
        }));
        var byEvent = details.ToDictionary(d => d.Id);

        var rows = results.Select(r =>
        {
            var d = byEvent[r.EventId];
            var finishers = d.All.Data?.Count(x => x.EventCategoryId == r.EventCategoryId) ?? 0;
            return new MyResultRow(r, d.Event.Data?.EventDate ?? default, Math.Max(finishers, r.Position));
        }).OrderByDescending(r => r.EventDate).ToList();

        return View(rows);
    }
}
