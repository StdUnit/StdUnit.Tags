using StdUnit.Tags;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Plugins;

internal class Handle1Snap : LogicetBase
{
    private ITagCbnt _cbnt1;
    private ITagCbnt _cbnt2;

    private ITagCbntor _outLedReset;
    private ITagCbntor _inBtnReset;
    private ITagCbntor _outLedRed;

    public Handle1Snap(IReadOnlyList<ITagChannel> channels, ITagGrp tags) : base(channels, tags)
    {
        this._cbnt1 = this.Tags.SelectCbnt("IoBox/输入");
        this._inBtnReset = _cbnt1.SelectTag("复位_执行键");

        this._cbnt2 = this.Tags.SelectCbnt("IoBox/输出");
        this._outLedReset = _cbnt2.SelectTag("复位_提示灯");
        this._outLedRed = _cbnt2.SelectTag("红灯");
    }

    public override int Order => 1;

    public override bool MatchEntry(ITagGrp entry)
    {
        return true;
    }

    public override Task ProcessAsync(ITagGrp entry, ITagChannel? channel)
    {
        var hasReq = this._inBtnReset.GetTagValue<bool>();
        var hasAck = _outLedRed.GetTagValue<bool>();

        if (hasReq && !hasAck)
        {
            Console.WriteLine($"[复位]: 收到请求，开始响应");
            _outLedRed.Value = true;
        }

        if (!hasReq && hasAck)
        {
            _outLedRed.Value = false;
            Console.WriteLine($"[复位]: 清除信号");
        }

        return Task.CompletedTask;
    }
}
