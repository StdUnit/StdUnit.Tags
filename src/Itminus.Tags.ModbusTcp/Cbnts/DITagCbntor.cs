namespace Itminus.Tags.ModbusTcp;


/// <summary>
/// ModBus的 DI 点，地址范围10000~19999
/// </summary>
public class DITagCbntor : ModbusBitSpaceTagCbntorBase
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 位空间组合（bool 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal DITagCbntor(TagDescriptor tagDescriptor, TagCbnt<bool> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => Cache.Span[CacheOffset];
        set => throw new NotSupportedException($"DI点({this.TagName()}地址={this.RawAddress()})不可写入");
    }

}
