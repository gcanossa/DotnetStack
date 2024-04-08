using System.Globalization;
using GC.Blazor;
using GC.MudBlazor.Localization;
using MudBlazor;
using Test.Repo.UI.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddGCBlazorServices();

builder.Services.AddLocalization();
builder.Services.AddMudBlazorLocalization(CultureInfo.GetCultureInfo("it-IT"))
    .AddLanguage<IT_MudLanguage>();

var app = builder.Build();

var loc = app.Services.GetRequiredService<MudLocalizer>();

var test = loc["MudDataGrid.Cancel"];

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
