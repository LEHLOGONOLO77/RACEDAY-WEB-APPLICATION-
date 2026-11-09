using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>What the MVC app remembers about the signed-in user for the length of their session.</summary>
public record SessionUser(int ProfileId, string Role, string Email, string Name)
{
    public bool IsOrganiser => Role == "Organiser";
    public bool IsParticipant => Role == "Participant";
}

public static class SessionExtensions
{
    private const string UserKey = "user";
    public const string ApiCookieKey = "apiCookie";

    public static SessionUser? GetUser(this ISession session)
    {
        var json = session.GetString(UserKey);
        return json is null ? null : JsonSerializer.Deserialize<SessionUser>(json);
    }

    public static void SetUser(this ISession session, SessionUser user) =>
        session.SetString(UserKey, JsonSerializer.Serialize(user));
}
