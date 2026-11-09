namespace RaceDay.Api.Tests;

public class RegistrationTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public RegistrationTests(RaceDayFactory f) => _f = f;

    private async Task<(TestUser org, int eventId, int catId)> OpenEventAsync()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var eventId = await Helpers.CreateEventAsync(org.Client);
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);
        return (org, eventId, catId);
    }

    [Fact]
    public async Task Participant_CanRegister_AndRegistrationIsRecorded()
    {
        var (org, eventId, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");

        var resp = await p.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = p.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await Helpers.JsonOf(resp);
        Assert.Equal("Pending", created.GetProperty("registrationStatus").GetString());
        Assert.Equal("Pending", created.GetProperty("paymentStatus").GetString());

        // The participant sees it...
        var mine = await Helpers.JsonOf(await p.Client.GetAsync($"/api/participants/{p.ProfileId}/registrations"));
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(catId, mine[0].GetProperty("eventCategoryId").GetInt32());
        Assert.Equal(eventId, mine[0].GetProperty("eventId").GetInt32());

        // ...and so does the organiser of the event.
        var theirs = await Helpers.JsonOf(await org.Client.GetAsync($"/api/events/{eventId}/registrations"));
        Assert.Equal(1, theirs.GetArrayLength());
        Assert.Equal(p.ProfileId, theirs[0].GetProperty("participantId").GetInt32());
    }

    [Fact]
    public async Task Register_Twice_Returns409()
    {
        var (_, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        await Helpers.RegisterForAsync(p, catId);

        var second = await p.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = p.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_ForAnotherParticipant_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");

        var resp = await a.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = b.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotRegister_Returns403()
    {
        var (org, _, catId) = await OpenEventAsync();
        var resp = await org.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = org.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotRegister_Returns401()
    {
        var (_, _, catId) = await OpenEventAsync();
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/registrations",
            new { participantId = 1, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Register_NonExistentCategory_Returns400()
    {
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var resp = await p.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = p.ProfileId, eventCategoryId = 999999 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_ForEventThatIsNotOpen_Returns400()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var eventId = await Helpers.CreateEventAsync(org.Client, "Draft");
        var catId = await Helpers.CreateCategoryAsync(org.Client, eventId);
        var p = await Helpers.NewUserAsync(_f, "Participant");

        var resp = await p.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = p.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewAnotherParticipantsRegistrations_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(a, catId);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/participants/{a.ProfileId}/registrations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/registrations/{regId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantRegistrationsEndpoint_Returns403()
    {
        var (org, _, _) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await org.Client.GetAsync($"/api/participants/{p.ProfileId}/registrations")).StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotViewEventRegistrations_Returns403()
    {
        var (_, eventId, _) = await OpenEventAsync();
        var other = await Helpers.NewUserAsync(_f, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.GetAsync($"/api/events/{eventId}/registrations")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventRegistrationList_Returns403()
    {
        var (_, eventId, _) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.GetAsync($"/api/events/{eventId}/registrations")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanPay_WhichConfirmsRegistration()
    {
        var (_, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);

        var resp = await p.Client.PutAsJsonAsync($"/api/registrations/{regId}/payment", new { paymentStatus = "Paid" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await Helpers.JsonOf(resp);
        Assert.Equal("Paid", json.GetProperty("paymentStatus").GetString());
        Assert.Equal("Confirmed", json.GetProperty("registrationStatus").GetString());
    }

    [Fact]
    public async Task Participant_CannotConfirmOwnRegistrationWithoutPaying_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);

        var resp = await p.Client.PutAsJsonAsync($"/api/registrations/{regId}",
            new { paymentStatus = "Pending", registrationStatus = "Confirmed" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotMarkOtherParticipantsRegistrationPaid_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(a, catId);

        var resp = await b.Client.PutAsJsonAsync($"/api/registrations/{regId}/payment", new { paymentStatus = "Paid" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Bib_CannotBeAssignedBeforePayment_Returns400_ThenSucceedsAfterPayment()
    {
        var (org, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);

        var before = await org.Client.PutAsJsonAsync($"/api/registrations/{regId}/bib-number", new { bibNumber = 101 });
        Assert.Equal(HttpStatusCode.BadRequest, before.StatusCode);

        await p.Client.PutAsJsonAsync($"/api/registrations/{regId}/payment", new { paymentStatus = "Paid" });

        var after = await org.Client.PutAsJsonAsync($"/api/registrations/{regId}/bib-number", new { bibNumber = 101 });
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(101, (await Helpers.JsonOf(after)).GetProperty("bibNumber").GetInt32());
    }

    [Fact]
    public async Task Bib_DuplicateInSameEvent_Returns409()
    {
        var (org, _, catId) = await OpenEventAsync();
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");
        var regA = await Helpers.RegisterForAsync(a, catId);
        var regB = await Helpers.RegisterForAsync(b, catId);
        await a.Client.PutAsJsonAsync($"/api/registrations/{regA}/payment", new { paymentStatus = "Paid" });
        await b.Client.PutAsJsonAsync($"/api/registrations/{regB}/payment", new { paymentStatus = "Paid" });

        Assert.Equal(HttpStatusCode.OK, (await org.Client.PutAsJsonAsync($"/api/registrations/{regA}/bib-number", new { bibNumber = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await org.Client.PutAsJsonAsync($"/api/registrations/{regB}/bib-number", new { bibNumber = 7 })).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAssignBib_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);
        await p.Client.PutAsJsonAsync($"/api/registrations/{regId}/payment", new { paymentStatus = "Paid" });

        var resp = await p.Client.PutAsJsonAsync($"/api/registrations/{regId}/bib-number", new { bibNumber = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotAssignBib_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var other = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);

        var resp = await other.Client.PutAsJsonAsync($"/api/registrations/{regId}/bib-number", new { bibNumber = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnRegistration_AndCanRegisterAgain()
    {
        var (_, _, catId) = await OpenEventAsync();
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(p, catId);

        Assert.Equal(HttpStatusCode.NoContent, (await p.Client.DeleteAsync($"/api/registrations/{regId}")).StatusCode);
        var after = await Helpers.JsonOf(await p.Client.GetAsync($"/api/registrations/{regId}"));
        Assert.Equal("Cancelled", after.GetProperty("registrationStatus").GetString());

        var again = await p.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = p.ProfileId, eventCategoryId = catId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelAnotherParticipantsRegistration_Returns403()
    {
        var (_, _, catId) = await OpenEventAsync();
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");
        var regId = await Helpers.RegisterForAsync(a, catId);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.DeleteAsync($"/api/registrations/{regId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewParticipantRegisteredForTheirEvent_ButNotOthers()
    {
        var (org, _, catId) = await OpenEventAsync();
        var stranger = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        await Helpers.RegisterForAsync(p, catId);

        Assert.Equal(HttpStatusCode.OK, (await org.Client.GetAsync($"/api/participants/{p.ProfileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.Client.GetAsync($"/api/participants/{p.ProfileId}")).StatusCode);
    }
}
