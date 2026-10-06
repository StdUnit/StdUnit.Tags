using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace StdUnit.Tags.ComScanner.Channels;

/// <summary>
/// COM 通道描述符
/// </summary>
public class ComChannelDescriptor : TagChannelDescriptor
{
    /// <summary>
    /// c'tor
    /// </summary>
    public ComChannelDescriptor()
    {
        this.Driver = ComDriverNames.DriverName;
    }

    /// <summary>
    /// 通道选项
    /// </summary>
    public ComChannelOption Option { get; set; } = new ComChannelOption();

    /// <summary>
    /// 转换为 XElement
    /// </summary>
    /// <returns></returns>
    public override XElement ToXElement()
    {
        var ele = base.ToXElement();
        // 下面几处的判空之所以不用 string.IsNullOrEmpty：net472 引用程序集无可空标注，
        // 编译器看不到 [NotNullWhen(false)]，无法收缩类型（会报 CS8604）。
        var newLine = this.Option.NewLine;
        if (newLine is not null && newLine.Length > 0)
        {
            ele.SetOrAddChild(nameof(Option.NewLine), newLine);
        }
        if (!this.Option.ReadEntireLine)
        {
            ele.SetOrAddChild(nameof(Option.ReadEntireLine), this.Option.ReadEntireLine);
        }
        var readScript = this.Option.ReadScript;
        if (readScript is not null && readScript.Length > 0)
        {
            ele.SetOrAddChild(nameof(Option.ReadScript), readScript);
        }
        if (this.Option.ReadScriptDebugInformationEnabled)
        {
            ele.SetOrAddChild(nameof(Option.ReadScriptDebugInformationEnabled), this.Option.ReadScriptDebugInformationEnabled);
        }
        ele.SetOrAddChild(nameof(Option.Port), this.Option.Port);
        ele.SetOrAddChild(nameof(Option.BaudRate), this.Option.BaudRate);
        ele.SetOrAddChild(nameof(Option.Parity), this.Option.Parity);
        ele.SetOrAddChild(nameof(Option.DataBits), this.Option.DataBits);
        ele.SetOrAddChild(nameof(Option.StopBits), this.Option.StopBits);
        ele.SetOrAddChild(nameof(Option.ChannelCapacity), this.Option.ChannelCapacity);
        return ele;
    }
}

/// <summary>
/// extensions for conversions between <see cref="TagChannelDescriptor"/> and <see cref="ComChannelDescriptor"/>
/// </summary>
public static class TagChannelDescriptor_ComExtensions
{
    /// <summary>
    /// 转成 <see cref="ComChannelDescriptor"/>
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectConfigurationException">当前描述符的驱动不是 <see cref="ComDriverNames.DriverName"/></exception>
    /// <exception cref="TagsProjectXmlException">串口选项无法解析</exception>
    public static ComChannelDescriptor ToComChannelDescriptor(this TagChannelDescriptor descriptor)
    {
        if (descriptor.Driver != ComDriverNames.DriverName)
        {
            throw new TagsProjectConfigurationException(
                $"通道驱动错误：期望 {ComDriverNames.DriverName}，而当前为{descriptor.Driver}",
                $"Channel({descriptor.Name})");
        }
        if (descriptor is ComChannelDescriptor d)
        {
            return d;
        }

        int defaultBaudRate = 9600;
        Parity defaultParity = Parity.None;
        int defaultDataBits = 8;
        StopBits defaultStopBits = StopBits.None;
        int defaultChannelCapacity = 1;
        var location = $"Channel({descriptor.Name})";

        string? newline = null;
        if (descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.NewLine), out var newLine))
        {
            var raw = newLine.Value ?? string.Empty;
            // If XML contains literal escape sequences like "\\r\\n", unescape them to actual control chars
            if (raw.Contains("\\r") || raw.Contains("\\n") || raw.Contains("\\t"))
            {
                raw = Regex.Unescape(raw);
            }
            newline = raw;
        }

        var readEntireLine = (
                descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.ReadEntireLine), out var readEntireLineStr)
                && bool.TryParse(readEntireLineStr.Value, out var readEntireLineVal)
            ) ?
                readEntireLineVal :
                true;

        var readscript = !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.ReadScript), out var readScript) ?
                 null :
                 readScript.Value;
        var readScriptDebugInformationEnabled =
                 !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.ReadScriptDebugInformationEnabled), out var readScriptDebugInformationEnabledStr) ? false :
                 bool.TryParse(readScriptDebugInformationEnabledStr.Value, out var readScriptDebugInformationEnabledVal) ? readScriptDebugInformationEnabledVal :
                 throw new TagsProjectXmlException($"串口读取脚本调试信息开关非法，无法解析成布尔值({readScriptDebugInformationEnabledStr.Value})", location);

        var port = !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.Port), out var comPort) ?
                    "COM1" :
                    comPort.Value;
        // 向后兼容：0.10 及之前版本使用拼写错误的 `<BaundRate>` 元素名，0.11 起修正为 `BaudRate`。
        // 优先读新拼写，找不到时回退旧拼写——现场存量 XML 无需修改即可升级。
        var baudRate =
                    !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.BaudRate), out var baudRateStr) &&
                    !descriptor.Extras.TryGetValue("BaundRate", out baudRateStr) ? defaultBaudRate :
                    int.TryParse(baudRateStr.Value, out var baudRateVal) ? baudRateVal :
                    throw new TagsProjectXmlException($"串口波特率非法，无法解析成整数({baudRateStr.Value})", location);
        var parity = !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.Parity), out var parityStr) ? defaultParity :
                    Enum.TryParse<Parity>(parityStr.Value, out var parityVal) ? parityVal :
                    throw new TagsProjectXmlException($"串口极性非法，无法解析成Parity({parityStr.Value})", location);
        var databits =
                    !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.DataBits), out var databitsStr) ? defaultDataBits :
                    int.TryParse(databitsStr.Value, out var databitsVal) ? databitsVal :
                    throw new TagsProjectXmlException($"串口数据位非法，无法解析成整数({databitsStr.Value})", location);
        var stopbits = !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.StopBits), out var stopbitsStr) ? defaultStopBits :
                    Enum.TryParse<StopBits>(stopbitsStr.Value, out var stopbitsVal) ? stopbitsVal :
                    throw new TagsProjectXmlException($"串口停止位非法，无法解析成StopBits({stopbitsStr.Value})", location);

        var channelCapacity = !descriptor.Extras.TryGetValue(nameof(ComChannelDescriptor.Option.ChannelCapacity), out var channelCapacityStr) ? defaultChannelCapacity :
                   int.TryParse(channelCapacityStr.Value, out var channelCapacityVal) ? channelCapacityVal :
                    throw new TagsProjectXmlException($"通道容量非法，无法解析成正整数({channelCapacityStr.Value})", location);

        var res = new ComChannelDescriptor
        {
            Name = descriptor.Name,
            Driver = descriptor.Driver,
            Extras = descriptor.Extras,
            Option = new ComChannelOption
            {
                ReadEntireLine = readEntireLine,
                NewLine = newline,
                ReadScript = readscript,
                ReadScriptDebugInformationEnabled = readScriptDebugInformationEnabled,
                Port = port,
                BaudRate = baudRate,
                Parity = parity,
                DataBits = databits,
                StopBits = stopbits,
                ChannelCapacity = channelCapacity,
            },
        };
        return res;
    }
}