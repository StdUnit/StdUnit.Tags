namespace StdUnit.Tags.ZLan;

/// ZLan 数字量输入（DI）针脚地址。<br/>
/// 枚举值是该针脚在 Modbus 输入触点区内的偏移（DI1 = 0x00）。
public enum DIPinAddr : ushort
{
    /// <summary>DI1 输入针脚</summary>
    DI1 = 0x00,
    /// <summary>DI2 输入针脚</summary>
    DI2 = 0x01,
    /// <summary>DI3 输入针脚</summary>
    DI3 = 0x02,
    /// <summary>DI4 输入针脚</summary>
    DI4 = 0x03,
    /// <summary>DI5 输入针脚</summary>
    DI5 = 0x04,
    /// <summary>DI6 输入针脚</summary>
    DI6 = 0x05,
    /// <summary>DI7 输入针脚</summary>
    DI7 = 0x06,
    /// <summary>DI8 输入针脚</summary>
    DI8 = 0x07,
}


//public record ZLanModbusTcpDI(DIPinAddr PinAddr, string TagName);

//public record ZLanModbusTcpDO(DOPinAddr PinAddr, string TagName);
