namespace RaceDay.Api.Tests;

/// <summary>Stand-in for Azure Blob Storage so upload endpoints can be tested without a storage account.</summary>
public class FakeBlobStorage : IBlobStorage
{
    public Task<string> UploadAsync(Stream content, string contentType, string folder, string extension, CancellationToken ct = default) =>
        Task.FromResult($"https://fake.blob.local/{folder}/test{extension}");
}

public class UploadTests
{
    private static MultipartFormDataContent Image(string contentType = "image/png", string fileName = "pic.png")
    {
        var bytes = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { bytes, "file", fileName } };
    }

    private async Task<(TestUser organiser, TestUser participant, int eventId)> SetupAsync(RaceDayFactory f)
    {
        var organiser = await Helpers.NewUserAsync(f, "Organiser");
        var participant = await Helpers.NewUserAsync(f, "Participant");
        var eventId = await Helpers.CreateEventAsync(organiser.Client);
        return (organiser, participant, eventId);
    }

    [Fact]
    public async Task Organiser_can_upload_banner()
    {
        using var f = new FakeStorageFactory();
        var (organiser, _, eventId) = await SetupAsync(f);

        var res = await organiser.Client.PostAsync($"/api/events/{eventId}/banner", Image());

        res.EnsureSuccessStatusCode();
        var body = await Helpers.JsonOf(res);
        Assert.StartsWith("https://fake.blob.local/banners/", body.GetProperty("bannerUrl").GetString());
    }

    [Fact]
    public async Task Participant_cannot_upload_banner()
    {
        using var f = new FakeStorageFactory();
        var (_, participant, eventId) = await SetupAsync(f);

        var res = await participant.Client.PostAsync($"/api/events/{eventId}/banner", Image());

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Non_image_is_rejected()
    {
        using var f = new FakeStorageFactory();
        var (organiser, _, eventId) = await SetupAsync(f);

        var res = await organiser.Client.PostAsync($"/api/events/{eventId}/banner", Image("text/plain", "x.txt"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Participant_can_upload_own_picture()
    {
        using var f = new FakeStorageFactory();
        var (_, participant, _) = await SetupAsync(f);

        var res = await participant.Client.PostAsync($"/api/participants/{participant.ProfileId}/picture", Image());

        res.EnsureSuccessStatusCode();
        var body = await Helpers.JsonOf(res);
        Assert.StartsWith("https://fake.blob.local/profiles/", body.GetProperty("profilePictureUrl").GetString());
    }
}

/// <summary>Same in-memory API, with the blob storage swapped for the fake.</summary>
public class FakeStorageFactory : RaceDayFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddSingleton<IBlobStorage, FakeBlobStorage>());
    }
}
