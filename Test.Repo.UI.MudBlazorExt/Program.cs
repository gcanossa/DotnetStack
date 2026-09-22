using System.Globalization;
using GKit.BlazorExt;
using GKit.UI.Localization;
using GKit.UI.MudBlazorExt;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using Test.Repo.UI.MudBlazorExt.Components;
using Test.Repo.UI.Shared;

const string DemoDatabaseName = "Test.Repo.UI.Demo";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddGKitBlazorServices();

builder.Services.AddMudServices();
builder.Services.AddGKitMudBlazorUi();
builder.Services.AddGKitUiLocalization();

builder.Services.AddDbContextFactory<DemoDbContext>(options =>
    options.UseInMemoryDatabase(DemoDatabaseName));

builder.Services.AddLocalization(); ;

var app = builder.Build();

// Seed the in-memory demo data the grids render.
DemoData.Seed(DemoDatabaseName).Dispose();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
