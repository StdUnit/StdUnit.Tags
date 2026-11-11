using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Xunit;

namespace StdUnit.Tags.Tests.Core.TagGrpRunners;

/// <summary>
/// <see cref="TagsMetricContract"/>（公开契约：名称/单位/tag 键）与 <see cref="TagsMetric"/>（实际注册与记录）的固化测试：
/// Meter 名、指标名、单位、tag 键一旦改名，使用方的 dashboard 会静默变空，所以用字面量钉住；
/// 同时验证这些常量确实被传给了 <c>Meter.CreateCounter/CreateHistogram</c>，而不只是两个字符串相等。
/// </summary>
public class MetricsContractTests
{
    private static bool HasEntryTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string expected)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == "entry" && Equals(tag.Value, expected))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void Meter_Name_IsStable()
    {
        Assert.Equal("StdUnit.Tags", TagsMetricContract.MeterName);
    }

    /// <summary>
    /// 把每个契约字符串钉在字面量上。<br/>
    /// 常量本身改起来毫无编译阻力，但使用方的 dashboard 会静默变空——这条测试就是那道阻力：
    /// 谁改了常量，这里先红；要放行就得连同 CHANGELOG / README 一起改（见 <see cref="TagsMetricContract"/> 的契约区注释）。
    /// </summary>
    [Fact]
    public void ContractStrings_ArePinned()
    {
        Assert.Equal("stdunit.tags.turns", TagsMetricContract.Turns);
        Assert.Equal("{turn}", TagsMetricContract.TurnsUnit);

        Assert.Equal("stdunit.tags.turn.duration", TagsMetricContract.TurnDuration);
        Assert.Equal("s", TagsMetricContract.TurnDurationUnit);

        Assert.Equal("stdunit.tags.failures", TagsMetricContract.Failures);
        Assert.Equal("{failure}", TagsMetricContract.FailuresUnit);

        Assert.Equal("stdunit.tags.reconnects", TagsMetricContract.Reconnects);
        Assert.Equal("{reconnect}", TagsMetricContract.ReconnectsUnit);

        Assert.Equal("entry", TagsMetricContract.EntryTag);
    }

    /// <summary>四个指标的名称与单位（单位属于契约：`s` 而不是 `ms`）</summary>
    [Fact]
    public void Instruments_HaveStableNamesAndUnits()
    {
        var units = new Dictionary<string, string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != TagsMetricContract.MeterName)
                {
                    return;
                }

                units[instrument.Name] = instrument.Unit;
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();

        // 触发静态构造（指标在静态 ctor 里创建）
        TagsMetric.RecordTurn(
            new[] { new KeyValuePair<string, object?>("entry", "unit-test") },
            TimeSpan.FromMilliseconds(1));

        Assert.Equal("s", units["stdunit.tags.turn.duration"]);
        Assert.Equal("{turn}", units["stdunit.tags.turns"]);
        Assert.Equal("{failure}", units["stdunit.tags.failures"]);
        Assert.Equal("{reconnect}", units["stdunit.tags.reconnects"]);
    }

    /// <summary>一次轮次记录 = counter +1、histogram 记秒数，且都带 entry 维度</summary>
    [Fact]
    public void RecordTurn_RecordsCounterAndHistogram_WithEntryTag()
    {
        var entry = "record-turn-" + Guid.NewGuid().ToString("N");
        var tags = new[] { new KeyValuePair<string, object?>("entry", entry) };

        long turns = -1;
        double seconds = double.NaN;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == TagsMetricContract.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tagList, state) =>
        {
            if (instrument.Name == "stdunit.tags.turns" && HasEntryTag(tagList, entry))
            {
                turns = value;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tagList, state) =>
        {
            if (instrument.Name == "stdunit.tags.turn.duration" && HasEntryTag(tagList, entry))
            {
                seconds = value;
            }
        });
        listener.Start();

        TagsMetric.RecordTurn(tags, TimeSpan.FromSeconds(1.5));

        Assert.Equal(1, turns);
        Assert.Equal(1.5, seconds, 9);
    }

    /// <summary>
    /// 崩溃与恢复各自 +1，且只带 entry 维度（不带 driver——失败可能来自辅通道，标注主通道会算错责任人）。
    /// </summary>
    [Fact]
    public void RecordFailureAndReconnect_OnlyCarryEntryTag()
    {
        var entry = "record-failure-" + Guid.NewGuid().ToString("N");
        var tags = new[] { new KeyValuePair<string, object?>("entry", entry) };

        var seen = new Dictionary<string, int>();
        var tagKeys = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == TagsMetricContract.MeterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tagList, state) =>
        {
            if (!HasEntryTag(tagList, entry))
            {
                return;
            }

            seen.TryGetValue(instrument.Name, out var current);
            seen[instrument.Name] = current + (int)value;
            foreach (var tag in tagList)
            {
                tagKeys.Add(tag.Key);
            }
        });
        listener.Start();

        TagsMetric.RecordFailure(tags);
        TagsMetric.RecordReconnect(tags);

        Assert.Equal(1, seen["stdunit.tags.failures"]);
        Assert.Equal(1, seen["stdunit.tags.reconnects"]);
        Assert.All(tagKeys, key => Assert.Equal("entry", key));
    }
}
