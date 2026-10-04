using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.Web;

internal class HandleSnap11 : LogicetBase
{
    private ITagCbnt _cbnt1;
    private ITagCbnt _cbnt2;
    private ITagCbntor _reqTag;
    private ITagCbntor _ackTag;


    public HandleSnap11(IReadOnlyList<ITagChannel> channels, ITagGrp tags) : base(channels, tags)
    {
        this._cbnt1 = this.Tags.SelectCbnt("g4/输入");
        this._reqTag = _cbnt1.SelectTag("心跳请求");

        this._cbnt2 = this.Tags.SelectCbnt("g4/输出");
        this._ackTag = _cbnt2.SelectTag("心跳响应");
    }

    public override int Order => 1;


    public override bool MatchEntry(ITagGrp entry)
    {
        return entry.TagName() == "g4";
    }

    public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)
    {
        var hasReq = this._reqTag.GetTagValue<bool>();
        var hasAck = _ackTag.GetTagValue<bool?>() == true;

        if (hasReq && !hasAck)
        {
            _ackTag.Value = true;
        }

        if (!hasReq && hasAck)
        {
            _ackTag.Value = false;
            Console.WriteLine($"清除拍照响应信号");
        }

        return Task.CompletedTask;
    }
}
