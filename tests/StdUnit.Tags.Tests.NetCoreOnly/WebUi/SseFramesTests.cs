using System;
using Xunit;
using StdUnit.Tags.WebUi;
using StdUnit.Tags.WebUi.Internal;

namespace StdUnit.Tags.Tests.NetCoreOnly.WebUi;

/// <summary>
/// SSE 帧的线格式：事件名、<c>data:</c> 前缀、camelCase、null 省略。
/// </summary>
public class SseFramesTests
{
    [Fact]
    public void State_Format()
    {
        var text = StreamTestContext.Text(SseFrames.State(new StateDto(7, true, null)));

        Assert.StartsWith("event: state\ndata: ", text);
        Assert.EndsWith("\n\n", text);
        Assert.Contains("\"epoch\":7", text);
        Assert.Contains("\"isStarted\":true", text);

        // null 不出现（DefaultIgnoreCondition = WhenWritingNull）
        Assert.DoesNotContain("projectRoot", text);
    }

    [Fact]
    public void Values_Format()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var frame = SseFrames.Values(new EntryValuesDto(
            Epoch: 3,
            Entry: "g1",
            Seq: 9,
            SampledAtUtc: timestamp,
            Full: false,
            Values: new[] { new TagValueDto("g1/a", "INT16", "42", timestamp, IsDirty: false, IsScanned: true) },
            ContainerFlags: null));

        var text = StreamTestContext.Text(frame);

        Assert.StartsWith("event: values\ndata: ", text);
        Assert.Contains("\"entry\":\"g1\"", text);
        Assert.Contains("\"seq\":9", text);
        Assert.Contains("\"full\":false", text);
        Assert.Contains("\"path\":\"g1/a\"", text);
        Assert.Contains("\"raw\":\"42\"", text);
        Assert.Contains("\"timestampUtc\":\"2026-01-02T03:04:05Z\"", text);
        Assert.DoesNotContain("containerFlags", text);
    }

    [Fact]
    public void Values_KeepsFrameSingleLineWhenValueContainsCrLf()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var frame = SseFrames.Values(
            new EntryValuesDto(
                Epoch: 1,
                Entry: "g1",
                Seq: 1,
                SampledAtUtc: timestamp,
                Full: false,
                Values: new[] { 
                    new TagValueDto("g1/a", "STRING", "a\nb\rc", timestamp, IsDirty: false, IsScanned: true)
                },
                ContainerFlags: null
            )
        );

        var text = StreamTestContext.Text(frame);

        // 帧结构只有 3 个换行：event/data 之间 1 个、帧尾 2 个。
        // 值里的 CR/LF 必须被 JSON 转义——否则 SSE 会把一帧拆成多帧（空行即 dispatch）。
        Assert.Equal(3, text.Split('\n').Length - 1);
        Assert.False(text.Contains('\r'));
        Assert.Contains("a\\nb\\rc", text);
    }

    [Fact]
    public void Retry_And_Ping_AreWellFormed()
    {
        Assert.Equal("retry: 3000\n\n", StreamTestContext.Text(SseFrames.Retry));
        Assert.Equal(": ping\n\n", StreamTestContext.Text(SseFrames.Ping));
    }
}
