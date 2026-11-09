using System.Text.RegularExpressions;

namespace RaceDay.Web.Controllers;

[Route("organiser/events/{eventId:int}/results")]
public class OrganiserResultsController : OrganiserBase
{
    public OrganiserResultsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await LoadOwnEventAsync(eventId);
        if (ev is null) return RedirectToAction("Index", "OrganiserEvents");

        var regs = await Api.GetAsync<List<RegistrationModel>>($"api/events/{eventId}/registrations");
        var results = await Api.GetAsync<List<ResultModel>>($"api/events/{eventId}/results");
        if (!regs.Ok || !results.Ok) ViewBag.LoadError = regs.Error ?? results.Error;

        var published = results.Data ?? new();
        var done = published.Select(r => r.ParticipantId).ToHashSet();
        var awaiting = (regs.Data ?? new())
            .Where(r => r.RegistrationStatus != "Cancelled" && !done.Contains(r.ParticipantId))
            .OrderBy(r => r.CategoryName).ThenBy(r => r.ParticipantName).ToList();

        return View(new ResultsEntryVm { Event = ev, Awaiting = awaiting, Published = published });
    }

    [HttpPost("add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int eventId, int participantId, int eventCategoryId, int? position, string? finishTime)
    {
        if (!TryReadEntry(position, finishTime, out var time, out var error))
        {
            Failure(error);
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = await Api.PostAsync<ResultModel>("api/results", new
        {
            participantId, eventId, eventCategoryId, position = position!.Value, finishTime = time.ToString("c")
        });
        if (result.Ok) Success("Result published.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int eventId, int id, int? position, string? finishTime)
    {
        if (!TryReadEntry(position, finishTime, out var time, out var error))
        {
            Failure(error);
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = await Api.PutAsync<ResultModel>($"api/results/{id}", new { position = position!.Value, finishTime = time.ToString("c") });
        if (result.Ok) Success("Result updated.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteAsync($"api/results/{id}");
        if (result.Ok) Success("Result removed.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    /// <summary>Marks the event as Completed once the race is over.</summary>
    [HttpPost("complete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int eventId)
    {
        var ev = await LoadOwnEventAsync(eventId);
        if (ev is null) return RedirectToAction("Index", "OrganiserEvents");

        var result = await Api.PutAsync<EventModel>($"api/events/{eventId}", new
        {
            name = ev.Name, description = ev.Description, eventDate = ev.EventDate, location = ev.Location,
            distance = ev.Distance, eventType = ev.EventType, status = "Completed"
        });
        if (result.Ok) Success("Event marked as completed.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private static bool TryReadEntry(int? position, string? finishTime, out TimeSpan time, out string error)
    {
        time = default;
        error = "";
        if (position is null or < 1)
        {
            error = "Enter a finishing position of 1 or more.";
            return false;
        }
        if (finishTime is null || !Regex.IsMatch(finishTime.Trim(), @"^\d{1,2}:\d{2}:\d{2}$") || !Format.TryParseFinishTime(finishTime, out time))
        {
            error = "Enter the finish time as hh:mm:ss, for example 00:45:30.";
            return false;
        }
        return true;
    }
}
