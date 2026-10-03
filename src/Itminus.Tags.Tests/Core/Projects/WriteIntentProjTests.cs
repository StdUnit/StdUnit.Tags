using Itminus.Tags.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.Core.Projects;

public class WriteIntentProjTests
{
    private readonly ServiceProvider _root;

    public WriteIntentProjTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddFakedSupport();
        });

        _root = services.BuildServiceProvider();
    }

    [Fact]
    public async Task TestWriteIntent()
    {
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("S7Tags");

        var entry = new XElement("TagGrp",
            new XAttribute("name", "g1"),
            new XAttribute("isEntry", true),
            new XAttribute("isEnabled", true),
            new XAttribute("channel", "fake")
        );
        var channel = new XElement("Channel",
            new XAttribute("name", "fake"),
            new XAttribute("driver", "fake")
        );
        XElement ele = new XElement("Project", [
            channel,
            entry,
        ]);
        using var proj = factory.Create(dir!, ele);
        var xx = false;
        var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            // 延时1000ms，确保WriteIntent已经注册完成
            await Task.Delay(1000);
            await proj.RunAsync(cts.Token);
        });
        proj.WriteIntent("g1", (entry, ct) =>
        {
            xx = true;
            return default;
        }, out var task);

        // 刚注册完成，WriteIntent还未触发
        Assert.False(xx);
        await task;
        // WriteIntent触发后，xx应该被设置为true
        Assert.True(xx);
        cts.Cancel();
    }


    [Fact]
    public async Task TestWriteIntentException()
    {
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;
        var factory = sp.GetRequiredService<ITagsProjectFactory>();
        var dir = TestPaths.Fixture("S7Tags");

        var entry = new XElement("TagGrp",
            new XAttribute("name", "g1"),
            new XAttribute("isEntry", true),
            new XAttribute("isEnabled", true),
            new XAttribute("channel", "fake")
        );
        var channel = new XElement("Channel",
            new XAttribute("name", "fake"),
            new XAttribute("driver", "fake")
        );
        XElement ele = new XElement("Project", [
            channel,
            entry,
        ]);
        using var proj = factory.Create(dir!, ele);
        var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            // 延时1000ms，确保WriteIntent已经注册完成
            await Task.Delay(1000);
            await proj.RunAsync(cts.Token);
        });
        proj.WriteIntent("g1", (entry, ct) =>
        {
            throw new MyException("test exception");
        }, out var task);

        await Assert.ThrowsAsync<MyException>(async () =>
        {
            await task;
        });
        cts.Cancel();
    }


    class MyException : Exception
    {
        public MyException(string message) : base(message)
        {
        }
    }
}


