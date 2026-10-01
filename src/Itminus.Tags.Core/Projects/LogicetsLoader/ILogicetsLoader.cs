using System.Xml.Linq;

namespace Itminus.Tags.Core.Projects;

/// <summary>
/// Logicet 解析器
/// </summary>
/// <remarks>
/// 插件（dll）加载能力因目标框架而异：<br/>
/// * <c>net8.0</c>：基于 <c>AssemblyLoadContext</c> 按目录隔离加载，支持卸载，
///   <see cref="LoadedLogicets.Disposables"/> 会在项目停止时释放插件；<br/>
/// * <c>net472</c>：退化为 <c>Assembly.LoadFrom</c>，插件仍可独立编译并挂载，但
///   <b>无依赖隔离、无卸载能力</b>，<see cref="LoadedLogicets.Disposables"/> 为空列表。<br/>
/// 该差异不影响在宿主内注册逻辑组件（<c>TryAddLogicet&lt;TLogicet&gt;</c>），该方式在两个框架下均可用。
/// </remarks>
public interface ILogicetsLoader
{
    /// <summary>
    /// 加载 Logicet 列表，并返回 Logicet 实例列表和需要释放的资源列表
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="dlls">dll路径列表</param>
    /// <param name="channels"></param>
    /// <param name="tags"></param>
    /// <returns></returns>
    LoadedLogicets LoadLogicets(IServiceProvider sp, IEnumerable<string> dlls, IReadOnlyList<ITagChannel> channels, ITagGrp tags);
}

/// <summary>
/// 业务逻辑小组件加载结果
/// </summary>
/// <param name="Logicets"></param>
/// <param name="Disposables"></param>
public record LoadedLogicets(IReadOnlyList<ILogicet> Logicets, IReadOnlyList<IDisposable> Disposables);