using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StdUnit.Tags.OpcUaClient;
using Microsoft.Extensions.Logging.Abstractions;
using Opc.Ua;

namespace StdUnit.Tags.Tests.OpcUaClientTags;

/// <summary>
/// Mock 通道，继承自 <see cref="OpcUaClientTagChannel"/>，重写 ReadAsync/WriteAsync。
/// 可通过 <see cref="ReadAsyncOverride"/> 和 <see cref="WriteAsyncOverride"/> 注入自定义行为。
/// </summary>
internal class MockOpcUaChannel : OpcUaClientTagChannel
{
    public MockOpcUaChannel(string channelName)
        : base(new OpcUaClientTagChannelDescriptor() { Name = channelName }, NullLogger<OpcUaClientTagChannel>.Instance)
    {
    }

    /// <summary>重写 ReadAsync 的委托</summary>
    public Func<IList<NodeId>, CancellationToken, Task<(DataValueCollection, IList<ServiceResult>)>>? ReadAsyncOverride { get; set; }

    /// <summary>重写 WriteAsync 的委托</summary>
    public Func<IDictionary<NodeId, DataValue>, CancellationToken, Task>? WriteAsyncOverride { get; set; }

    public override Task<(DataValueCollection values, IList<ServiceResult> errs)> ReadAsync(IList<NodeId> nodeIds, CancellationToken ct)
    {
        if (ReadAsyncOverride is not null)
            return ReadAsyncOverride(nodeIds, ct);
        return base.ReadAsync(nodeIds, ct);
    }

    public override Task WriteAsync(IDictionary<NodeId, DataValue> toBeWritten, CancellationToken ct)
    {
        if (WriteAsyncOverride is not null)
            return WriteAsyncOverride(toBeWritten, ct);
        return base.WriteAsync(toBeWritten, ct);
    }
}
