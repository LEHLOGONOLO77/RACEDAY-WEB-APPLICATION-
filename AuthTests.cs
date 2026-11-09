namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public AuthTests(RaceDayFactory f) => _f = f;

    private static object RegBody(string email, string role = "Participant", string password = Helpers.Password,
        string? dob = "1995-05-01") => new
    {
        name = "Test", surname = "User", email, password, phone = "0820000000", role, dateOfBirth = dob
    };

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task Register_Participant_Returns201()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(NewEmail()));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal("Participant", (await Helpers.JsonOf(resp)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_Organiser_Returns201()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(NewEmail(), "Organiser", dob: null));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _f.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegBody(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", RegBody(email));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRoleName_Returns400()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(NewEmail(), "Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRoleNumber_Returns400()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            name = "T", surname = "U", email = NewEmail(), password = Helpers.Password, phone = "082", role = 99
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_ParticipantWithoutDateOfBirth_Returns400()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(NewEmail(), dob: null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(NewEmail(), password: "short"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody("not-an-email"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var email = NewEmail();
        await _f.CreateClient().PostAsJsonAsync("/api/auth/register", RegBody(email));

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(Helpers.Password, user.PasswordHash);
        Assert.DoesNotContain(Helpers.Password, user.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndSessionCookie()
    {
        var client = _f.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegBody(email));

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Helpers.Password });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains(resp.Headers.GetValues("Set-Cookie"), c => c.StartsWith("RaceDay.Session"));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _f.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", RegBody(email));

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = NewEmail(), password = Helpers.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsRole()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var resp = await org.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Organiser", (await Helpers.JsonOf(resp)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_WithoutLogin_Returns401()
    {
        var resp = await _f.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        Assert.Equal(HttpStatusCode.OK, (await org.Client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await org.Client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await org.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewAndUpdateOwnProfile()
    {
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var get = await p.Client.GetAsync($"/api/participants/{p.ProfileId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var put = await p.Client.PutAsJsonAsync($"/api/participants/{p.ProfileId}", new
        {
            name = "Changed", surname = "Name", email = p.Email, phone = "0831111111", dateOfBirth = "1990-01-01"
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Changed", (await Helpers.JsonOf(put)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Participant_CannotViewOrUpdateAnotherParticipantsProfile()
    {
        var a = await Helpers.NewUserAsync(_f, "Participant");
        var b = await Helpers.NewUserAsync(_f, "Participant");

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/participants/{a.ProfileId}")).StatusCode);
        var put = await b.Client.PutAsJsonAsync($"/api/participants/{a.ProfileId}", new
        {
            name = "Hacked", surname = "X", email = a.Email, phone = "1", dateOfBirth = "1990-01-01"
        });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAndUpdateOwnProfile_ButNotOthers()
    {
        var a = await Helpers.NewUserAsync(_f, "Organiser");
        var b = await Helpers.NewUserAsync(_f, "Organiser");

        Assert.Equal(HttpStatusCode.OK, (await a.Client.GetAsync($"/api/organisers/{a.ProfileId}")).StatusCode);
        var put = await a.Client.PutAsJsonAsync($"/api/organisers/{a.ProfileId}",
            new { name = "New", surname = "Name", email = a.Email, phone = "0842222222" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/organisers/{a.ProfileId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAccessOrganiserProfile()
    {
        var org = await Helpers.NewUserAsync(_f, "Organiser");
        var p = await Helpers.NewUserAsync(_f, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.GetAsync($"/api/organisers/{org.ProfileId}")).StatusCode);
    }

    [Fact]
    public async Task PostParticipants_PublicEndpoint_CreatesParticipant()
    {
        var resp = await _f.CreateClient().PostAsJsonAsync("/api/participants", new
        {
            name = "Pat", surname = "Runner", email = NewEmail(), password = Helpers.Password,
            phone = "0820000000", dateOfBirth = "1998-03-04"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CanDeleteOwnAccount_ThenCannotLogIn()
    {
        var p = await Helpers.NewUserAsync(_f, "Participant");
        var del = await p.Client.DeleteAsync($"/api/participants/{p.ProfileId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = p.Email, password = Helpers.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
