using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public static class Lookups
{
    public static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };
    public static readonly string[] EventStatuses = { "Draft", "Open", "Closed", "Completed", "Cancelled" };
}

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class RegisterVm : IValidatableObject
{
    [Required] public string Role { get; set; } = "Participant";
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [Required, StringLength(60)] public string Surname { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, Phone] public string Phone { get; set; } = "";
    [Display(Name = "Date of birth"), DataType(DataType.Date)] public DateTime? DateOfBirth { get; set; }
    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Display(Name = "Confirm password"), Compare(nameof(Password), ErrorMessage = "Passwords do not match."), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Role != "Participant" && Role != "Organiser")
            yield return new ValidationResult("Choose Participant or Organiser.", new[] { nameof(Role) });
        if (Role == "Participant")
        {
            if (DateOfBirth is null)
                yield return new ValidationResult("Date of birth is required for participants.", new[] { nameof(DateOfBirth) });
            else if (DateOfBirth > DateTime.Today)
                yield return new ValidationResult("Date of birth cannot be in the future.", new[] { nameof(DateOfBirth) });
        }
    }
}

public class EventFormVm
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required, Display(Name = "Date and time")] public DateTime? EventDate { get; set; }
    [Required] public string Location { get; set; } = "";
    [Required, Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")] public decimal? Distance { get; set; }
    [Required, Display(Name = "Event type")] public string EventType { get; set; } = "Run";
    [Required] public string Status { get; set; } = "Draft";
    public string? BannerUrl { get; set; }
    [Display(Name = "Banner image")] public IFormFile? Banner { get; set; }
}

public class CategoryFormVm
{
    public int Id { get; set; }
    public int EventId { get; set; }
    [Required, Display(Name = "Category name"), StringLength(80)] public string CategoryName { get; set; } = "";
    [Required, Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")] public decimal? Distance { get; set; }
    [Required, Display(Name = "Entry fee"), Range(0, 100000, ErrorMessage = "Entry fee must be 0 or more.")] public decimal? EntryFee { get; set; }
}

public class ProfileVm
{
    [Required, StringLength(60)] public string Name { get; set; } = "";
    [Required, StringLength(60)] public string Surname { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, Phone] public string Phone { get; set; } = "";
    [Required, Display(Name = "Date of birth"), DataType(DataType.Date)] public DateTime? DateOfBirth { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

// ---- page models ----

public class HomeVm
{
    public List<EventModel> Events { get; set; } = new();
    public string? Q { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool Filtered => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Type) || From is not null || To is not null;
}

public class EventDetailsVm
{
    public EventModel Event { get; set; } = null!;
    public List<CategoryModel> Categories { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public record DashboardRow(EventModel Event, int Enrolments);

public class DashboardVm
{
    public List<DashboardRow> Rows { get; set; } = new();
    public int TotalEnrolments => Rows.Sum(r => r.Enrolments);
    public List<DashboardRow> Upcoming => Rows.Where(r => r.Event.EventDate >= DateTime.Now && r.Event.Status != "Cancelled")
        .OrderBy(r => r.Event.EventDate).Take(3).ToList();
}

public class CategoriesVm
{
    public EventModel Event { get; set; } = null!;
    public List<CategoryModel> Categories { get; set; } = new();
    public CategoryFormVm NewCategory { get; set; } = new();
}

public class EnrolmentsVm
{
    public EventModel Event { get; set; } = null!;
    public List<RegistrationModel> Registrations { get; set; } = new();
}

public class ResultsEntryVm
{
    public EventModel Event { get; set; } = null!;
    public List<RegistrationModel> Awaiting { get; set; } = new();
    public List<ResultModel> Published { get; set; } = new();
}

public record MyResultRow(ResultModel Result, DateTime EventDate, int Finishers);
