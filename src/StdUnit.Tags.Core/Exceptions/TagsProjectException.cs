namespace StdUnit.Tags;

/// <summary>
/// 本库抛出的异常的基类。<br/>
/// <br/>
/// 目前<b>加载期</b>（XML 解析 / 地址解析 / 配置构建 / 项目校验）已统一到该异常族下，
/// 见 <see cref="TagsProjectLoadException"/>；运行期（读写、连接、清理等）异常仍在逐步细化，
/// 因此<b>不要</b>假设库抛出的所有异常都派生自本类型。<br/>
/// 需要捕获加载期错误时，请优先捕获 <see cref="TagsProjectLoadException"/> 或其具体子类。
/// </summary>
public abstract class TagsProjectException : Exception
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    protected TagsProjectException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    protected TagsProjectException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
