using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Persistence;
using YourOrderWeDeliver.Core.Pricing;
using YourOrderWeDeliver.Web.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

DmsOptions options = builder.Configuration.GetSection(DmsOptions.SectionName).Get<DmsOptions>() ?? new DmsOptions();
string dataFile = Path.Combine(builder.Environment.ContentRootPath, options.DataFile);

// Bootstrap (A2 Section 7): the single DeliveryManagementSystem loads saved state and builds the roster and register.
DeliveryManagementSystem system = DeliveryManagementSystem.Initialise(
    new JsonFileDataStore(dataFile), options.CreateServiceArea(), StandardPricingPolicy.Default(), new SystemClock());
if (options.SeedDemoData)
{
    DemoDataSeeder.SeedIfEmpty(system);
}

builder.Services.AddSingleton(system);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<StaffAccountService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(cookie =>
    {
        cookie.LoginPath = "/Account/SignIn";
        cookie.AccessDeniedPath = "/Account/AccessDenied";
        cookie.ExpireTimeSpan = TimeSpan.FromHours(10);
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Office, policy => policy.RequireRole(Roles.OfficeStaff, Roles.Manager))
    .AddPolicy(Policies.Manager, policy => policy.RequireRole(Roles.Manager))
    .AddPolicy(Policies.DeliveryStaff, policy => policy.RequireRole(Roles.DeliveryStaff))
    .AddPolicy(Policies.AnyStaff, policy => policy.RequireRole(Roles.OfficeStaff, Roles.Manager, Roles.DeliveryStaff));

builder.Services.AddRazorPages(pages =>
{
    pages.Conventions.AuthorizeFolder("/Office", Policies.Office);
    pages.Conventions.AuthorizeFolder("/Manager", Policies.Manager);
    pages.Conventions.AuthorizeFolder("/Driver", Policies.DeliveryStaff);
    pages.Conventions.AuthorizeFolder("/Availability", Policies.AnyStaff);
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(DisplayFormat.DisplayCulture),
    SupportedCultures = [DisplayFormat.DisplayCulture],
    SupportedUICultures = [DisplayFormat.DisplayCulture],
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SingleWriterMiddleware>();
app.MapRazorPages();

Console.WriteLine($"Delivery Management System ready. Data file: {dataFile}");
app.Run();
