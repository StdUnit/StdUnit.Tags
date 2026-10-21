using StdUnit.Tags.Compat;
using StdUnit.Tags.Core.Projects;
#if !NETFRAMEWORK
using McMaster.NETCore.Plugins;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace StdUnit.Tags.Logicets;

/// <summary>
/// Logicet 加载器。<br/>
/// <br/>
/// <c>&lt;Logicet&gt;</c>（按 dll 路径动态挂载插件）依赖宿主运行时的**动态加载程序集**能力，因此有三种形态：
/// <list type="bullet">
/// <item><c>net8.0</c> + CoreCLR：<c>AssemblyLoadContext</c> 按目录隔离加载，支持卸载；</item>
/// <item><c>net472</c>：退化为 <c>Assembly.LoadFrom</c>（无隔离、无卸载，见 <see cref="WarnAboutNetFrameworkLimitations"/>）；</item>
/// <item><b>NativeAOT</b>：<b>不支持</b>（运行时直接抛 <see cref="PlatformNotSupportedException"/>），
/// 配置了 <c>&lt;Logicet&gt;</c> 时会以 <see cref="TagsProjectNotSupportedException"/> 快速失败，
/// 见 <see cref="EnsureDynamicLoadingSupported"/>。</item>
/// </list>
/// 三种形态都不影响宿主内注册（<c>ITagsProject.TryAddLogicet&lt;TLogicet&gt;()</c>）。
/// </summary>
internal class LogicetLoader : ILogicetsLoader
{
    private readonly LogicetLoadOptions _options;
    private readonly ILogger<LogicetLoader> _logger;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public LogicetLoader(IOptions<LogicetLoadOptions> options, ILogger<LogicetLoader> logger)
    {
        this._options = options.Value;
        this._logger = logger;
    }


    /// <inheritdoc/>
    public LoadedLogicets LoadLogicets(IServiceProvider sp, IEnumerable<string> dllLocations, ITagsProject proj)
    {
        var dlls = dllLocations as IReadOnlyList<string> ?? dllLocations.ToList();
        WarnAboutNetFrameworkLimitations(dlls);

        // 有意放在 try/catch 之外：宿主不支持动态加载是"环境问题"，不是"这个插件坏了"，
        // 必须冒泡给调用方（TagsProject.Initialize → 使用者的加载失败处理），不能被下面的 catch 降级成日志。
        EnsureDynamicLoadingSupported(dlls);

        var disposables = new List<IDisposable>();
        var logicets = new List<ILogicet>();

        foreach (var dll in dlls)
        {

            // 
            IList<ILogicet> batch;
            IDisposable? loader = null;
            try
            {
                List<Type> sharedTypes = new List<Type> {
                    typeof(ILogicet),
                    typeof(ITagsProject),
                    typeof(ITagChannel),
                    typeof(ITagGrp),
                    typeof(IServiceProvider),
                    typeof(IServiceCollection),
                    typeof(ILogger),
                };

                (batch, loader) = MakeCore(sp, proj, dll, sharedTypes);
                logicets.AddRange(batch);
                disposables.Add(loader);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "加载Logicet失败：dll={dll}", dll);
                if (loader is not null)
                {
                    try
                    {
                        loader.Dispose();
                    }
                    catch
                    {
                        /* 有意忽略 */
                    }
                }
            }

        }
        return new LoadedLogicets(logicets, disposables);
    }

    private (IList<ILogicet> batch, IDisposable loader) MakeCore(IServiceProvider sp, ITagsProject proj, string dll, List<Type> sharedTypes)
    {
        this._options.SharedTypesFilter?.Invoke(dll, sharedTypes);
#if NETFRAMEWORK
        // net472 没有 AssemblyLoadContext，无法使用 McMaster.NETCore.Plugins。
        // 退化为 Assembly.LoadFrom：保留「宿主开发与插件开发分离」的能力（插件可独立编译、按路径挂载），
        // 但缺少两项能力，属于降级用法（限制见 WarnAboutNetFrameworkLimitations 输出的告警）：
        //   ① 无依赖隔离：依赖解析走默认上下文，与宿主同名程序集复用已加载的实例（先加载者为准）；
        //   ② 无卸载：返回空 IDisposable，程序集在宿主进程退出前无法释放。
        // 注意：不要改用 Assembly.LoadFile——它不做标识匹配，插件里的 ILogicet 会被判定为
        //       与宿主不同型（IsAssignableFrom 为 false）而被全部跳过。
        var pluginAssembly = Assembly.LoadFrom(dll);
        var fallbackBatch = MakeLogicets(sp, pluginAssembly, proj);

        return (fallbackBatch, _noopDisposable);
#else
        var loader = PluginLoader.CreateFromAssemblyFile(
            dll,
            isUnloadable: true,
            sharedTypes: [.. sharedTypes]
        );
        var plugin = loader.LoadDefaultAssembly();
        var batch = MakeLogicets(sp, plugin, proj);

        return (batch, loader);
#endif
    }


    /// <summary>
    /// 当前加载器是否支持动态加载程序集？<br/>
    /// </summary>
    protected virtual bool SupportsDynamicAssemblyLoading => DynamicLoadingCompat.IsSupported;

    /// <summary>
    /// 宿主运行时不支持动态加载程序集（NativeAOT）时，配置了 <c>&lt;Logicet&gt;</c> 就快速失败。<br/>
    /// 刻意"抛"而不是"记日志后继续"：配置了却一条逻辑都跑不起来，项目语义已经不完整，
    /// 静默降级在工业场景下比启动失败危险得多。
    /// </summary>
    /// <param name="dlls">XML 中声明的插件 dll 路径</param>
    /// <exception cref="TagsProjectNotSupportedException">宿主不支持动态加载且声明了至少一个插件时</exception>
    private void EnsureDynamicLoadingSupported(IReadOnlyList<string> dlls)
    {
        if (dlls.Count == 0 || this.SupportsDynamicAssemblyLoading)
        {
            return;
        }

        throw NotSupportedByHost(dlls, innerException: null);
    }

    /// <summary>
    /// 构造"宿主不支持动态加载"的加载期异常（消息给出替代方案，便于使用者一步改对）。
    /// </summary>
    private static TagsProjectNotSupportedException NotSupportedByHost(IReadOnlyList<string> dlls, Exception? innerException)
    {
        var dllsText = string.Join(", ", dlls);
        return new TagsProjectNotSupportedException(
            $"当前宿主运行时不支持。典型场景是AOT场景下不支持动态加载程序集。"+
            "如果你使用了AOT+动态Logicet插件，需要改成宿主内注册（ITagsProject.TryAddLogicet<TLogicet>()；",
            location: $"Logicet({dllsText})",
            innerException: innerException);
    }


    /// <summary>
    /// 从程序集创建 Logicet 实例列表。对于无法成功创建实例的类型，会跳过。
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="assembly"></param>
    /// <param name="project"></param>
    /// <returns></returns>
    /// <remarks>
    /// 由于本方法只服务「从程序集动态挂载」这一条路径，传入的 <paramref name="assembly"/>是运行期从 dll 加载的<b>外部程序集</b>：<br/>
    ///     该assembly不参与宿主的裁剪 / AOT 编译，其类型与构造函数都在插件自己的元数据里完整保留。<br/>
    /// 因此这里对 <c>Assembly.GetTypes()</c> 与 DAM 流转的裁剪告警（<c>IL2026</c> / <c>IL2067</c>）一律不适用，抑制以消除噪音。<br/>
    /// <b>必须用特性而不是 <c>#pragma</c></b>：<c>#pragma warning disable</c> 只作用于编译期的 Roslyn/ILLink 分析器，
    /// 而 <c>dotnet publish</c>（NativeAOT / 裁剪）是 ILLink 在元数据上**重新分析**——它只认写进元数据的
    /// <c>[UnconditionalSuppressMessage]</c>，看不到 <c>#pragma</c>（实测：用 <c>#pragma</c> 时编译 0 告警、
    /// 但 publish 会把这两条原样再报一遍）。该特性只存在于 net5.0+，故用 <c>#if</c> 标注。
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "assembly 是运行期按路径加载的插件 dll，不属于宿主的裁剪范围，其类型不会被移除。")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "GetTypes() 返回的插件类型来自未裁剪的外部程序集，其公共构造函数一定存在。")]
#endif
    protected virtual IList<ILogicet> MakeLogicets(IServiceProvider sp, Assembly assembly, ITagsProject project)
    {
        var types = assembly.GetTypes()
            .Where(t =>
                !t.IsInterface && !t.IsAbstract && !t.IsGenericType
                && typeof(ILogicet).IsAssignableFrom(t)
            );

        var logicets = types
            .Select(t =>
            {
                var (logicet, ex) = LogicetProviderUtils.CreateLogicet(sp, t, project);
                if (logicet is null)
                {
                    _logger.LogError(ex, "构建Logicet错误：t={t}", t.Name);
                    return null;
                }
                return logicet;
            })
            .Where(logicet => logicet != null)
            .ToList();
        return logicets!;
    }

    /// <summary>
    /// net472 下 Logicet 插件以 <see cref="Assembly.LoadFrom(string)"/> 加载，缺少依赖隔离与卸载能力。
    /// 这里仅发出 WARNING（不阻断加载）：插件照常可用，但使用者需知悉其限制。net8.0 下为空实现。
    /// </summary>
    private void WarnAboutNetFrameworkLimitations(IReadOnlyList<string> dlls)
    {
#if NETFRAMEWORK
        if (dlls.Count == 0)
        {
            return;
        }

        this._logger.LogWarning(
            "net472 下 Logicet 插件降级为 Assembly.LoadFrom 加载，缺少隔离与卸载能力："
            + "① 不做依赖隔离，插件依赖与宿主同名程序集冲突时以先加载者为准；"
            + "② 不支持卸载，project 停止不会释放插件程序集，插件 dll 在宿主进程退出前一直被锁定，"
            + "同一路径的插件重新编译后若不重启宿主，可能仍运行旧代码。"
            + "dlls={dlls}",
            string.Join(", ", dlls));

        if (this._options.SharedTypesFilter is not null)
        {
            this._logger.LogWarning(
                "net472 下 LogicetLoadOptions.SharedTypesFilter 不生效：插件与宿主本来就在同一加载上下文中，"
                + "同名程序集天然复用同一份实例，无需（也无法）指定共享类型。该回调仍会被调用，但其结果被忽略。");
        }
#endif
    }

#if NETFRAMEWORK
    /// <summary>net472 下插件加载不具备卸载语义，用作 <see cref="IDisposable"/> 占位。</summary>
    private static readonly IDisposable _noopDisposable = new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
#endif

}
