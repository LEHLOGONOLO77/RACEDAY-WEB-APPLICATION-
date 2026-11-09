using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Session: holds the signed-in user, their role and the API session cookie. Nothing else is stored.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "RaceDay.Web.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});

// The only way this app gets or changes data: the Part 2 REST API.
var apiBase = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");
var acceptAnyCert = builder.Configuration.GetValue<bool>("Api:AcceptAnyCertificate");

builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    // UseCookies = false: the session cookie is attached manually from the user's MVC session.
    var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
    if (acceptAnyCert)   // lets the app call the API's local development certificate (also inside Docker)
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

var app = builder.Build();

// One culture so numbers and dates parse and print the same locally and in Docker.
var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new List<CultureInfo> { culture },
    SupportedUICultures = new List<CultureInfo> { culture }
});

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
