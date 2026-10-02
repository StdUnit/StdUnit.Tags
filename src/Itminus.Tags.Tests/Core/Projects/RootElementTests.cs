using Itminus.Tags.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Itminus.Tags.Tests.Core.Projects;

public class RootElementTests
{
    private readonly ServiceProvider _root;

    public RootElementTests()
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
    public void RootElement_WithExplicitRoot_ReturnsSameElement()
    {
        // Arrange
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;

        var expectedRoot = new XElement("Project",
            new XElement("Channel", new XAttribute("name", "ch1"), new XAttribute("driver", "fake")),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", true),
                new XAttribute("isEnabled", true),
                new XAttribute("channel", "ch1")
            )
        );

        var dir = TestPaths.OutputDirectory;

        // Act
        using var proj = sp.MakeProject(dir!, expectedRoot);

        // Assert
        Assert.NotNull(proj.GetRootElement());
        Assert.Same(expectedRoot, proj.GetRootElement());
        Assert.Equal("Project", proj.GetRootElement()?.Name.LocalName);
    }

    [Fact]
    public void RootElement_WithExplicitRoot_ChannelsAndTagsLoadedFromRoot()
    {
        // Arrange
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;

        var root = new XElement("Project",
            new XElement("Channel", new XAttribute("name", "ch1"), new XAttribute("driver", "fake")),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", true),
                new XAttribute("isEnabled", true),
                new XAttribute("channel", "ch1")
            )
        );

        var dir = TestPaths.OutputDirectory;

        // Act
        using var proj = sp.MakeProject(dir!, root);

        // Assert
        // RootElement 与传入的一致，而 Channels/Tags 应当从该 RootElement 中加载
        Assert.Same(root, proj.GetRootElement());
        Assert.Single(proj.Channels);
        Assert.NotNull(proj.Tags.SelectGrp("g1"));
    }

    [Fact]
    public void RootElement_WhenRootIsNull_LoadsFromIndexXml()
    {
        // Arrange
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;

        // Create a temp directory with an index.xml that uses "fake" driver
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var indexXmlPath = Path.Combine(tempDir, "index.xml");
        var xml = new XElement("Project",
            new XElement("Channel", new XAttribute("name", "ch1"), new XAttribute("driver", "fake")),
            new XElement("TagGrp",
                new XAttribute("name", "g1"),
                new XAttribute("isEntry", true),
                new XAttribute("isEnabled", true),
                new XAttribute("channel", "ch1")
            )
        );
        xml.Save(indexXmlPath);

        try
        {
            // Act
            using var proj = sp.MakeProject(tempDir);

            // Assert
            Assert.NotNull(proj.GetRootElement());
            Assert.Equal("Project", proj.GetRootElement()?.Name.LocalName);

            // Verify it actually contains content from the file
            var channelElements = proj.GetRootElement()?.Elements("Channel");
            Assert.NotNull(channelElements);
            Assert.Contains(channelElements, ch => ch.Attribute("name")?.Value == "ch1");
        }
        finally
        {
            // Clean up the temp directory after the test
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RootElement_WhenRootIsNullAndFileMissing_ThrowsFileNotFoundException()
    {
        // Arrange
        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;

        // Use a temp directory that does NOT contain index.xml
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() => sp.MakeProject(tempDir));
        Assert.Contains("index.xml", ex.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
