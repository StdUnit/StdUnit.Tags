using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.Web;

internal class HandleSnap14 : LogicetBase
{
    private ITagCbnt _cbnt1;
    private ITagCbntor _reqFlag;

    public HandleSnap14(IReadOnlyList<ITagChannel> channels, ITagGrp tags) : base(channels, tags)
    {
        this._cbnt1 = this.Tags.SelectCbnt("拧紧枪/1#/拧紧请求");
        this._reqFlag = _cbnt1.SelectTag("请求标志");
    }

    public override int Order => 1;


    public override bool MatchEntry(ITagGrp entry)
    {
        return entry.TagName() == "拧紧枪";
    }


    public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)
    {
        _reqFlag.Value = true;
        return Task.CompletedTask;
    }
}
