using StdUnit.Tags.Web.Components;
using StdUnit.Tags.Web.Tags;

using MudBlazor.Services;
using StdUnit.Tags.BlazorLib;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(lb =>
{
    lb.AddConsole();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddTags();

builder.Services.AddMudServices();
builder.Services.AddTagsBlazorLib();



var app = builder.Build();


//_ = Task.Run(() =>
//{
//    var projctrl = app.Services.GetRequiredService<TagsProjectCtrl>();

//    var xml = "index-plugin.xml";
//    _ = projctrl.StartPoll(dir: null, xml);
//});

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
