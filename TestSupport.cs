using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay.Api.Tests;

/// <summary>Boots the real API in memory, swapping SQL Server for an in-memory database.</summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

/// <summary>A registered, logged-in user. Each has its own HttpClient (and therefore its own session cookie).</summary>
public record TestUser(HttpClient Client, int UserId, int ProfileId, string Email);

public static class Helpers
{
    public const string Password = "Passw0rd!123";

    public static async Task<TestUser> NewUserAsync(RaceDayFactory f, string role)
    {
        var client = f.CreateClient();
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.com";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Test", surname = role, email, password = Password, phone = "0820000000", role,
            dateOfBirth = role == "Participant" ? "1995-05-01" : null
        });
        reg.EnsureSuccessStatusCode();
        var body = await reg.Content.ReadFromJsonAsync<JsonElement>();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return new TestUser(client, body.GetProperty("userId").GetInt32(), body.GetProperty("profileId").GetInt32(), email);
    }

    public static object EventBody(string status = "Open", string name = "Test 10km") => new
    {
        name, description = "A test event", eventDate = DateTime.UtcNow.AddDays(30),
        location = "Johannesburg", distance = 10, eventType = "Run", status
    };

    public static async Task<int> IdOf(HttpResponseMessage r)
    {
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    public static async Task<int> CreateEventAsync(HttpClient organiser, string status = "Open") =>
        await IdOf(await organiser.PostAsJsonAsync("/api/events", EventBody(status)));

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId) =>
        await IdOf(await organiser.PostAsJsonAsync("/api/event-categories",
            new { eventId, categoryName = "10km", distance = 10, entryFee = 150 }));

    public static async Task<int> RegisterForAsync(TestUser participant, int categoryId) =>
        await IdOf(await participant.Client.PostAsJsonAsync("/api/registrations",
            new { participantId = participant.ProfileId, eventCategoryId = categoryId }));

    public static async Task<JsonElement> JsonOf(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();
}
