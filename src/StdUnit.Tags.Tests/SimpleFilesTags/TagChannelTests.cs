using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.SimpleFiles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StdUnit.Tags.Tests.SimpleFilesTags;

public class TagChannelTests
{


    #region SimpleFilesTagChannel

    [Fact]
    public void Channel_Constructor_SetsProperties()
    {
        var baseDir = TestPaths.TempPath("base");
        var settings = new SimpleFilesSettings(baseDir);
        var logger = NullLogger<SimpleFilesTagChannel>.Instance;
        var channel = new SimpleFilesTagChannel(
            new SimpleFilesTagChannelDescriptor()
            {
                Name = "test-ch",
                BaseDir = settings.BaseDir,
            },
            logger
        );

        Assert.Equal("test-ch", channel.ChannelName());
        Assert.Same(settings.BaseDir, channel.Settings.BaseDir);
        Assert.Equal(SimpleFilesNames.DriverName, channel.Driver());
    }

    [Fact]
    public void Channel_MakePath_WithBaseDir_CombinesPath()
    {
        var baseDir = TestPaths.TempPath("base");
        var settings = new SimpleFilesSettings(baseDir);
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = settings.BaseDir,
        };
        var channel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);

        // 相对地址用正斜杠书写：Windows 的 API 同样接受 '/'，Linux 上则只有 '/' 才是分隔符
        var path = channel.MakePath("sub/file.txt");

        Assert.Equal(Path.Combine(baseDir, "sub/file.txt"), path);
    }

    [Fact]
    public void Channel_MakePath_WithoutBaseDir_ReturnsAddressAsIs()
    {
        var settings = new SimpleFilesSettings(null);
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = settings.BaseDir,
        };
        var channel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);

        // 绝对路径必须是"两个平台都真 rooted"的，否则本用例在 Linux 上并不在测绝对地址
        var absoluteAddress = TestPaths.TempPath("absolute.txt");
        var path = channel.MakePath(absoluteAddress);

        Assert.Equal(absoluteAddress, path);
    }

    [Fact]
    public void Channel_EnsureConnectedAsync_DoesNothing()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = new SimpleFilesSettings(null).BaseDir,
        };
        var channel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);

        var task = channel.EnsureConnectedAsync(false, CancellationToken.None);

        Assert.True(task.Status == TaskStatus.RanToCompletion);
    }

    [Fact]
    public void Channel_DisconnectAsync_DoesNothing()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = new SimpleFilesSettings(null).BaseDir,
        };
        var channel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);

        var task = channel.DisconnectAsync(CancellationToken.None);

        Assert.True(task.Status == TaskStatus.RanToCompletion);
    }

    [Fact]
    public void Channel_Dispose_DoesNotThrow()
    {
        var descriptor = new SimpleFilesTagChannelDescriptor()
        {
            Name = "ch",
            BaseDir = new SimpleFilesSettings(null).BaseDir,
        };
        var channel = new SimpleFilesTagChannel(descriptor, NullLogger<SimpleFilesTagChannel>.Instance);

        channel.Dispose(); // should not throw
    }

    #endregion


}
