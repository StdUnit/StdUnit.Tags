using System.IO;
using System.Threading.Tasks;

namespace Itminus.Tags.Tests.Compat;

/// <summary>
/// 测试基础设施：补齐 .NET Core+ 才有的 <see cref="File"/> 异步 API，供 net472 目标使用。<br/>
/// <br/>
/// net472 没有 <c>File.ReadAllTextAsync</c> / <c>WriteAllTextAsync</c>（.NET Core 2.0+ 才加入），
/// 而 SimpleFiles 的测试大量使用它们。这里集中处理该差异，调用点无需逐个铺 <c>#if</c>：
/// 受影响的两个测试文件顶部加一行 <c>using File = Itminus.Tags.Tests.Compat.TestFileCompat;</c>，
/// 即可让文件内的 <c>File.XxxAsync(...)</c> 指向本类型；net8.0 下同样经由此转发到 BCL。<br/>
/// <br/>
/// 注意：本类型只提供这两个文件实际用到的成员（<see cref="Exists"/>、
/// <see cref="ReadAllTextAsync"/>、<see cref="WriteAllTextAsync"/>）。
/// 若要在这些文件里使用其它 <see cref="File"/> 成员，需要在此处补一层转发，
/// 或在调用点改用 <c>System.IO.File</c> 全限定名。<br/>
/// <br/>
/// net472 分支直接调同步方法并包成已完成的 Task（不套 <c>Task.Run</c>）：
/// 保持方法名与返回类型不变、调用点不动，同时避免多一次无意义的线程跳转。
/// </summary>
internal static class TestFileCompat
{
    /// <summary>转发 <see cref="File.Exists(string)"/>（两个目标框架都有）。</summary>
    internal static bool Exists(string path) => System.IO.File.Exists(path);

    /// <summary>异步读取文件全部文本。</summary>
    internal static Task<string> ReadAllTextAsync(string path)
    {
#if NETFRAMEWORK
        return Task.FromResult(System.IO.File.ReadAllText(path));
#else
        return System.IO.File.ReadAllTextAsync(path);
#endif
    }

    /// <summary>异步写入文件全部文本。</summary>
    internal static Task WriteAllTextAsync(string path, string contents)
    {
#if NETFRAMEWORK
        System.IO.File.WriteAllText(path, contents);
        return Task.CompletedTask;
#else
        return System.IO.File.WriteAllTextAsync(path, contents);
#endif
    }
}
