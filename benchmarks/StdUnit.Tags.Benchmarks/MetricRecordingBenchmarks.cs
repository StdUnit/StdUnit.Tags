using System.Collections.Generic;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;

namespace StdUnit.Tags.Benchmarks;

/// <summary>
/// 指标记录本身的开销——验证 README 里那句"没人订阅时零分配、纳秒级"。<br/>
/// 两个参数只差一件事：是否有人 <see cref="MeterListener"/> 订阅。若两者差距显著，
/// 就说明"打点"在热路径上是有代价的，设计需要重新评估。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class MetricRecordingBenchmarks
{
    /// <summary>true = 挂一个 MeterListener（真实采集）；false = 没人订阅（默认部署形态）。</summary>
    [Params(false, true)]
    public bool WithListener { get; set; }

    private KeyValuePair<string, object?>[] _tags = null!;
    private MeterListener? _listener;
    private readonly System.TimeSpan _elapsed = System.TimeSpan.FromMilliseconds(1);

    [GlobalSetup]
    public void Setup()
    {
        _tags = new[] { new KeyValuePair<string, object?>(TagsMetricContract.EntryTag, "TagGrp(bench)") };

        if (WithListener)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == TagsMetricContract.MeterName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.Start();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _listener?.Dispose();

    [Benchmark]
    public void RecordTurn() => TagsMetric.RecordTurn(_tags, _elapsed);
}
