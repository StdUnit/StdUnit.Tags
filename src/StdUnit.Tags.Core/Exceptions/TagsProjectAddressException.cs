namespace StdUnit.Tags;

/// <summary>
/// 测点地址字符串无法解析时抛出，例如 S7 的 <c>DB200.0.0</c>、Modbus 的 <c>1~40001.0</c>、
/// 合宙的 <c>DO1</c>、SimpleFiles 的相对路径等。<br/>
/// <br/>
/// 地址属于"配置里最容易写错、又最难在运行期定位"的部分，因此单独成类；
/// 消息中会带上原始地址字符串，方便直接定位到 XML 属性。
/// </summary>
public class TagsProjectAddressException : TagsProjectLoadException
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="location">出错位置描述，可为 null</param>
    /// <param name="innerException">内部异常，可为 null</param>
    public TagsProjectAddressException(string message, string? location = null, Exception? innerException = null)
        : base(message, location, innerException)
    {
    }
}
