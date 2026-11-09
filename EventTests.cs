namespace RaceDay.Api.Tests;

public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public EventTests(RaceDayFactory f) => _f = f;

    // ---------- public reads ----------

    [Fact]
    public async Task GetEvents_Anonymous_Returns200()
    {
        var resp = await _f.CreateClient().GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetEventById_Existing_Returns200()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(org.Client);
        var resp = await _f.CreateClient().GetAsync($"/api/events/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetEventById_Missing_Returns404()
    {
        var resp = await _f.CreateClient().GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEvents()
    {
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.OK, (await p.Client.GetAsync("/api/events")).StatusCode);
    }

    // ---------- organiser success ----------

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201_AndOwnsIt()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var resp = await org.Client.PostAsJsonAsync("/api/events", Helpers.EventBody());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal(org.ProfileId, (await Helpers.JsonOf(resp)).GetProperty("organiserId").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(org.Client, "Draft");

        var resp = await org.Client.PutAsJsonAsync($"/api/events/{id}", Helpers.EventBody("Closed", "Renamed"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await Helpers.JsonOf(resp);
        Assert.Equal("Renamed", json.GetProperty("name").GetString());
        Assert.Equal("Closed", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(org.Client);

        Assert.Equal(HttpStatusCode.NoContent, (await org.Client.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Client.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanListOwnEvents()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        await Helpers.CreateEventAsync(org.Client);
        var resp = await org.Client.GetAsync($"/api/organisers/{org.ProfileId}/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, (await Helpers.JsonOf(resp)).GetArrayLength());
    }

    // ---------- failures ----------

    [Fact]
    public async Task CreateEvent_InvalidData_Returns400()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var resp = await org.Client.PostAsJsonAsync("/api/events", new
        {
            name = "", description = "d", eventDate = DateTime.UtcNow, location = "x", distance = -5, eventType = "Run"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateMissingEvent_Returns404()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var resp = await org.Client.PutAsJsonAsync("/api/events/999999", Helpers.EventBody());
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteMissingEvent_Returns404()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await org.Client.DeleteAsync("/api/events/999999")).StatusCode);
    }

    // ---------- unauthenticated ----------

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/events", Helpers.EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUpdateOrDeleteEvent_Returns401()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(org.Client);
        var anon = _f.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync($"/api/events/{id}", Helpers.EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotListOrganiserEvents_Returns401()
    {
        var resp = await _f.CreateClient().GetAsync("/api/organisers/1/events");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------- role enforcement ----------

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.PostAsJsonAsync("/api/events", Helpers.EventBody())).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent_Returns403()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var id = await Helpers.CreateEventAsync(org.Client);

        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.PutAsJsonAsync($"/api/events/{id}", Helpers.EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.DeleteAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotListOrganiserEvents_Returns403()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.GetAsync($"/api/organisers/{org.ProfileId}/events")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(a.Client);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PutAsJsonAsync($"/api/events/{id}", Helpers.EventBody())).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.CreateEventAsync(a.Client);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.DeleteAsync($"/api/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Client.GetAsync($"/api/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotListAnotherOrganisersEvents_Returns403()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/organisers/{a.ProfileId}/events")).StatusCode);
    }

    [Fact]
    public async Task Event_WithRegistrations_CannotBeDeleted_Returns409()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var eventId = await Helpers.CreateEventAsync(org.Client);
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);
        await Helpers.RegisterForAsync(p, catId);

        Assert.Equal(HttpStatusCode.Conflict, (await org.Client.DeleteAsync($"/api/events/{eventId}")).StatusCode);
    }

    // ---------- routes ----------

    [Fact]
    public async Task Organiser_CanCreateUpdateDeleteRoute_AndAnyoneCanListThem()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var eventId = await Helpers.CreateEventAsync(org.Client);

        var create = await org.Client.PostAsJsonAsync("/api/routes", new
        {
            eventId, routeName = "Loop A", distance = 10, startPoint = "Park", finishPoint = "Park", routeDescription = "Flat"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var routeId = await Helpers.IdOf(create);

        var list = await _f.CreateClient().GetAsync($"/api/events/{eventId}/routes");
        Assert.Equal(1, (await Helpers.JsonOf(list)).GetArrayLength());

        var update = await org.Client.PutAsJsonAsync($"/api/routes/{routeId}", new
        {
            routeName = "Loop B", distance = 12, startPoint = "Park", finishPoint = "Gate", routeDescription = "Hilly"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await org.Client.DeleteAsync($"/api/routes/{routeId}")).StatusCode);
    }

    [Fact]
    public async Task Route_Management_RejectsParticipantsAndOtherOrganisers()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var eventId = await Helpers.CreateEventAsync(a.Client);
        var body = new { eventId, routeName = "R", distance = 5, startPoint = "A", finishPoint = "B", routeDescription = "" };

        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.PostAsJsonAsync("/api/routes", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PostAsJsonAsync("/api/routes", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsJsonAsync("/api/routes", body)).StatusCode);
    }

    // ---------- categories ----------

    [Fact]
    public async Task Organiser_CanManageCategories_AndAnyoneCanViewThem()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var eventId = await Helpers.CreateEventAsync(org.Client);
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);

        var list = await _f.CreateClient().GetAsync($"/api/events/{eventId}/categories");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, (await Helpers.JsonOf(list)).GetArrayLength());

        var update = await org.Client.PutAsJsonAsync($"/api/event-categories/{catId}",
            new { categoryName = "21km", distance = 21, entryFee = 250 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await org.Client.DeleteAsync($"/api/event-categories/{catId}")).StatusCode);
    }

    [Fact]
    public async Task Category_Management_RejectsParticipantsAndOtherOrganisers()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var eventId = await Helpers.CreateEventAsync(a.Client);
        var catId = await Helpers.CreateCategoryAsync(a.Client, eventId);
        var body = new { eventId, categoryName = "5km", distance = 5, entryFee = 50 };

        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.PostAsJsonAsync("/api/event-categories", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PostAsJsonAsync("/api/event-categories", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.DeleteAsync($"/api/event-categories/{catId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsJsonAsync("/api/event-categories", body)).StatusCode);
    }

    [Fact]
    public async Task Category_WithRegistrations_CannotBeDeleted_Returns409()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var eventId = await Helpers.CreateEventAsync(org.Client);
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);
        await Helpers.RegisterForAsync(p, catId);

        Assert.Equal(HttpStatusCode.Conflict, (await org.Client.DeleteAsync($"/api/event-categories/{catId}")).StatusCode);
    }
}
