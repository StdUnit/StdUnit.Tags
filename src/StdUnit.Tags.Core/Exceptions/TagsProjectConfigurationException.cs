namespace StdUnit.Tags;

/// <summary>
/// 项目配置在<b>语义</b>上不自洽时抛出：<c>channel</c> 指向不存在的通道、<c>driver</c> 未注册、
/// 同一父节点下测点重名、测点种类超出驱动支持范围、通道类型与测点要求不匹配等。<br/>
/// <br/>
/// 这些错误 XML 语法与 XSD 校验都发现不了（XSD 只能保证结构合法），只有在构建测点树时才能发现；
/// 统一成本类型后，用户不必再面对 <c>NullReferenceException</c> 或字典的
/// "An item with the same key has already been added"。<br/>
/// 与 <see cref="TagsProjectValidationException"/> 的分工：前者是"构建过程中立刻发现"，后者是
/// "构建之前由校验器批量扫描后一次性报告"。<br/>
/// <br/>
/// <b>启动期</b>也用它：<see cref="ITagsProject.RunAsync"/> 为入口匹配逻辑组件时，
/// <see cref="ILogicet.MatchEntry"/> / <see cref="ILogicet.Order"/> 抛出异常意味着"这条配置无法判定"——
/// 这是确定性的配置错误，重试不会变好，因此不走 <see cref="ITagsProject.RunnerCrashed"/> 的恢复路径，
/// 而是拒绝启动（此时没有任何入口开始轮询）。
/// </summary>
public class TagsProjectConfigurationException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">出错位置描述，可为 null</param>
    /// <param name="innerException">内部异常，可为 null</param>
    public TagsProjectConfigurationException(string message, string? location = null, Exception? innerException = null)
        : base(message, location, innerException)
    {
    }
}
