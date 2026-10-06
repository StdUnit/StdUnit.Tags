using Itminus.FSharpExtensions;
using Microsoft.FSharp.Core;
using System.Text.RegularExpressions;

namespace StdUnit.Tags.S7;

/// <summary>
/// 地址类型
/// </summary>
public enum AreaKinds
{
    /// <summary>
    /// 空
    /// </summary>
    None,
    /// <summary>
    /// MB
    /// </summary>
    MB,
    /// <summary>
    /// DB
    /// </summary>
    DB,
}

/// <summary>
/// S7 地址
/// </summary>
public struct S7Address
{
    /// <summary>
    /// c'tor
    /// </summary>
    public S7Address()
    {
    }

    /// <summary>
    /// 地址类型
    /// </summary>
    public AreaKinds Area = AreaKinds.DB;

    /// <summary>
    /// Block号，只有AreaKinds.DB时才有意义
    /// </summary>
    public int BlockNumber = 0;

    /// <summary>
    /// Block已经指定
    /// </summary>
    public bool BlockSpecified = true;

    /// <summary>
    /// 起始地址
    /// </summary>
    public int StartAddress = 0;

    /// <summary>
    /// 是否使用位寻址
    /// </summary>
    public bool UseBit = false;

    /// <summary>
    /// 第nth位，取值范围是 0~15
    /// </summary>
    public byte NthBit = 0;

    /// 转成 TagAddress 字符串
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Area 不是已知的区域类型</exception>
    public override string ToString()
    {
        var str = this.Area switch
        {
            AreaKinds.MB => $"MB.{StartAddress}",
            AreaKinds.DB => $"DB{BlockNumber}.{StartAddress}",
            AreaKinds.None => $"$${StartAddress}",
            _ => throw new ArgumentOutOfRangeException(nameof(this.Area), this.Area, "未预料的S7 Area类型")
        };
        if (!UseBit)
        {
            return str;
        }
        return $"{str}.{NthBit}";
    }

    /// <summary>
    /// 展示格式化的地址字符串，
    /// 格式化结果与输入的地址字符串格式相同，例如：
    /// 如果输入的地址是"$$104.3"，则格式化结果也是"$$104.3";
    /// 如果输入的地址是"DB200.100"，则格式化结果也是"DB200.100"
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Area 不是已知的区域类型</exception>
    public string Format()
    {
        var str = (this.BlockSpecified, this.Area) switch
        {
            (false, _) => $"$${StartAddress}",
            (true, AreaKinds.MB) => $"MB.{StartAddress}",
            (true, AreaKinds.DB) => $"DB{BlockNumber}.{StartAddress}",
            _ => throw new ArgumentOutOfRangeException(nameof(this.Area), this.Area, "未预料的S7 Area类型")
        };
        if (!UseBit)
        {
            return str;
        }
        return $"{str}.{NthBit}";
    }
}

/// <summary>
/// S7 地址解析器
/// </summary>
public static class S7AddressParser
{
    /// <summary>
    /// 把地址字符串解析成 <see cref="S7Address"/>
    /// </summary>
    /// <param name="addr"></param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址字符串不是合法的 S7 地址</exception>
    public static S7Address Parse(string addr)
    {
        var addrspan = addr.AsSpan();
        if (addrspan.Length < 2)
        {
            throw new TagsProjectAddressException(
                $"S7地址 '{addr}' 格式错误：长度不足2（期望 DB<block>.<start>[.<bit>] / MB.<start>[.<bit>] / $$<start>[.<bit>]）");
        }

        // $$开头表示引用TagCbnt的AreaKind和BlockNumber，地址字符串中不包含AreaKind和BlockNumber信息
        if (addrspan[0] == '$' && addrspan[1] == '$')
        {
            return ParseRelativeAddress(addr, addrspan.Slice(2));
        }

        // MB. 开头表示MB区地址，地址字符串中不包含AreaKind和BlockNumber信息
        if (addrspan.Length >= 3 && addrspan[0] == 'M' && addrspan[1] == 'B' && addrspan[2] == '.')
        {
            return ParseMBAddress(addr, addrspan.Slice(3));
        }

        // DB开头表示DB区地址，地址字符串中包含AreaKind和BlockNumber信息
        if (addrspan[0] == 'D' && addrspan[1] == 'B')
        {
            var q = ParseDBAddressWithNthBit(addr).OrElse(_ => ParseDBAddressWithoutNthBit(addr));
            if (q.IsError)
            {
                throw new TagsProjectAddressException(
                    $"非法的S7 DB地址 '{addr}'：{q.ErrorValue}（期望 DB<block>.<start>[.<bit>]）");
            }
            return q.ResultValue;
        }

        throw new TagsProjectAddressException(
            $"非法的S7地址 '{addr}'（期望 DB<block>.<start>[.<bit>] / MB.<start>[.<bit>] / $$<start>[.<bit>]）");
    }

    private static FSharpResult<S7Address, string> ParseDBAddressWithNthBit(string addr)
    {
        var regex = new Regex(@"DB(?<db>[0-9]+)\.(?<start>[0-9]+)\.(?<nth>[0-9]+)$");
        var match = regex.Match(addr);
        if (!match.Success)
        {
            return $"未能匹配模式 DB<db>.<start>.<nth>的模式".ToErrResult<S7Address, string>();
        }
        else
        {
            var dbStr = match.Groups["db"].Value;
            var startStr = match.Groups["start"].Value;
            var nthStr = match.Groups["nth"].Value;

            if (!int.TryParse(dbStr, out var db))
            {
                return $"无法解析db号".ToErrResult<S7Address, string>();
            }

            if (!int.TryParse(startStr, out var start))
            {
                return $"无法解析起始地址".ToErrResult<S7Address, string>();
            }

            if (!byte.TryParse(nthStr, out var nth))
            {
                return $"无法解析起始位地址".ToErrResult<S7Address, string>();
            }

            var s7Address = new S7Address
            {
                Area = AreaKinds.DB,
                BlockNumber = db,
                StartAddress = start,
                UseBit = true,
                NthBit = nth,
            };
            return s7Address.ToOkResult<S7Address, string>();
        }
    }

    private static FSharpResult<S7Address, string> ParseDBAddressWithoutNthBit(string addr)
    {
        var regex = new Regex(@"DB(?<db>[0-9]+)\.(?<start>[0-9]+)$");
        var match = regex.Match(addr);
        if (!match.Success)
        {
            return $"未能匹配模式 DB<db>.<start>的模式".ToErrResult<S7Address, string>();
        }
        var dbStr = match.Groups["db"].Value;
        var startStr = match.Groups["start"].Value;

        if (!int.TryParse(dbStr, out var db))
        {
            return $"无法解析db号".ToErrResult<S7Address, string>();
        }

        if (!int.TryParse(startStr, out var start))
        {
            return $"无法解析起始地址".ToErrResult<S7Address, string>();
        }
        var s7Address = new S7Address
        {
            Area = AreaKinds.DB,
            BlockNumber = db,
            StartAddress = start,
            UseBit = false,
            NthBit = 0,
        };
        return s7Address.ToOkResult<S7Address, string>();
    }

    /// <summary>
    /// 解析整数。<br/>
    /// net472 没有 <c>int.TryParse(ReadOnlySpan&lt;char&gt;)</c>（.NET Core 2.1+ 才加入），
    /// 这里集中处理该差异，避免每个调用点铺 #if。
    /// </summary>
    private static bool TryParseInt(ReadOnlySpan<char> span, out int value)
    {
#if NETFRAMEWORK
        return int.TryParse(span.ToString(), out value);
#else
        return int.TryParse(span, out value);
#endif
    }

    /// <summary>
    /// 解析字节，net472 差异同 <see cref="TryParseInt"/>。
    /// </summary>
    private static bool TryParseByte(ReadOnlySpan<char> span, out byte value)
    {
#if NETFRAMEWORK
        return byte.TryParse(span.ToString(), out value);
#else
        return byte.TryParse(span, out value);
#endif
    }

    /// <summary>
    /// 解析MB地址，输入类似于"2000.1"
    /// </summary>
    /// <param name="originalAddr">原始地址字符串，仅用于错误消息</param>
    /// <param name="span">去掉 "MB." 前缀后的地址片段</param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址片段不是合法的 MB 地址</exception>
    private static S7Address ParseMBAddress(string originalAddr, ReadOnlySpan<char> span)
    {
        var index = span.IndexOf('.');
        var useBit = index > 0;
        if (useBit)
        {
            // 必须排除 '.' 本身（Slice 的结束索引是开区间），否则 "2000.1" 会被切成 "2000."，
            // int.TryParse 必然失败——即 MB 的位寻址地址永远解析不了。
            var startSpan = span.Slice(0, index);
            if (!TryParseInt(startSpan, out var start))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把起始地址 '{startSpan.ToString()}' 解析成整数");
            }

            if (!TryParseByte(span.Slice(index + 1), out var nthBit))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把位地址 '{span.Slice(index + 1).ToString()}' 解析成整数");
            }
            return new S7Address()
            {
                Area = AreaKinds.MB,
                BlockNumber = 0,
                StartAddress = start,
                UseBit = true,
                NthBit = nthBit,
            };

        }
        else
        {
            if (!TryParseInt(span, out var start))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把起始地址 '{span.ToString()}' 解析成整数");
            }
            return new S7Address()
            {
                Area = AreaKinds.MB,
                BlockNumber = 0,
                StartAddress = start,
                UseBit = false,
                NthBit = 0,
            };
        }

    }

    /// <summary>
    /// 解析相对地址（<c>$$</c> 前缀），输入类似于"104.3"。
    /// </summary>
    /// <param name="originalAddr">原始地址字符串，仅用于错误消息</param>
    /// <param name="span">去掉 "$$" 前缀后的地址片段</param>
    /// <returns></returns>
    /// <exception cref="TagsProjectAddressException">地址片段不是合法的相对地址</exception>
    private static S7Address ParseRelativeAddress(string originalAddr, ReadOnlySpan<char> span)
    {
        var index = span.IndexOf('.');
        var useBit = index > 0;
        if (useBit)
        {
            var startSpan = span.Slice(0, index);
            if (!TryParseInt(startSpan, out var start))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把起始地址 '{startSpan.ToString()}' 解析成整数");
            }

            if (!TryParseByte(span.Slice(index + 1), out var nthBit))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把位地址 '{span.Slice(index + 1).ToString()}' 解析成整数");
            }
            return new S7Address()
            {
                Area = AreaKinds.None,
                BlockNumber = 0,
                BlockSpecified = false,
                StartAddress = start,
                UseBit = true,
                NthBit = nthBit,
            };

        }
        else
        {
            if (!TryParseInt(span, out var start))
            {
                throw new TagsProjectAddressException(
                    $"S7地址 '{originalAddr}' 不合法：无法把起始地址 '{span.ToString()}' 解析成整数");
            }
            return new S7Address()
            {
                Area = AreaKinds.None,
                BlockNumber = 0,
                BlockSpecified = false,
                StartAddress = start,
                UseBit = false,
                NthBit = 0,
            };
        }

    }


}
