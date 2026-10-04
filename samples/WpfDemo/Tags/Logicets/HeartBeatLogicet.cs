using StdUnit.Tags;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfDemo.Tags.Logicets;

public class HeartBeatLogicet : LogicetBase
{
    private readonly ITag _heartReq;
    private readonly ITag _heartAck;
    private readonly ITag _interval;
    private readonly ILogger<HeartBeatLogicet> _logger;

    private Stopwatch _stopwatch = new Stopwatch();
    private bool _fst = true;
    private TimeSpan _duration = TimeSpan.Zero;

    public HeartBeatLogicet(IReadOnlyList<ITagChannel> channels, ITagGrp tags, ILogger<HeartBeatLogicet> logger) : base(channels, tags)
    {
        var grp = tags.SelectGrp("IoBox/通用状态");

        _heartReq = grp.SelectTag("PLC/心跳请求");
        _heartAck = grp.SelectTag("MST/心跳响应");
        _interval = grp.SelectTag("MST/扫描周期");
        this._logger = logger;
    }

    public override int Order => 2;

    public override bool MatchEntry(ITagGrp entry) => entry.TagName() == "IoBox";

    public override Task ProcessAsync(ITagGrp entry, ITagChannel? thisChannel)
    {
        _heartAck.Value = _heartReq.Value;


        // 统计周期
        if (_fst)
        {
            _stopwatch.Start();
            _fst = false;
        }
        else
        {
            _duration = _stopwatch.Elapsed;
            _interval.Value = (float)_duration.TotalMilliseconds;
            _stopwatch.Restart();
        }

        return Task.CompletedTask;
    }
}