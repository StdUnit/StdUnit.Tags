using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 32 位无符号（<c>UINT32</c>）解读器
/// </summary>
internal sealed class ModbusUInt32Interpreter : ModbusValueInterpreter<uint>
{
    private static readonly ModbusValueInterpreter<uint>[] Instances =
        BuildInstances(4, static packed => new ModbusUInt32Interpreter(packed));

    private ModbusUInt32Interpreter(ReadOnlyMemory<byte> deviceIndexOfValueByte) : base(4, deviceIndexOfValueByte)
    {
    }

    /// <summary>
    /// 取该测点该用的解读器（<b>会做加载期校验</b>）
    /// </summary>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static ModbusValueInterpreter<uint> For(TagDescriptor descriptor) => Instances[VariantIndexOf(descriptor, 4)];

    /// <inheritdoc/>
    protected override uint FromValueBytes(ReadOnlySpan<byte> valueBytes) => BinaryPrimitives.ReadUInt32BigEndian(valueBytes);

    /// <inheritdoc/>
    protected override void ToValueBytes(uint value, Span<byte> valueBytes) => BinaryPrimitives.WriteUInt32BigEndian(valueBytes, value);
}
