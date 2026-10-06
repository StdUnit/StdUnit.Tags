using Opc.Ua;

namespace StdUnit.Tags.OpcUaClient;

/// <summary>
/// OPC UA 读取结果的可用性判定。<br/>
/// 读操作"没有抛异常"不代表值可用：每个节点各自带状态码。<br/>
/// 本库<b>不解释质量</b>，只区分两件事：<c>Bad</c> = 这次读取失败（抛出去交给重试/崩溃处理）；
/// <c>Good</c>/<c>Uncertain</c> = 采集到值就照原样采集（要可信度就另加一个测点，不要由驱动层替下游决定）。
/// </summary>
internal static class OpcUaValueQuality
{
    /// <summary>
    /// 该节点的读取是否<b>失败</b>（状态码为 <c>Bad</c>）。<br/>
    /// 用"失败"而不是"可用"作为判据：调用点写成 <c>if (IsFailed(...)) { 抛错 }</c>，优先分支与条件一致。<br/>
    /// <b>注意</b>：<c>Uncertain</c>（不确定）不算失败——那只是"服务器给了值、它自己不确定"，
    /// 是否可信是业务问题，不该由驱动层判断；判定时取 <paramref name="err"/> 与 <c>DataValue.StatusCode</c> 中更严格的那个。
    /// </summary>
    /// <param name="err"><see cref="OpcUaClientTagChannel.ReadAsync"/> 返回的每节点错误；单节点读取没有它，可为 <c>null</c>。</param>
    /// <param name="value">读取到的值；为 <c>null</c> 视为失败。</param>
    public static bool IsFailed(ServiceResult? err, DataValue? value)
    {
        if (value is null)
        {
            return true;
        }
        if (err is not null && ServiceResult.IsBad(err))
        {
            return true;
        }
        return StatusCode.IsBad(value.StatusCode);
    }

    /// <summary>
    /// 描述不可用的值（节点 + 状态码），用于日志。
    /// </summary>
    public static string Describe(NodeId nodeId, ServiceResult? err, DataValue? value)
    {
        var code = err?.StatusCode ?? value?.StatusCode ?? StatusCodes.BadNoData;
        return $"节点={nodeId} 状态码=0x{code.Code:X8}({StatusCodes.GetBrowseName(code.Code)})";
    }
}
