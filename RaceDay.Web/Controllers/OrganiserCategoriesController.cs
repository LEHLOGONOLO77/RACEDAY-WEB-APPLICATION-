namespace RaceDay.Web.Controllers;

[Route("organiser/events/{eventId:int}/categories")]
public class OrganiserCategoriesController : OrganiserBase
{
    public OrganiserCategoriesController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await LoadOwnEventAsync(eventId);
        if (ev is null) return RedirectToAction("Index", "OrganiserEvents");

        var cats = await Api.GetAsync<List<CategoryModel>>($"api/events/{eventId}/categories");
        if (!cats.Ok) ViewBag.LoadError = cats.Error;
        return View(new CategoriesVm { Event = ev, Categories = cats.Data ?? new() });
    }

    [HttpPost("add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int eventId, CategoryFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            Failure(FirstError());
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = await Api.PostAsync<CategoryModel>("api/event-categories", new
        {
            eventId, categoryName = vm.CategoryName.Trim(), distance = vm.Distance!.Value, entryFee = vm.EntryFee!.Value
        });
        if (result.Ok) Success("Category added.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int eventId, int id, CategoryFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            Failure(FirstError());
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = await Api.PutAsync<CategoryModel>($"api/event-categories/{id}", new
        {
            categoryName = vm.CategoryName.Trim(), distance = vm.Distance!.Value, entryFee = vm.EntryFee!.Value
        });
        if (result.Ok) Success("Category saved.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        var result = await Api.DeleteAsync($"api/event-categories/{id}");
        if (result.Ok) Success("Category removed.");
        else Failure(result.Error);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private string FirstError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the category details.";
}
