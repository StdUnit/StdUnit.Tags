using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.SimpleFiles.Compat;

/// <summary>
/// 文本文件的异步读写（与 net8.0 的 <see cref="File"/> 异步签名对齐）。<br/>
/// <br/>
/// 存在的理由：net472 没有 <c>File.ReadAllTextAsync</c> / <c>WriteAllTextAsync</c>
/// （.NET Core 2.0+ 才加入）。这里把该差异集中到一处，调用点无需铺 <c>#if</c>。<br/>
/// net8.0 直接用标准库实现（有标准库就用标准）；
/// net472 直接调同步方法并包成已完成的 Task——
/// 「方法名/返回类型不变、调用点不变」，但**不套 <c>Task.Run</c>**：额外的线程跳转
/// 对本地小文件 I/O 没有收益，反而多一次调度开销。
/// </summary>
internal static class FileAsyncCompat
{
    /// <summary>
    /// 异步读取文件全部文本。
    /// </summary>
    internal static Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
#if NETFRAMEWORK
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(File.ReadAllText(path));
#else
        return File.ReadAllTextAsync(path, ct);
#endif
    }

    /// <summary>
    /// 异步写入文件全部文本。
    /// </summary>
    internal static Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
#if NETFRAMEWORK
        ct.ThrowIfCancellationRequested();
        File.WriteAllText(path, contents);
        return Task.CompletedTask;
#else
        return File.WriteAllTextAsync(path, contents, ct);
#endif
    }
}
