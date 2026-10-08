namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// <c>TagCbnt</c> 上 <c>slave</c> 属性的共享处理：把从站号合成进组合的起始地址。<br/>
/// 位空间（<see cref="ModbusBitTagCbntBuilder"/>）与寄存器空间（<see cref="ModbusRegisterTagCbntBuilder"/>）语义一致：
/// 组合读写的从站号优先取 <c>slave</c> 属性，其次是 <c>address</c> 里的 <c>&lt;slave&gt;~</c> 前缀，都没有时为 1。
/// </summary>
internal static class ModbusCbntSlaveAddress
{
    /// <summary>
    /// 用 <paramref name="slave"/> 覆盖起始地址里的从站号。<br/>
    /// 只重写 <c>~</c> 之前的从站号，<c>~</c> 之后的原文（含 <c>00020</c> 这类前导零）保持原样。
    /// </summary>
    internal static string WithSlave(string startAddress, byte slave)
    {
        var raw = startAddress is null ? string.Empty : startAddress.Trim();
        if (raw.Length == 0)
        {
            return raw;
        }
        var index = raw.IndexOf('~');
        var rest = index >= 0 ? raw.Substring(index + 1) : raw;
        return $"{slave}~{rest}";
    }

    /// <summary>
    /// 读取可选的 <c>slave</c> 属性。
    /// </summary>
    /// <param name="descriptor">组合描述符</param>
    /// <param name="slave">属性值；未写该属性时为 1</param>
    /// <returns>写了 <c>slave</c> 属性时返回 true</returns>
    /// <exception cref="TagsProjectXmlException">属性值不是 0~255 的整数</exception>
    internal static bool TryGetSlave(TagCbntDescriptor descriptor, out byte slave)
    {
        slave = 1;
        if (!descriptor.Extras.TryGetValue("slave", out var slaveAttr))
        {
            return false;
        }
        if (!byte.TryParse(slaveAttr.Value, out slave))
        {
            throw new TagsProjectXmlException(
                $"无效的Modbus从站地址：{slaveAttr.Value}（必须是 0~255 的整数）",
                $"TagCbnt({descriptor.Name})");
        }
        return true;
    }
}
