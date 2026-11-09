namespace RaceDay.Web.Models;

// Shapes returned by the RaceDay API (Part 2). Plain data only: no business logic lives in this project.

public record LoginResponse(int UserId, int ProfileId, string Role);

public record EventModel(int Id, int OrganiserId, string Name, string Description, DateTime EventDate,
    string Location, decimal Distance, string EventType, string Status, string? BannerUrl);

public record CategoryModel(int Id, int EventId, string CategoryName, decimal Distance, decimal EntryFee);

public record RegistrationModel(int Id, int ParticipantId, string ParticipantName, int EventId, string EventName,
    int EventCategoryId, string CategoryName, DateTime RegistrationDate, string PaymentStatus,
    string RegistrationStatus, int? BibNumber);

public record ResultModel(int Id, int ParticipantId, string ParticipantName, int EventId, string EventName,
    int EventCategoryId, string CategoryName, int Position, TimeSpan FinishTime);

public record ParticipantModel(int Id, string Name, string Surname, string Email, string Phone,
    DateOnly DateOfBirth, string? ProfilePictureUrl);
