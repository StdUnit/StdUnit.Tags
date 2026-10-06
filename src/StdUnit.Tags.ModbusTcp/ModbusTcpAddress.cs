using Itminus.FSharpExtensions;
using Microsoft.FSharp.Core;
using System.Text.RegularExpressions;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// 寄存器种类
/// </summary>
public enum RegisterKinds
{
    /// <summary>
    /// 离散输出，00001~09999，对应S7-200Smart的Q点
    /// </summary>
    OutputCoils = 0,

    /// <summary>
    /// 离散输入，10001~19999，对应S7-200Smart的I点
    /// </summary>
    InputContacts = 1,

    /// <summary>
    /// 模拟输入，30001~39999，对应S7-200Smart的AIW点
    /// </summary>
    InputRegisters = 3,

    /// <summary>
    /// 保持寄存器，40001~49999，对应S7-200Smart的V点
    /// </summary>
    HoldingRegisters = 4,
}


/// <summary>
/// 42801 - 40001
/// </summary>
public struct ModbusTcpAddress
{

    /// <summary>
    /// 离散输入寄存器，RO
    /// </summary>
    public const ushort INPUT_CONTACTS_BASE = 10001;

    /// <summary>
    /// 模拟量寄存器，RO
    /// </summary>
    public const ushort INPUT_REGISTERS_BASE = 30001;


    /// <summary>
    /// 线圈输出，R/W
    /// </summary>
    public const ushort OUTPUT_COILS_BASE = 00001;

    /// <summary>
    /// 保持寄存器，R/W
    /// </summary>
    public const ushort HOLDING_REGISTERS_BASE = 40001;


    /// <summary>
    /// c'tor
    /// </summary>
    public ModbusTcpAddress()
    {
    }

    /// <summary>
    /// 从站地址
    /// </summary>
    public byte SlaveAddress = 1;

    /// <summary>
    /// 寄存器区域，默认是HoldingRegister
    /// </summary>
    public RegisterKinds Area = RegisterKinds.HoldingRegisters;

    /// <summary>
    /// Modbus中的首地址（点号）
    /// </summary>
    public ushort StartPoint = 0;

    /// <summary>
    /// 是否使用位地址
    /// </summary>
    public bool UseBit = false;

    /// <summary>
    /// 位地址，0-16
    /// </summary>
    public byte NthBit = 0;


    /// <inheritdoc/>
    public override string ToString()
    {
        if (Area == RegisterKinds.HoldingRegisters)
        {
            var addr = HOLDING_REGISTERS_BASE + StartPoint;
            if (!UseBit)
                return $"{SlaveAddress}~{addr}";

            return $"{SlaveAddress}~{addr}.{NthBit}";
        }

        // 离散输出：一定不会使用Bit位
        if (Area == RegisterKinds.OutputCoils)
        {
            var addr = OUTPUT_COILS_BASE + StartPoint;
            return $"{SlaveAddress}~{addr}";
        }

        // 离散输入：一定不会使用Bit位
        if (Area == RegisterKinds.InputContacts)
        {
            var addr = INPUT_CONTACTS_BASE + StartPoint;
            return $"{SlaveAddress}~{addr}";
        }

        if (Area == RegisterKinds.InputRegisters)
        {
            var addr = INPUT_REGISTERS_BASE + StartPoint;
            if (!UseBit)
                return $"{SlaveAddress}~{addr}";

            return $"{SlaveAddress}~{addr}.{NthBit}";
        }

        throw new TagsProjectAddressException(
            $"ModbusTcp 地址无法格式化：未预料到的地址区域 Area={Area}（Slave={SlaveAddress}, StartPoint={StartPoint}, UseBit={UseBit}, NthBit={NthBit}）");
    }
}

/// <summary>
/// ModbusTcp地址解析器
/// </summary>
public static class ModBusTcpAddressParser
{
    static readonly Regex RegexPattern_WithNthBit = new Regex(@"^((?<slave>[0-9]{1,})~)?(?<area>[0134])(?<start>[0-9]{1,5})\.(?<nth>[0-9]+)$");
    static readonly Regex RegexPattern_WithoutNthBit = new Regex(@"^((?<slave>[0-9]{1,})~)?(?<area>[0134])(?<start>[0-9]{1,5})$");


    /// <summary>
    /// 解析Modbus地址
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址字符串不是合法的 Modbus 地址</exception>
    public static ModbusTcpAddress Parse(string address)
    {
        var q = ParseWithNthBit(address).OrElse(_ => ParseWithoutNthBit(address));
        if (q.IsError)
        {
            throw new TagsProjectAddressException(
                $"非法的Modbus地址 '{address}'：{q.ErrorValue}（期望 [<slave>~]<area><start>[.<nth>]，area 取 0/1/3/4，如 '1~40001.0'）");
        }
        return q.ResultValue;
    }

    /// <summary>
    /// 解析带位地址的Modbus地址。<br/>
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    internal static FSharpResult<ModbusTcpAddress, string> ParseWithNthBit(string address)
    {
        var match = RegexPattern_WithNthBit.Match(address);
        if (!match.Success)
        {
            return $"未能匹配模式 <area><start>.<nth>的模式".ToErrResult<ModbusTcpAddress, string>();
        }

        byte slave = 1;
        var slavestr = match.Groups["slave"];
        if (!string.IsNullOrEmpty(slavestr.Value) && !byte.TryParse(slavestr.Value, out slave))
        {
            return $"Slave非整数(={slavestr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }

        var areastr = match.Groups["area"];
        if (!byte.TryParse(areastr.Value, out var area))
        {
            return $"区域非整数(={areastr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }

        if (area != 0 && area != 1 && area != 3 && area != 4)
        {
            return $"区域非法(={area})".ToErrResult<ModbusTcpAddress, string>();
        }


        var startstr = match.Groups["start"];
        if (!ushort.TryParse(startstr.Value, out var start))
        {
            return $"StartPoint非整数(={startstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        start -= 1;

        var nthstr = match.Groups["nth"];
        if (!byte.TryParse(nthstr.Value, out var nth))
        {
            return $"NthBit非整数(={nthstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        if (nth < 0 || nth > 15)
        {
            return $"NthBit范围非法(={nth})".ToErrResult<ModbusTcpAddress, string>();
        }

        var ok = new ModbusTcpAddress
        {
            Area = (RegisterKinds)area,
            StartPoint = start,
            SlaveAddress = slave,
            UseBit = true,
            NthBit = nth,
        };
        return ok.ToOkResult<ModbusTcpAddress, string>();
    }

    /// <summary>
    /// 解析不带位地址的Modbus地址<br/>
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    internal static FSharpResult<ModbusTcpAddress, string> ParseWithoutNthBit(string address)
    {
        var match = RegexPattern_WithoutNthBit.Match(address);
        if (!match.Success)
        {
            return $"未能匹配模式 <area><start>的模式".ToErrResult<ModbusTcpAddress, string>();
        }

        byte slave = 1;
        var slavestr = match.Groups["slave"];
        if (!string.IsNullOrEmpty(slavestr.Value) && !byte.TryParse(slavestr.Value, out slave))
        {
            return $"Slave非整数(={slavestr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }

        var areastr = match.Groups["area"];
        if (!byte.TryParse(areastr.Value, out var area))
        {
            return $"区域非整数(={areastr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }

        if (area != 0 && area != 1 && area != 3 && area != 4)
        {
            return $"区域非法(={area})".ToErrResult<ModbusTcpAddress, string>();
        }


        var startstr = match.Groups["start"];
        if (!ushort.TryParse(startstr.Value, out var start))
        {
            return $"StartPoint非整数(={startstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        start -= 1;

        var ok = new ModbusTcpAddress
        {
            Area = (RegisterKinds)area,
            StartPoint = start,
            SlaveAddress = slave,
            UseBit = false,
            NthBit = 0,
        };
        return ok.ToOkResult<ModbusTcpAddress, string>();
    }

}