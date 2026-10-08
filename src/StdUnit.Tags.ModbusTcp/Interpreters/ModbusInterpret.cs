using StdUnit.Tags.ModbusTcp.Interpreters;
using System.Text;
using System.Xml.Linq;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// Modbus 驱动私有属性 <c>interpret</c> 的读取、记法解析与校验。<br/>
/// <br/>
/// <b>为什么放在驱动侧</b>：这是本驱动独有的能力（S7 的缓存是 PLC 原始字节，语义不同），所以记法通过
/// 测点的 <see cref="TagDescriptor.Extras"/> 里的 <c>interpret</c> 属性传入，由驱动自己解析与校验，Core 不感知。<br/>
/// <br/>
/// <b>记法</b>：一串大写字母，<c>A</c> = 最高字节、<c>B</c> = 次高字节……从左到右列出的是<b>值里的各个字节</b>
/// 在设备端字节里出现的先后顺序，字符数必须等于该数值的<b>字节数</b>（32 位 4 个字符、64 位 8 个字符）。<br/>
/// <br/>
/// <b>与 <c>endian</c> 的分工</b>：<c>endian</c> 只管<b>每个 16 位单元内部</b>两个字节的顺序（协议规定大端，
/// 非标设备可能反放）；寄存器之间的顺序由 <c>interpret</c> 决定。因此每两个连续字符必须是相邻字节，
/// 且先后顺序与 <c>endian</c> 一致：<c>BigEndian</c> ⇒ <c>AB</c>/<c>CD</c>/…，<c>LittleEndian</c> ⇒ <c>BA</c>/<c>DC</c>/…。<br/>
/// <br/>
/// <b>不写 <c>interpret</c> 时</b>：<c>BigEndian</c> = 完全大端（<c>ABCD…</c>，标准设备）；
/// <c>LittleEndian</c> = 完全小端（<c>…DCBA</c>，与 S7 的同名配置语义一致）。<br/>
/// <br/>
/// <b>合法的排布是有限的</b>：16 位单元内部只有《大端/小端》两种，单元之间可以任意排列，于是 32 位有
/// 2 × 2! = 4 种、64 位有 2 × 4! = 48 种。每个合法排布都用上面那串字母写出唯一记法（<see cref="DescribeNotation"/> 可见
/// 记法与落位表的对应关系），
/// 解读器就是按这个编号复用的（见各具体解读器的 <c>For</c> 入口）。<br/>
/// <br/>
/// 记法只描述"字节怎么摆"，具体类型的换算见 <see cref="ModbusValueInterpreter{T}"/>。32 位物理值
/// <c>0x12345678</c> 的四种排布：
/// <list type="table">
/// <item><term><c>ABCD</c></term><description>完全大端（标准）：设备端字节 <c>12 34 56 78</c></description></item>
/// <item><term><c>CDAB</c></term><description>字交换：设备端字节 <c>56 78 12 34</c>（<c>endian="BigEndian" interpret="CDAB"</c>）</description></item>
/// <item><term><c>BADC</c></term><description>字节交换：设备端字节 <c>34 12 78 56</c>（<c>endian="LittleEndian" interpret="BADC"</c>）</description></item>
/// <item><term><c>DCBA</c></term><description>完全小端：设备端字节 <c>78 56 34 12</c>（<c>endian="LittleEndian"</c>）</description></item>
/// </list>
/// <br/>
/// 注意：寄存器在设备端由协议固定为大端、NModbus 也已按此解析成数值，所以这里的换算<b>与主机端序无关</b>。
/// </summary>
internal static class ModbusInterpret
{
    /// <summary>
    /// XML 属性名（位于测点的 <see cref="TagDescriptor.Extras"/> 中）
    /// </summary>
    internal const string AttributeName = "interpret";

    /// <summary>
    /// 全部合法排布（下标 0 = 恒等排布 <c>ABCD…</c>）的落位表，静态生成一次
    /// </summary>
    private static readonly ReadOnlyMemory<byte>[] Packings32 = Utils.MakePackings(2);

    /// <inheritdoc cref="Packings32"/>
    private static readonly ReadOnlyMemory<byte>[] Packings64 = Utils.MakePackings(4);

    /// <summary>
    /// 读取可选的 <c>interpret</c> 属性（来自 <see cref="TagDescriptor.Extras"/>）；未写或空白时返回 null。
    /// </summary>
    internal static string? TryGet(TagDescriptor descriptor) => TryGet(descriptor.Extras);
    /// <summary>
    /// 从属性字典里读取可选的 <c>interpret</c>；未写或空白时返回 null。
    /// </summary>
    internal static string? TryGet(IDictionary<string, XAttribute>? extras)
    {
        if (extras is null || !extras.TryGetValue(AttributeName, out var attr))
        {
            return null;
        }

        var value = attr.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// 解析并校验记法，返回"值里的第 i 个字节在设备端字节里的下标"这张落位表（<c>map[i]</c>）；
    /// <b>恒等排布（<c>ABCD…</c>）返回空表</b>（无需搬运）。<br/>
    /// 表用 <see cref="ReadOnlyMemory{T}"/> 表达（C# 没有"只读数组"，数组永远可以写元素），
    /// 取项就是 <c>map.Span[i]</c>：没有位移/掩码数学，也不限于 8 字节（将来支持更宽的格式不用改编码）。
    /// </summary>
    /// <param name="descriptor">测点描述符（提供 <c>endian</c> 与 <c>interpret</c>）</param>
    /// <param name="byteCount">该数值的字节数（4/8）</param>
    /// <param name="location">报错时的位置上下文（如 <c>Tag(名)</c>）</param>
    /// <exception cref="TagsProjectXmlException"><c>interpret</c> 的长度/字符/与 <c>endian</c> 的一致性不合法</exception>
    internal static ReadOnlyMemory<byte> Parse(TagDescriptor descriptor, int byteCount, string location)
    {
        var text = TryGet(descriptor);
        if (text is null)
        {
            if (descriptor.EndianKind == EndianKinds.BigEndian)
            {
                return default;
            }

            // LittleEndian 且未写 interpret：完全小端（DCBA / HGFEDCBA）
            var reversed = new byte[byteCount];
            for (var i = 0; i < byteCount; i++)
            {
                reversed[i] = (byte)(byteCount - 1 - i);
            }
            return Normalize(reversed);
        }

        if (text.Length != byteCount)
        {
            throw new TagsProjectXmlException(
                $"interpret='{text}' 的字符数({text.Length})与测点字节数({byteCount})不一致：" +
                "每个字符代表一个字节，左侧为最高字节（A = 最高字节）",
                location);
        }

        var seen = new bool[byteCount];
        var deviceIndexOfValueByte = new byte[byteCount];
        for (var p = 0; p < byteCount; p++)
        {
            var letter = text[p];
            var valueByteIndex = letter - 'A';
            if (valueByteIndex < 0 || valueByteIndex >= byteCount)
            {
                throw new TagsProjectXmlException(
                    $"interpret='{text}' 含有非法字符 '{letter}'：{byteCount} 字节的数值只允许使用 {Letters(byteCount)}，且各出现一次",
                    location);
            }
            if (seen[valueByteIndex])
            {
                throw new TagsProjectXmlException(
                    $"interpret='{text}' 中字符 '{letter}' 重复：{byteCount} 字节的数值必须由 {Letters(byteCount)} 各出现一次组成",
                    location);
            }
            seen[valueByteIndex] = true;
            deviceIndexOfValueByte[valueByteIndex] = (byte)p;
        }

        // 每两个连续字符必须是相邻字节，且先后与 endian 一致
        var expected = descriptor.EndianKind == EndianKinds.BigEndian ? "AB、CD、EF…" : "BA、DC、FE…";
        for (var p = 0; p < byteCount; p += 2)
        {
            var leftIndex = text[p] - 'A';
            var rightIndex = text[p + 1] - 'A';
            var adjacent = Math.Abs(leftIndex - rightIndex) == 1;
            var evenFirst = leftIndex % 2 == 0;
            var endianMatched = descriptor.EndianKind == EndianKinds.BigEndian ? evenFirst : !evenFirst;
            if (!adjacent || !endianMatched)
            {
                throw new TagsProjectXmlException(
                    $"interpret='{text}' 与 endian='{descriptor.EndianKind}' 冲突：endian 描述每个 16 位单元内部两个字节的顺序，" +
                    $"此时每两个连续字符必须是 {expected}；寄存器之间的先后才由 interpret 决定" +
                    "（字交换写 endian=\"BigEndian\" interpret=\"CDAB\"，字节交换写 endian=\"LittleEndian\" interpret=\"BADC\"）",
                    location);
            }
        }

        return Normalize(deviceIndexOfValueByte);
    }

    /// <summary>
    /// 恒等排布一律归一成空表（= 不需要置换），这样"显式写 <c>ABCD…</c>"与"不写 <c>interpret</c>"在
    /// <see cref="Parse"/> 这一层就是同一件事：同一个编号、同一个实例、读路径走直通分支。
    /// </summary>
    private static ReadOnlyMemory<byte> Normalize(byte[] deviceIndexOfValueByte)
    {
        for (var i = 0; i < deviceIndexOfValueByte.Length; i++)
        {
            if (deviceIndexOfValueByte[i] != i)
            {
                return deviceIndexOfValueByte;
            }
        }
        return default;
    }

    /// <summary>
    /// 该描述符的排布在"全部合法排布"里的编号（下标 0 = 恒等排布 <c>ABCD…</c>）；记法不合法则先抛。<br/>
    /// 编号与 <see cref="EnumeratePackings"/> 同序，供解读器按编号复用实例。查找直接比对落位表，
    /// 不经过字符串（<see cref="Parse"/> 的结果必然与枚举里的某一项逐个字节相同）。
    /// </summary>
    /// <param name="descriptor">测点描述符</param>
    /// <param name="byteCount">该数值的字节数（4/8）</param>
    /// <param name="location">报错时的位置上下文</param>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static int VariantIndex(TagDescriptor descriptor, int byteCount, string location)
    {
        var deviceIndexOfValueByte = Parse(descriptor, byteCount, location);
        var packings = EnumeratePackings(byteCount);
        for (var i = 0; i < packings.Length; i++)
        {
            if (AreSame(packings[i], deviceIndexOfValueByte))
            {
                return i;
            }
        }

        // 走不到这里：Parse 认可的结果必然在枚举里（枚举就是按同一套规则生成的）
        throw new TagsProjectConfigurationException(
            $"interpret 记法不在 {byteCount} 字节支持的排布集合内",
            location);
    }

    /// <summary>
    /// 两张落位表是否等价（都是"恒等用空表表示"的形态）
    /// </summary>
    private static bool AreSame(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
    {
        if (left.IsEmpty || right.IsEmpty)
        {
            return left.IsEmpty && right.IsEmpty;
        }
        return left.Span.SequenceEqual(right.Span);
    }

    /// <summary>
    /// 全部合法排布的落位表（下标 0 = 恒等排布，用空表表示），静态生成一次、之后直接复用。<br/>
    /// 生成规则与 <see cref="Parse"/> 的校验规则一致：<c>endian</c> 两种 × 16 位单元全排列
    /// （32 位 4 种、64 位 48 种），所以"合法的记法"与"枚举里的某一项"一一对应。
    /// </summary>
    internal static ReadOnlyMemory<byte>[] EnumeratePackings(int byteCount) => byteCount switch
    {
        4 => Packings32,
        8 => Packings64,
        _ => throw new TagsProjectConfigurationException($"Modbus 多寄存器数值只支持 4/8 字节，当前为 {byteCount}"),
    };


    /// <summary>
    /// 落位表 → 可选记法（设备端第 p 个位置上是值里的第几个字节，就写第几个字母）。<br/>
    /// 只用于测试与文档对照：它展示的是"这组字节摆布写成 XML 该怎么写"。生产路径不做这个转换——
    /// <see cref="VariantIndex"/> 直接比对落位表，<see cref="Parse"/> 也不靠字符串查找。
    /// 这里<b>不做合法性校验</b>，恒等排布要显式传空表。
    /// </summary>
    internal static string DescribeNotation(ReadOnlyMemory<byte> deviceIndexOfValueByte, int byteCount)
    {
        var letters = new char[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            var deviceIndex = deviceIndexOfValueByte.IsEmpty ? i : deviceIndexOfValueByte.Span[i];
            letters[deviceIndex] = (char)('A' + i);
        }
        return new string(letters);
    }

    /// <summary>
    /// 对只占一个 16 位单元（16 位数值、BIT、BYTE）或没有寄存器空间概念的测点，<c>interpret</c> 无意义：
    /// 只有一对字节，写 <c>endian</c> 即可，两处表达同一件事只会带来歧义，因此直接拒绝。
    /// </summary>
    /// <param name="descriptor">测点描述符</param>
    /// <param name="what">测点种类描述（用于报错）</param>
    /// <param name="location">位置上下文</param>
    /// <exception cref="TagsProjectConfigurationException">写了 <c>interpret</c></exception>
    internal static void Reject(TagDescriptor descriptor, string what, string location)
    {
        var text = TryGet(descriptor);
        if (text is not null)
        {
            throw new TagsProjectConfigurationException(
                $"{what} 只占一个 16 位单元，只有一对字节，无法使用 interpret='{text}'——请改用 endian" +
                "（endian 描述这一个寄存器内部两个字节的顺序：BigEndian 高位在前，LittleEndian 低位在前）",
                location);
        }
    }

    /// <summary>
    /// 组合（<c>TagCbnt</c>）本身没有数值，写 <c>interpret</c> 一定是误解（它应该写在具体的测点上）。
    /// </summary>
    /// <exception cref="TagsProjectConfigurationException">写了 <c>interpret</c></exception>
    internal static void RejectOnCbnt(IDictionary<string, XAttribute>? extras, string cbntName)
    {
        var text = TryGet(extras);
        if (text is not null)
        {
            throw new TagsProjectConfigurationException(
                $"组合(TagCbnt 名称={cbntName}) 本身没有数值，不能在它上面写 interpret='{text}'——" +
                "interpret 描述某个多寄存器数值的字节排布，请写到子测点上",
                $"TagCbnt({cbntName})");
        }
    }

    /// <summary>
    /// 对单 16 位单元的种类做 <see cref="Reject"/>；多寄存器种类（32/64 位）由测点自身的解读器校验记法。
    /// </summary>
    internal static void RejectForSingleUnit(TagDescriptor descriptor, string location)
    {
        if (IsSingleUnit(descriptor.TagKind))
        {
            Reject(descriptor, $"测点种类 {descriptor.TagKind}", location);
        }
    }

    /// <summary>
    /// 单 16 位单元的测点种类（这些种类只有一对字节，不支持 <c>interpret</c>）
    /// </summary>
    /// <param name="kind">测点种类（Core 里 <c>TagKinds</c> 是 <c>string</c> 的别名）</param>
    private static bool IsSingleUnit(string kind) =>
        kind == BuiltinTagKinds.BIT
        || kind == BuiltinTagKinds.BYTE
        || kind == BuiltinTagKinds.INT16
        || kind == BuiltinTagKinds.UINT16
        || kind == BuiltinTagKinds.DI
        || kind == BuiltinTagKinds.DO
        || kind == BuiltinTagKinds.Unknown;

    /// <summary>
    /// 记法允许的字符集（<c>4</c> 字节时为 <c>A/B/C/D</c>）
    /// </summary>
    private static string Letters(int byteCount)
    {
        var builder = new StringBuilder(byteCount + byteCount - 1);
        for (var i = 0; i < byteCount; i++)
        {
            if (i > 0)
            {
                builder.Append('/');
            }
            builder.Append((char)('A' + i));
        }
        return builder.ToString();
    }
}
