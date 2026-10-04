using StdUnit.Tags.Core.Projects;
#if !NETFRAMEWORK
using McMaster.NETCore.Plugins;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace StdUnit.Tags.Logicets;

/// <summary>
/// Logicet 加载器。
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
    public LoadedLogicets LoadLogicets(IServiceProvider sp, IEnumerable<string> dllLocations, IReadOnlyList<ITagChannel> channels, ITagGrp tags)
    {
        var dlls = dllLocations as IReadOnlyList<string> ?? dllLocations.ToList();
        WarnAboutNetFrameworkLimitations(dlls);

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
                    typeof(ITagChannel),
                    typeof(ITagGrp),
                    typeof(IServiceProvider),
                    typeof(IServiceCollection),
                    typeof(ILogger),
                };

                (batch, loader) = MakeCore(sp, channels, tags, dll, sharedTypes);
                logicets.AddRange(batch);
                disposables.Add(loader);
            }
            catch (Exception ex)
            {
                this._logger.LogError("加载Logicet失败：dll={dll}, ex={ex}, strace={strace}", dll, ex.Message, ex.StackTrace);
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

    private (IList<ILogicet> batch, IDisposable loader) MakeCore(IServiceProvider sp, IReadOnlyList<ITagChannel> channels, ITagGrp tags, string dll, List<Type> sharedTypes)
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
        var fallbackBatch = MakeLogicets(sp, pluginAssembly, channels, tags);

        return (fallbackBatch, _noopDisposable);
#else
        var loader = PluginLoader.CreateFromAssemblyFile(
            dll,
            isUnloadable: true,
            sharedTypes: [.. sharedTypes]
        );
        var plugin = loader.LoadDefaultAssembly();
        var batch = MakeLogicets(sp, plugin, channels, tags);

        return (batch, loader);
#endif
    }


    /// <summary>
    /// 从程序集创建 Logicet 实例列表。对于无法成功创建实例的类型，会跳过。
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="assembly"></param>
    /// <param name="channels"></param>
    /// <param name="tags"></param>
    /// <returns></returns>
    protected virtual IList<ILogicet> MakeLogicets(IServiceProvider sp, Assembly assembly, IReadOnlyList<ITagChannel> channels, ITagGrp tags)
    {
        var types = assembly.GetTypes()
            .Where(t =>
                !t.IsInterface && !t.IsAbstract && !t.IsGenericType
                && typeof(ILogicet).IsAssignableFrom(t)
            );

        var logicets = types
            .Select(t =>
            {
                var (logicet, ex) = LogicetProviderUtils.CreateLogicet(sp, t, channels, tags);
                if (logicet is null)
                {
                    _logger.LogError("构建Logicet错误：t={t}, ex={ex}, strace={strace}", t.Name, ex?.Message, ex?.StackTrace);
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
