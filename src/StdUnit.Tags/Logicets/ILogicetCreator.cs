namespace StdUnit.Tags.Logicets;

/// <summary>
/// 测点逻辑的工厂接口
/// </summary>
public interface ILogicetCreator
{
    /// <summary>
    /// 根据类型创建测点逻辑器
    /// </summary>
    /// <param name="sp">服务提供者</param>
    /// <param name="type"><see cref="ILogicet"/> 类型</param>
    /// <param name="channels">测点通道列表</param>
    /// <param name="tags">测点组</param>
    /// <returns>创建的 ILogicet 实例，如果创建失败则返回 null</returns>
    ILogicet? CreateLogicet(IServiceProvider sp, Type type, IReadOnlyList<ITagChannel> channels, ITagGrp tags);
}
