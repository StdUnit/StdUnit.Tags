using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StdUnit.Tags.ComScanner.Compat;

/// <summary>
/// 文本文件的异步写入（可指定编码）。<br/>
/// <br/>
/// 存在的理由：net472 没有 <c>File.WriteAllTextAsync</c>（.NET Core 2.0+ 才加入）。<br/>
/// net8.0 直接用标准库实现（有标准库就用标准）；<br/>
/// net472 直接调同步方法并包成已完成的 <see cref="Task"/>——「方法名/返回类型不变、
/// 调用点不变」，但<b>不套 <c>Task.Run</c></b>：额外的线程跳转没有收益，反而多一次调度开销。
/// </summary>
internal static class FileAsyncCompat
{
    /// <summary>
    /// 异步写入文件全部文本。
    /// </summary>
    internal static Task WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken ct)
    {
#if NETFRAMEWORK
        ct.ThrowIfCancellationRequested();
        File.WriteAllText(path, contents, encoding);
        return Task.CompletedTask;
#else
        return File.WriteAllTextAsync(path, contents, encoding, ct);
#endif
    }
}
