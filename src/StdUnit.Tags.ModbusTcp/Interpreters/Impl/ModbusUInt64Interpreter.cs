using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 64 位无符号（<c>UINT64</c>）解读器
/// </summary>
internal sealed class ModbusUInt64Interpreter : ModbusValueInterpreter<ulong>
{
    private static readonly ModbusValueInterpreter<ulong>[] Instances =
        BuildInstances(8, static packed => new ModbusUInt64Interpreter(packed));

    private ModbusUInt64Interpreter(ReadOnlyMemory<byte> deviceIndexOfValueByte) : base(8, deviceIndexOfValueByte)
    {
    }

    /// <summary>
    /// 取该测点该用的解读器（<b>会做加载期校验</b>）
    /// </summary>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static ModbusValueInterpreter<ulong> For(TagDescriptor descriptor) => Instances[VariantIndexOf(descriptor, 8)];

    /// <inheritdoc/>
    protected override ulong FromValueBytes(ReadOnlySpan<byte> valueBytes) => BinaryPrimitives.ReadUInt64BigEndian(valueBytes);

    /// <inheritdoc/>
    protected override void ToValueBytes(ulong value, Span<byte> valueBytes) => BinaryPrimitives.WriteUInt64BigEndian(valueBytes, value);
}
