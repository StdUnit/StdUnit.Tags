using StdUnit.Tags.WebUi;
using WebMonitor.Tags;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddLogging(lb =>{
    lb.AddConsole();
    lb.AddDebug();
});
// 只做"绑定"：驱动、控制器、观测流、后台任务全部在 Tags/_Extensions.cs 里注册
// （与 `dotnet new tags.web` 的分层一致：Program.cs 只负责宿主与管线）。
builder.Services.AddTags();

var app = builder.Build();

// WebUi 的端点：结构树 / 值流(SSE) / 写命令 / 监控页；不具名流与每个具名流各一套。
app.MapTagsStream();

app.Run();
