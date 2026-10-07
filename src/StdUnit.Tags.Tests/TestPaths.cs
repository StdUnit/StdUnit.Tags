using System;
using System.IO;

namespace StdUnit.Tags.Tests;

/// <summary>
/// 测试夹具（XML 等）的定位助手。<br/>
/// <br/>
/// <b>为什么不使用 <c>Assembly.GetExecutingAssembly().Location</c>：</b><br/>
/// net472 的测试宿主默认启用 AppDomain 隔离，并据此对程序集做「影子拷贝」(shadow copy)：
/// 程序集会被复制到 <c>%TEMP%\...\assembly\dl3\&lt;hash&gt;\&lt;hash&gt;\</c> 下，于是
/// <c>Assembly.Location</c> 指向临时目录，<b>而且不同程序集指向不同的子目录</b>；
/// 夹具文件（非程序集内容）则根本不会被拷贝过去。<br/>
/// 实测（net472 + 默认设置，即未关闭 AppDomain）：<br/>
/// <c>Assembly.GetExecutingAssembly().Location</c> → <c>%TEMP%\...\dl3\c37c9b25\33718e83_6b52dd01\StdUnit.Tags.Tests.dll</c><br/>
/// <c>typeof(ITagsProject).Assembly.Location</c> → <c>%TEMP%\...\dl3\0f770884\20707337_6a52dd01\StdUnit.Tags.Core.dll</c><br/>
/// <c>AppContext.BaseDirectory</c> → <c>&lt;仓库&gt;\src\StdUnit.Tags.Tests\bin\Debug\net472</c><br/>
/// 因此「用程序集位置推导夹具目录」在 net472 下必然失败（夹具找不到），
/// 而 <see cref="AppContext.BaseDirectory"/> 不受影子拷贝影响，始终是真实输出目录。<br/>
/// <br/>
/// 结论：<b>定位夹具统一走本类</b>。本类的取值与库自身的默认约定
/// （<c>TagsProjectServiceCollection.MakeProject</c> 在 dir 为空时使用的 <c>AppContext.BaseDirectory</c>）
/// <b>完全一致</b>，因此测试里的路径探针与被测代码看到的目录必然是同一个。
/// </summary>
internal static class TestPaths
{
    /// <summary>测试程序集的真实输出目录（夹具根）。影子拷贝安全。</summary>
    internal static string OutputDirectory => AppContext.BaseDirectory;

    /// <summary>
    /// 输出目录下的夹具路径。例如 <c>Fixture("S7Tags")</c>、
    /// <c>Fixture("ComTags", "ComScriptTests")</c>。
    /// </summary>
    internal static string Fixture(params string[] segments)
        => Path.Combine(OutputDirectory, Path.Combine(segments));

    /// <summary>
    /// 临时目录下的某个路径（**只拼字符串，不创建任何东西**）。<br/>
    /// <br/>
    /// 用途：需要"绝对 / rooted 路径"语义的测试数据（如 SimpleFiles 的 <c>BaseDir</c>、绝对地址）。
    /// 为什么写字面量 <c>@"C:\base"</c> 不行：反斜杠在 Linux 上只是普通字符，<c>C:\base</c> 也<b>不是</b> rooted 路径，
    /// 于是 <see cref="Path.Combine(string,string)"/> 在两端行为不同（Windows 会丢弃前一段、Linux 会拼接），
    /// 同一个用例在两个平台上"测的不是同一件事"——它可能在 Linux 上照样通过，却换了分支。
    /// 这里用运行时确定的临时目录，两个平台都是真 rooted。
    /// </summary>
    /// <param name="segments">相对于临时目录的路径片段</param>
    internal static string TempPath(params string[] segments)
    {
        var all = new string[segments.Length + 1];
        all[0] = Path.GetTempPath();
        Array.Copy(segments, 0, all, 1, segments.Length);
        return Path.Combine(all);
    }
}
