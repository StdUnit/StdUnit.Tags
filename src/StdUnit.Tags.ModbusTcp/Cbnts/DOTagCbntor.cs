namespace StdUnit.Tags.ModbusTcp;


/// <summary>
/// Modbus的DO点，地址范围00000~09999
/// </summary>
public class DOTagCbntor : ModbusBitSpaceTagCbntorBase
{

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">Modbus 位空间组合（bool 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal DOTagCbntor(TagDescriptor tagDescriptor, TagCbnt<bool> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get => this.Cache.Span[this.CacheOffset];
        set
        {
            if (value is not bool b)
            {
                throw new Exception($"不应该为Bit类型的测点赋值一个类型为{value?.GetType().Name}值");
            }

            if (Value != null && !Value.Equals(b))
            {
                this.Cache.Span[this.CacheOffset] = b;
            }

            Timestamp = DateTime.Now;
            MarkDirty();
        }
    }

}