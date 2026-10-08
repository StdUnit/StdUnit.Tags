using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 64 位有符号（<c>INT64</c>）解读器
/// </summary>
internal sealed class ModbusInt64Interpreter : ModbusValueInterpreter<long>
{
    private static readonly ModbusValueInterpreter<long>[] Instances =
        BuildInstances(8, static packed => new ModbusInt64Interpreter(packed));

    private ModbusInt64Interpreter(ReadOnlyMemory<byte> deviceIndexOfValueByte) : base(8, deviceIndexOfValueByte)
    {
    }

    /// <summary>
    /// 取该测点该用的解读器（<b>会做加载期校验</b>）
    /// </summary>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static ModbusValueInterpreter<long> For(TagDescriptor descriptor) => Instances[VariantIndexOf(descriptor, 8)];

    /// <inheritdoc/>
    protected override long FromValueBytes(ReadOnlySpan<byte> valueBytes) => BinaryPrimitives.ReadInt64BigEndian(valueBytes);

    /// <inheritdoc/>
    protected override void ToValueBytes(long value, Span<byte> valueBytes) => BinaryPrimitives.WriteInt64BigEndian(valueBytes, value);
}
