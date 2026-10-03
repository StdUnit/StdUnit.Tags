using System.Xml.Linq;

namespace Itminus.Tags;

/// <summary>
/// 访问模式
/// </summary>
public enum TagAccessMode
{
    /// <summary>
    /// Read and Write
    /// </summary>
    RW = 0,


    /// <summary>
    /// ReadOnly
    /// </summary>
    RO = 2,


    /// <summary>
    /// ReadOnce and then WriteOnly
    /// </summary>
    R1W = 1,


    /// <summary>
    /// WriteOnly
    /// </summary>
    WO = 3,
}

/// <summary>
/// 测点描述
/// </summary>
public class TagDescriptor : ITagsDescriptor
{
    /// <summary>
    /// 测点名称——用于展示，在测组中具有唯一性
    /// </summary>
    public string TagName { set; get; } = null!;

    /// <summary>
    /// 测点类型，位、字节、ushort、整型、浮点等
    /// </summary>
    public TagKinds TagKind { set; get; } = BuiltinTagKinds.Unknown;

    /// <summary>
    /// 本测点所占据的内存大小，以字节为单位。<br/>
    /// 通常，在连续Byte型存储中（比如SiemensPLC的DataBlock)：<br/>
    ///     - Bit 也会占据1个字节——每个字节最多可以代表8个Bits；<br/>
    ///     - Int16 会占据2个字节；<br/>
    /// 在连续WORD型存储中(比如Modbus的保持寄存器段），<br/>
    ///     - Bit会占据2个字节，每个点可以代表16个Bits<br/>
    ///     - Float会占据4个字节——用两个连续的点来表示<br/>
    /// 在Modbus中的线圈段，每个点就是一个比特，由于计算机内存的最小单位是字节，我们仍把这种情况下的测点大小记为1<br/>
    /// <br/>
    /// 尽管大部分基础类型的TagSize都可以根据数据类型直接推导出来，但是并不是所有情况都能自动计算：<br/>
    /// 1. 对于底层是连续<bold>Byte型</bold>存储，虽然对于普通的Bit、Byte、Int16等可以在编译时就知道类型大小，但是字符串型仍需要在运行时指定测点长度。<br/>
    /// 2. 对于底层是连续<bold>WORD型</bold>存储（比如Modbus的HoldingRegisters区域）, 即使是一个Bit数据点也会占据2个字节；而Float可能占据两个数据点（4个字节）<br/>
    /// </summary>
    public int TagSize { get; set; }

    /// <summary>
    /// 配置的原始地址字符串<br/>
    /// </summary>
    public TagAddress RawAddress { get; set; } = string.Empty;

    #region
    /// <summary>
    /// 测点地址
    /// </summary>
    private TagAddress? _normalizedAddress = null;

    /// <summary>
    /// 范化后的测点地址。<br/>
    /// 这个不会被持久化到Xml中，
    /// 而是会在运行之前，根据<see cref="RawAddress"/>和上下文进行动态解析。<br/>
    /// </summary>
    public TagAddress NormalizedAddress
    {
        get => (
            String.IsNullOrEmpty(_normalizedAddress) ? 
                RawAddress : 
                _normalizedAddress
            ) ?? "";
        set
        {
            _normalizedAddress = value;
        }
    }
    #endregion

    /// <summary>
    /// 大小尾
    /// </summary>
    public EndianKinds EndianKind { set; get; } = EndianKinds.LittleEndian;

    /// <summary>
    /// 测点访问类型。<br/>
    /// null 表示未指定，在构建阶段会冒泡式向上检索父级配置。<br/>
    /// 如果所有层级均为 null，则会抛出异常。
    /// </summary>
    public TagAccessMode? AccessMode { get; set; }

    /// <summary>
    /// 通道名。<br/>
    /// 对于组合型的子测点，这个属性通常会被忽略，因为会使用多个组合子所共享的父级<see cref="ITagCbnt.Channel"/>或者更上层的<see cref="ITagGrp.Channel"/>；<br/>
    /// 而对于分组的直接子测点，设置了这个属性，会遮蔽上层的通道名。<br/>
    /// </summary>
    public string? ChannelName { set; get; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Note { set; get; }

    /// <summary>
    /// 额外参数
    /// </summary>
    public virtual IDictionary<string, XAttribute> Extras { get; set; } = new Dictionary<string, XAttribute>();
}
