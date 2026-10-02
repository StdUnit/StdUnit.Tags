using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Itminus.Tags.Tests.Core.Projects;

public class MakeProjectTests
{
    private class FakeTagsProjectFactory : ITagsProjectFactory
    {
        public string? CapturedDir { get; private set; }
        public XElement? CapturedRoot { get; private set; }

        public ITagsProject Create(string projRoot, XElement? root = null)
        {
            CapturedDir = projRoot;
            CapturedRoot = root;
            return null!; // We only want to capture parameters for the test
        }
    }

    [Fact]
    public void MakeProject_WithDir_UsesProvidedDir()
    {
        // Arrange
        var services = new ServiceCollection();
        var fakeFactory = new FakeTagsProjectFactory();
        services.AddSingleton<ITagsProjectFactory>(fakeFactory);
        var sp = services.BuildServiceProvider();

        var expectedDir = "some_directory";
        var root = new XElement("Project");

        // Act
        var proj = sp.MakeProject(expectedDir, root);

        // Assert
        Assert.Equal(expectedDir, fakeFactory.CapturedDir);
        Assert.Equal(root, fakeFactory.CapturedRoot);
    }

    [Fact]
    public void MakeProject_WithoutDir_UsesApplicationDirectory()
    {
        // Arrange
        var services = new ServiceCollection();
        var fakeFactory = new FakeTagsProjectFactory();
        services.AddSingleton<ITagsProjectFactory>(fakeFactory);
        var sp = services.BuildServiceProvider();

        // 项目根目录的默认值 = 应用程序目录（AppContext.BaseDirectory），
        // 与测试夹具定位约定（TestPaths.OutputDirectory）一致。
        var expectedDir = TestPaths.OutputDirectory;
        var root = new XElement("Project");

        // Act
        var proj = sp.MakeProject(null, root);

        // Assert
        Assert.Equal(expectedDir, fakeFactory.CapturedDir);
        Assert.Equal(root, fakeFactory.CapturedRoot);
    }

    [Fact]
    public void MakeProject_WithEmptyDir_UsesApplicationDirectory()
    {
        // Arrange
        var services = new ServiceCollection();
        var fakeFactory = new FakeTagsProjectFactory();
        services.AddSingleton<ITagsProjectFactory>(fakeFactory);
        var sp = services.BuildServiceProvider();

        // 同 MakeProject_WithoutDir_UsesApplicationDirectory：默认值 = 应用程序目录
        var expectedDir = TestPaths.OutputDirectory;
        var root = new XElement("Project");

        // Act
        var proj = sp.MakeProject(string.Empty, root);

        // Assert
        Assert.Equal(expectedDir, fakeFactory.CapturedDir);
        Assert.Equal(root, fakeFactory.CapturedRoot);
    }
}
