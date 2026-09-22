using GKit.BlazorExt;
using GKit.UI.Localization;
using GKit.UI.RadzenExt;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Test.Repo.UI.Radzen.Components;
using Test.Repo.UI.Shared;

const string DemoDatabaseName = "Test.Repo.UI.Radzen.Demo";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddGKitBlazorServices();

builder.Services.AddRadzenComponents();
builder.Services.AddGKitRadzenUi();
builder.Services.AddGKitUiLocalization();

// The EditContext validation bridge resolves validators as IValidator<T>.
builder.Services.AddGKitValidator<Widget, WidgetValidator>();
builder.Services.AddGKitValidator<Category, CategoryValidator>();

builder.Services.AddDbContextFactory<DemoDbContext>(options =>
    options.UseInMemoryDatabase(DemoDatabaseName));

builder.Services.AddLocalization();

var app = builder.Build();

// Seed the in-memory demo data the grids render.
DemoData.Seed(DemoDatabaseName).Dispose();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
