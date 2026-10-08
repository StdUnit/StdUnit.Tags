using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 32 位浮点（<c>FLOAT</c>）解读器：位模式与 <c>UINT32</c> 相同，只是最后按 IEEE754 重解释
/// （<c>BinaryPrimitives</c> 只有 .NET 5+ 才有 <c>ReadSingleBigEndian</c>，这里统一走位模式）。
/// </summary>
internal sealed class ModbusFloatInterpreter : ModbusValueInterpreter<float>
{
    private static readonly ModbusValueInterpreter<float>[] Instances =
        BuildInstances(4, static packed => new ModbusFloatInterpreter(packed));

    private ModbusFloatInterpreter(ReadOnlyMemory<byte> deviceIndexOfValueByte) : base(4, deviceIndexOfValueByte)
    {
    }

    /// <summary>
    /// 取该测点该用的解读器（<b>会做加载期校验</b>）
    /// </summary>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static ModbusValueInterpreter<float> For(TagDescriptor descriptor) => Instances[VariantIndexOf(descriptor, 4)];

    /// <inheritdoc/>
    protected override float FromValueBytes(ReadOnlySpan<byte> valueBytes) =>
        Compat.FloatBitsCompat.ToSingle(BinaryPrimitives.ReadUInt32BigEndian(valueBytes));

    /// <inheritdoc/>
    protected override void ToValueBytes(float value, Span<byte> valueBytes) =>
        BinaryPrimitives.WriteUInt32BigEndian(valueBytes, Compat.FloatBitsCompat.ToBits(value));
}
