using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Itminus.Tags.SimpleFiles.Compat;
using Xunit;

namespace Itminus.Tags.Tests.SimpleFilesTags.Compat;

/// <summary>
/// <see cref="FileAsyncCompat"/> 的测试。<br/>
/// <br/>
/// 该类型被 SimpleFiles 的读写路径广泛使用（已有大量端到端用例覆盖），
/// 这里只针对**契约本身**收口：文件确实被创建/覆盖、返回的 Task 可直接 await、
/// 以及取消语义在两个框架下一致。
/// </summary>
public class FileAsyncCompatTests : IDisposable
{
    private readonly string _tempDir;

    public FileAsyncCompatTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileAsyncCompatTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string PathOf(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public async Task ReadAllTextAsync_ReturnsFileContent()
    {
        var path = PathOf("read.txt");
        File.WriteAllText(path, "hello");

        var text = await FileAsyncCompat.ReadAllTextAsync(path, CancellationToken.None);

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task WriteAllTextAsync_CreatesFile()
    {
        var path = PathOf("created.txt");
        Assert.False(File.Exists(path));

        await FileAsyncCompat.WriteAllTextAsync(path, "hello", CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public async Task WriteAllTextAsync_OverwritesExistingContent()
    {
        var path = PathOf("overwrite.txt");
        File.WriteAllText(path, "AAAAAAAAAA");

        await FileAsyncCompat.WriteAllTextAsync(path, "B", CancellationToken.None);

        Assert.Equal("B", File.ReadAllText(path));
    }

    [Fact]
    public async Task WriteAllTextAsync_EmptyString_CreatesEmptyFile()
    {
        var path = PathOf("empty.txt");

        await FileAsyncCompat.WriteAllTextAsync(path, string.Empty, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(string.Empty, File.ReadAllText(path));
    }

    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        var path = PathOf("roundtrip.txt");

        await FileAsyncCompat.WriteAllTextAsync(path, "往返-ASCII", CancellationToken.None);
        var text = await FileAsyncCompat.ReadAllTextAsync(path, CancellationToken.None);

        Assert.Equal("往返-ASCII", text);
    }

    [Fact]
    public async Task WriteAllTextAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        // net472 分支在进入前 ct.ThrowIfCancellationRequested()（同步抛），
        // net8.0 分支由 File.WriteAllTextAsync 返回已取消的 Task。
        // 两条路径对调用方都应表现为 OperationCanceledException。
        var path = PathOf("cancelled.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FileAsyncCompat.WriteAllTextAsync(path, "x", cts.Token));
    }
}
