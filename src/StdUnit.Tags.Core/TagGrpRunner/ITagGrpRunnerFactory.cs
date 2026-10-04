namespace StdUnit.Tags;


/// <summary>
/// <see cref="ITagGrpRunner"/> 工厂
/// </summary>
public interface ITagGrpRunnerFactory
{
    /// <summary>
    /// 创建一个<see cref="ITagGrpRunner"/>实例
    /// </summary>
    /// <returns></returns>
    ITagGrpRunner Create(ITagsProject project);
}