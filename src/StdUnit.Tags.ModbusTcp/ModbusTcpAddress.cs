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
    /// 离散输出，参考号 00001~09999，对应S7-200Smart的Q点
    /// </summary>
    OutputCoils = 0,

    /// <summary>
    /// 离散输入，参考号 10001~19999，对应S7-200Smart的I点
    /// </summary>
    InputContacts = 1,

    /// <summary>
    /// 模拟输入，参考号 30001~39999，对应S7-200Smart的AIW点
    /// </summary>
    InputRegisters = 3,

    /// <summary>
    /// 保持寄存器，参考号 40001~49999，对应S7-200Smart的V点
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
    /// 参考号（reference number）对应的 <b>0 起算</b>协议地址，也就是 NModbus 各读写方法里的 <c>startAddress</c>。<br/>
    /// XML 里写的是 <b>参考号</b>——1 起算的 5 位写法（<c>40001</c> = 保持寄存器的第 1 个点），解析时统一减 1 得到本字段；
    /// 即 <c>StartPoint = 参考号 - 1</c>。全库只用这两个词：<b>参考号</b>（用户写的那个数）与
    /// <b>StartPoint</b>（协议地址），不再使用"点号 / 首地址"这类含糊说法。
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
            // 线圈的参考号是 0xxxx（00001 即 1 号线圈），基址 1 相加后仍需按 5 位补零，
            // 否则 00020 会被解析成 1 号区域的 20 号点
            var addr = OUTPUT_COILS_BASE + StartPoint;
            return $"{SlaveAddress}~{addr:d5}";
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
        if (address is null)
        {
            throw new TagsProjectAddressException("非法的Modbus地址：地址不能为 null");
        }

        // 两种写法互斥（带位号的一定有 '.'），所以按形状直接选解析器：
        // 若先试不匹配的那个形状，拿到的只会是"未能匹配模式"，把真正的原因（从站号/参考号/位号越界）盖掉。
        var q = address.Contains('.')
            ? ParseWithNthBit(address)
            : ParseWithoutNthBit(address);
        if (q.IsError)
        {
            throw new TagsProjectAddressException(
                $"非法的Modbus地址 '{address}'：{q.ErrorValue}（期望 [<slave>~]<area><start>[.<nth>]，area 取 0/1/3/4、start（参考号）从 1 开始，如 '1~40001.0'）");
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
            return $"从站号(Slave)必须是 0~255 的整数(={slavestr.Value})".ToErrResult<ModbusTcpAddress, string>();
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
            return $"参考号必须是 1~65535 的整数(={startstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        if (start == 0)
        {
            // 参考号从 1 开始，x0000 没有对应的点；若继续 -1 会回绕成 65535，静默指向一个不存在的点
            return $"参考号必须 >= 1(={startstr.Value})：Modbus 参考号从 1 开始，x0000 没有对应的点"
                .ToErrResult<ModbusTcpAddress, string>();
        }
        start -= 1;

        var nthstr = match.Groups["nth"];
        if (!byte.TryParse(nthstr.Value, out var nth))
        {
            return $"NthBit(位号)必须是 0~15 的整数(={nthstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        if (nth > 15)
        {
            return $"NthBit(位号)必须是 0~15 的整数(={nth})".ToErrResult<ModbusTcpAddress, string>();
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
            return $"从站号(Slave)必须是 0~255 的整数(={slavestr.Value})".ToErrResult<ModbusTcpAddress, string>();
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
            return $"参考号必须是 1~65535 的整数(={startstr.Value})".ToErrResult<ModbusTcpAddress, string>();
        }
        if (start == 0)
        {
            // 参考号从 1 开始，x0000 没有对应的点；若继续 -1 会回绕成 65535，静默指向一个不存在的点
            return $"参考号必须 >= 1(={startstr.Value})：Modbus 参考号从 1 开始，x0000 没有对应的点"
                .ToErrResult<ModbusTcpAddress, string>();
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