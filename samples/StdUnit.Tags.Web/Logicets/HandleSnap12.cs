using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.Web;

internal class HandleSnap12 : LogicetBase
{
    private ITagCbnt _cbnt1;
    private ITagCbnt _cbnt2;

    private ITag _btnLetGo;
    private ITag _btnManual;
    private ITag _ledGreen;
    private ITag _ledRed;
    private ITag _ledYellow;
    private ITag _ledLetGo;

    public HandleSnap12(IReadOnlyList<ITagChannel> channels, ITagGrp tags) : base(channels, tags)
    {
        this._cbnt1 = this.Tags.SelectCbnt("g2/输入");
        this._btnLetGo = this._cbnt1.SelectTag("放行按钮闭合状态");
        this._btnManual = this._cbnt1.SelectTag("手动");

        this._cbnt2 = this.Tags.SelectCbnt("g2/输出");
        this._ledGreen = this._cbnt2.SelectTag("绿灯");
        this._ledRed = this._cbnt2.SelectTag("红灯");
        this._ledYellow = this._cbnt2.SelectTag("黄灯");
        this._ledLetGo = this._cbnt2.SelectTag("放行灯");
    }

    public override int Order => 1;


    public override bool MatchEntry(ITagGrp entry)
    {
        return entry.TagName() == "g2";
    }

    private int i = 0;

    public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)
    {
        var letgo = this._btnLetGo.GetTagValue<bool>();
        var red = this._ledRed.GetTagValue<bool>();
        var green = this._ledGreen.GetTagValue<bool>();
        var yellow = this._ledYellow.GetTagValue<bool>();


        if (letgo)
        {
            Console.WriteLine($"{i++}: LetGo Btn has been pressed");

            if (!red && !yellow && !green)
            {
                this._ledGreen.Value = true;
            }

            if (red)
            {
                this._ledRed.Value = false;
                this._ledYellow.Value = true;
                this._ledGreen.Value = false;
            }
            if (yellow)
            {
                this._ledRed.Value = false;
                this._ledYellow.Value = false;
                this._ledGreen.Value = true;
            }
            if (green)
            {
                this._ledRed.Value = true;
                this._ledYellow.Value = false;
                this._ledGreen.Value = false;
            }
        }
        return Task.CompletedTask;
    }
}
