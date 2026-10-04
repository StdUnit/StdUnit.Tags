using StdUnit.Tags;
using StdUnit.Tags.SimpleFiles;
using StdUnit.Tags.BlazorLib;
using NixMonitor.Tags;
using NixMonitor.Tags.SimpleTags;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAntiforgery();
builder.Services.AddTagsProjectServices(builder =>
{
    builder.AddSimpleFilesChannel()
        .AddSimpleFilesDirectTagBuilder(
            configure: b => b.WithJsonTagFactory<MyJson>(),
            predicate: b => b.TagDescriptor.TagKind == "MyJson"
        )        // 特化测点：解析 Linux /proc 伪文件（利用泛型 WithFactory<TVal> 注入创建委托）
        .AddSimpleFilesDirectTagBuilder(
            configure: b => b.WithFactory<MemInfo>((descriptor, thisChannel, container) => new MemInfoTag(descriptor, thisChannel, container)),
            predicate: b => b.TagDescriptor.TagKind == "MemInfo"
        )
        .AddSimpleFilesDirectTagBuilder(
            configure: b => b.WithFactory<CpuInfo>((descriptor, thisChannel, container) => new CpuInfoTag(descriptor, thisChannel, container)),
            predicate: b => b.TagDescriptor.TagKind == "CpuInfo"
        )
        .AddSimpleFilesDirectTagBuilder(
            configure: b => b.WithFactory<LoadAvg>((descriptor, thisChannel, container) => new LoadAvgTag(descriptor, thisChannel, container)),
            predicate: b => b.TagDescriptor.TagKind == "LoadAvg"
        ).AddSimpleFilesDirectTagBuilder();
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddTagsBlazorLibCore();

builder.Services.AddHostedService<NixMonitorBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAntiforgery();
app.MapRazorComponents<StdUnit.Tags.BlazorLib.Apps.TagsApp>()
    .AddInteractiveServerRenderMode();
app.Run();