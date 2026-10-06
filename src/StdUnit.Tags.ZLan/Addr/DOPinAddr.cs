namespace StdUnit.Tags.ZLan;

/// ZLan 数字量输出（DO）针脚地址。<br/>
/// 枚举值是该针脚在 Modbus 输出线圈区内的偏移（DO1 = 0x10）。
public enum DOPinAddr : ushort
{
    /// <summary>DO1 输出针脚</summary>
    DO1 = 0x10,
    /// <summary>DO2 输出针脚</summary>
    DO2 = 0x11,
    /// <summary>DO3 输出针脚</summary>
    DO3 = 0x12,
    /// <summary>DO4 输出针脚</summary>
    DO4 = 0x13,
    /// <summary>DO5 输出针脚</summary>
    DO5 = 0x14,
    /// <summary>DO6 输出针脚</summary>
    DO6 = 0x15,
    /// <summary>DO7 输出针脚</summary>
    DO7 = 0x16,
    /// <summary>DO8 输出针脚</summary>
    DO8 = 0x17,
}
