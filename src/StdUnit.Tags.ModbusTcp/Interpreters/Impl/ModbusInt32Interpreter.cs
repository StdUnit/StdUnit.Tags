using System.Buffers.Binary;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 32 位有符号（<c>INT32</c>）解读器
/// </summary>
internal sealed class ModbusInt32Interpreter : ModbusValueInterpreter<int>
{
    private static readonly ModbusValueInterpreter<int>[] Instances =
        BuildInstances(4, static packed => new ModbusInt32Interpreter(packed));

    private ModbusInt32Interpreter(ReadOnlyMemory<byte> deviceIndexOfValueByte) : base(4, deviceIndexOfValueByte)
    {
    }

    /// <summary>
    /// 取该测点该用的解读器（<b>会做加载期校验</b>）
    /// </summary>
    /// <exception cref="TagsProjectXmlException">记法本身不合法</exception>
    internal static ModbusValueInterpreter<int> For(TagDescriptor descriptor) => Instances[VariantIndexOf(descriptor, 4)];

    /// <inheritdoc/>
    protected override int FromValueBytes(ReadOnlySpan<byte> valueBytes) => BinaryPrimitives.ReadInt32BigEndian(valueBytes);

    /// <inheritdoc/>
    protected override void ToValueBytes(int value, Span<byte> valueBytes) => BinaryPrimitives.WriteInt32BigEndian(valueBytes, value);
}
