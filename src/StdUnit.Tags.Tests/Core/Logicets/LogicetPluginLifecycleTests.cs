using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StdUnit.Tags.S7;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace StdUnit.Tags.Tests.Core.Logicets;

public class LogicetPluginLifecycleTests
{
    private readonly ServiceProvider _root;

    public LogicetPluginLifecycleTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTagsProjectServices(b =>
        {
            b.AddS7Support();
        });

        this._root = services.BuildServiceProvider();
    }

    [Fact]
    public void TestLoadAndUnloadLogicetPlugin()
    {
        var projectDir = CreateProjectDir();
        var root = CreateXRoot(typeof(TestUnloadableLogicet).Assembly.Location);
        try
        {
            var firstLoadContextRef = LoadAndDisposeProject(projectDir, root);
            ForceUnload(firstLoadContextRef);

            var secondLoadContextRef = LoadAndDisposeProject(projectDir, root);
            ForceUnload(secondLoadContextRef);
        }
        finally
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }

    private WeakReference LoadAndDisposeProject(string projectDir, XElement root)
    {
        using var scope = this._root.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITagsProjectFactory>();

        WeakReference loadContextRef;
        using (var project = factory.Create(projectDir, root))
        {
            var logicets = project.Logicets
                .Where(l => l.GetType().Name == nameof(TestUnloadableLogicet));
            var logicet = Assert.Single(logicets);
            Assert.Equal(nameof(TestUnloadableLogicet), logicet.GetType().Name);
            Assert.Single(project.Channels);
            Assert.IsType<S7TagChannel>(project.Channels[0]);

            var loadContext = AssemblyLoadContext.GetLoadContext(logicet.GetType().Assembly);
            Assert.NotNull(loadContext);
            Assert.NotSame(AssemblyLoadContext.Default, loadContext);
            Assert.True(loadContext.IsCollectible);

            loadContextRef = new WeakReference(loadContext);
        }

        return loadContextRef;
    }

    private static string CreateProjectDir()
    {
        var projectDir = Path.Combine(
            Path.GetTempPath(),
            "StdUnit.Tags.Tests",
            nameof(LogicetPluginLifecycleTests),
            Path.GetRandomFileName()
        );
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }

    private static XElement CreateXRoot(string logicetDllPath)
    {
        var root = new XElement("Project",
            new XElement("Channel",
                new XAttribute("name", "S7-1"),
                new XAttribute("driver", "S7"),
                new XElement("IpAddr", "localhost"),
                new XElement("Rack", "0"),
                new XElement("Slot", "1")),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", "true"),
                new XAttribute("isEnabled", "true"),
                new XAttribute("address", "DB200.100.1"),
                new XAttribute("channel", "S7-1"),
                new XAttribute("scanInterval", "10"),
                new XElement("TagCbnt",
                    new XAttribute("name", "拍照请求"),
                    new XAttribute("access", "RO"),
                    new XAttribute("address", "DB200.100.1"),
                    new XElement("Tag",
                        new XAttribute("name", "拍照-请求-标志"),
                        new XAttribute("address", "DB200.100.1"),
                        new XAttribute("type", "BIT")))),
            new XElement("Logicet", logicetDllPath));

        return root;
    }

    private static void ForceUnload(WeakReference loadContextRef)
    {
        for (var i = 0; loadContextRef.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(100);
        }

        Assert.False(loadContextRef.IsAlive);
    }



    sealed class TestUnloadableLogicet : LogicetBase
    {
        public TestUnloadableLogicet(IReadOnlyList<ITagChannel> channels, ITagGrp tags)
            : base(channels, tags)
        {
        }

        public override int Order => 0;

        public override bool MatchEntry(ITagGrp entry)
        {
            return entry.TagName() == "g1";
        }

        public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)
        {
            return Task.CompletedTask;
        }
    }
}


