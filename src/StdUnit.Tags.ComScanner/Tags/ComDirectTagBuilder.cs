using StdUnit.Tags.ComScanner.Channels;
using System.Text;

namespace StdUnit.Tags.ComScanner.Tags;

/// <summary>
/// 串口测点构建器。<br/>
/// </summary>
public class ComDirectTagBuilder : TagBuilderBase
{
    /// <summary>
    /// 内部默认逻辑：仅支持 STR 类型只读或者只写串口Tag
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">通道类型不匹配 / 测点类型不受支持</exception>
    /// <exception cref="TagsProjectXmlException">access 不是 RO / WO</exception>
    protected override ITag Fallback(ITagChannel channel)
    {
        var tagKind = this.TagDescriptor.TagKind;
        if (string.IsNullOrEmpty(tagKind) || string.Compare(tagKind, BuiltinTagKinds.STR, ignoreCase: true) == 0)
        {
            if (channel is not ComChannelBase<string> com)
            {
                throw new TagsProjectConfigurationException(
                    $"测点 '{this.Name}' 当前通道必须是{nameof(ComChannelBase<string>)}！实际={channel.GetType()}",
                    $"Tag({this.Name})");
            }

            var accessMode = this.TagDescriptor.AccessMode ?? this.Parent.SearchAccessMode();

            ITag tag = accessMode switch
            {
                TagAccessMode.RO => new ComReadOnlyTag<string>(this.TagDescriptor, com, TagContainer.From(this.Parent)),
                TagAccessMode.WO => new ComWriteOnlyTag<string>(this.TagDescriptor, com, TagContainer.From(this.Parent), converter: str => Encoding.UTF8.GetBytes(str)),
                _ => throw new TagsProjectXmlException(
                    $"串口型测点 '{this.Name}' 只支持(RO|WO)访问，当前模式={accessMode}！",
                    $"Tag({this.Name})")
            };
            return tag;
        }

        throw new TagsProjectConfigurationException(
            $"串口测点 '{this.Name}' 的测点类型({tagKind})不受支持（只支持 STR）",
            $"Tag({this.Name})");
    }
}
