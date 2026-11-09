namespace RaceDay.Web.Controllers;

public class EventsController : AppController
{
    private readonly ApiClient _api;
    public EventsController(ApiClient api) => _api = api;

    // Public: full event information and available categories.
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetAsync<EventModel>($"api/events/{id}");
        if (!ev.Ok || ev.Data is null)
        {
            Failure(ev.Error);
            return RedirectToAction("Index", "Home");
        }

        var cats = await _api.GetAsync<List<CategoryModel>>($"api/events/{id}/categories");
        var vm = new EventDetailsVm { Event = ev.Data, Categories = cats.Data ?? new() };

        // For a logged-in Participant, mark categories they are already enrolled in.
        if (CurrentUser is { IsParticipant: true } user)
        {
            var mine = await _api.GetAsync<List<RegistrationModel>>($"api/participants/{user.ProfileId}/registrations");
            if (mine.Ok && mine.Data is not null)
                vm.EnrolledCategoryIds = mine.Data.Where(r => r.RegistrationStatus != "Cancelled")
                    .Select(r => r.EventCategoryId).ToHashSet();
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, SessionAuth("Participant")]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            Failure("Please choose a category before enrolling.");
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var result = await _api.PostAsync<RegistrationModel>("api/registrations",
            new { participantId = CurrentUser!.ProfileId, eventCategoryId = categoryId.Value });
        if (!result.Ok)
        {
            Failure(result.Error);
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        Success("You're enrolled. Complete payment to confirm your place.");
        return RedirectToAction("Index", "Enrolments");
    }
}
