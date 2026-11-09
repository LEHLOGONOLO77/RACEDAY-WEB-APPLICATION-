namespace RaceDay.Web.Controllers;

/// <summary>Shared by every Organiser page: login + role gate, and a helper to load "my" event.</summary>
[SessionAuth("Organiser")]
public abstract class OrganiserBase : AppController
{
    protected readonly ApiClient Api;
    protected OrganiserBase(ApiClient api) => Api = api;

    /// <summary>Loads an event and checks it belongs to the signed-in Organiser (the API enforces this too).</summary>
    protected async Task<EventModel?> LoadOwnEventAsync(int eventId)
    {
        var result = await Api.GetAsync<EventModel>($"api/events/{eventId}");
        if (!result.Ok || result.Data is null)
        {
            Failure(result.Error);
            return null;
        }
        if (result.Data.OrganiserId != CurrentUser!.ProfileId)
        {
            Failure("You can only manage your own events.");
            return null;
        }
        return result.Data;
    }
}
