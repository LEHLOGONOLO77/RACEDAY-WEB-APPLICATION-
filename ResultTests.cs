namespace RaceDay.Api.Tests;

public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public ResultTests(RaceDayFactory f) => _f = f;

    private async Task<(TestUser org, TestUser p, int eventId, int catId)> RegisteredScenarioAsync()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var eventId = await Helpers.CreateEventAsync(org.Client);
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);
        var p = await Helpers.NewUserAsync(_f, "Participant");
        await Helpers.RegisterForAsync(p, catId);
        return (org, p, eventId, catId);
    }

    private static object ResultBody(int participantId, int eventId, int catId, int position = 1) => new
    {
        participantId, eventId, eventCategoryId = catId, position, finishTime = "00:45:30"
    };

    [Fact]
    public async Task Organiser_CanRecordResult_Returns201()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        var resp = await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal(1, (await Helpers.JsonOf(resp)).GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task Participant_CanViewOwnResults()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));

        var resp = await p.Client.GetAsync($"/api/participants/{p.ProfileId}/results");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await Helpers.JsonOf(resp);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("00:45:30", list[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task Anyone_CanViewEventResults()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));

        var resp = await _f.CreateClient().GetAsync($"/api/events/{eventId}/results");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, (await Helpers.JsonOf(resp)).GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotRecordResult_Returns403()
    {
        var (_, p, eventId, catId) = await RegisteredScenarioAsync();
        var resp = await p.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotRecordResult_Returns401()
    {
        var (_, p, eventId, catId) = await RegisteredScenarioAsync();
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotRecordResult_Returns403()
    {
        var (_, p, eventId, catId) = await RegisteredScenarioAsync();
        var other = await Helpers.NewUserAsync(_f, "Organiser");
        var resp = await other.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Result_ForUnregisteredParticipant_Returns400()
    {
        var (org, _, eventId, catId) = await RegisteredScenarioAsync();
        var stranger = await Helpers.NewUserAsync(_f, "Participant");
        var resp = await org.Client.PostAsJsonAsync("/api/results", ResultBody(stranger.ProfileId, eventId, catId));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Result_WithInvalidPosition_Returns400()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        var resp = await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId, position: 0));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Result_Duplicate_Returns409()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId));
        var again = await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId, position: 2));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewAnotherParticipantsResults_Returns403()
    {
        var (_, a, _, _) = await RegisteredScenarioAsync();
        var b = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/participants/{a.ProfileId}/results")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantResultsEndpoint_Returns403()
    {
        var (org, p, _, _) = await RegisteredScenarioAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Client.GetAsync($"/api/participants/{p.ProfileId}/results")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateAndDeleteResult()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        var id = await Helpers.IdOf(await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId)));

        var put = await org.Client.PutAsJsonAsync($"/api/results/{id}", new { position = 2, finishTime = "00:50:00" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(2, (await Helpers.JsonOf(put)).GetProperty("position").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await org.Client.DeleteAsync($"/api/results/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await org.Client.DeleteAsync($"/api/results/{id}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteResult_Returns403()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        var id = await Helpers.IdOf(await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId)));

        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.PutAsJsonAsync($"/api/results/{id}", new { position = 1, finishTime = "00:01:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.DeleteAsync($"/api/results/{id}")).StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUpdateOrDeleteResult_Returns403()
    {
        var (org, p, eventId, catId) = await RegisteredScenarioAsync();
        var other = await Helpers.NewUserAsync(_f, "Organiser");
        var id = await Helpers.IdOf(await org.Client.PostAsJsonAsync("/api/results", ResultBody(p.ProfileId, eventId, catId)));

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PutAsJsonAsync($"/api/results/{id}", new { position = 1, finishTime = "00:01:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.DeleteAsync($"/api/results/{id}")).StatusCode);
    }
}
