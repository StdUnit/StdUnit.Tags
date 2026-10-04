using System.Xml.Linq;

namespace StdUnit.Tags.SimpleFiles;

/// <summary>
/// 简单文件通道描述符
/// </summary>
public class SimpleFilesTagChannelDescriptor : TagChannelDescriptor
{
    /// <summary>
    /// c'tor
    /// </summary>
    public SimpleFilesTagChannelDescriptor()
    {
        this.Driver = SimpleFilesNames.DriverName;
    }

    /// <summary>
    /// 基础目录
    /// </summary>
    public string? BaseDir { get; set; }

    /// <inheritdoc/>
    public override XElement ToXElement()
    {
        var element = base.ToXElement();
        if (!string.IsNullOrEmpty(this.BaseDir))
        {
            element.Add(new XElement(nameof(this.BaseDir), this.BaseDir));
        }
        return element;
    }
}

/// <summary>
/// extensions for <see cref="TagChannelDescriptor"/>
/// </summary>
internal static class TagChannelDescriptor_SimpleFilesExtensions
{
    /// <summary>
    /// 把 <see cref="TagChannelDescriptor"/> 转换为 <see cref="SimpleFilesTagChannelDescriptor"/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static SimpleFilesTagChannelDescriptor ToSimpleFilesTagChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != SimpleFilesNames.DriverName)
        {
            throw new InvalidOperationException($"通道驱动错误：期望 {SimpleFilesNames.DriverName}，而当前为{descriptor.Driver}");
        }
        if (descriptor is SimpleFilesTagChannelDescriptor d)
        {
            return d;
        }
        var res = new SimpleFilesTagChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,
        };
        if (descriptor.Extras != null && descriptor.Extras.TryGetValue(nameof(SimpleFilesTagChannelDescriptor.BaseDir), out var baseDirElement))
        {
            res.BaseDir = baseDirElement.Value;
        }
        return res;
    }


}