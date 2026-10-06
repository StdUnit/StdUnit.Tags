using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StdUnit.Tags.ZLan;

/// <summary>
/// ZLan 驱动的注册入口。
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 注册 ZLan 远程 IO 支持：通道工厂、通道描述符 schema、DI/DO 两类测点组合的构建器。<br/>
    /// 前置条件是已注册 ModbusTcp 支持（ZLan 建立在 <see cref="StdUnit.Tags.ModbusTcp.ModbusTcpChannel"/> 之上）。
    /// </summary>
    /// <param name="builder">服务构建器</param>
    /// <returns>服务构建器（便于链式调用）</returns>
    public static TagsProjectServiceBuilder AddZLanTcpSupport(this TagsProjectServiceBuilder builder)
    {

        builder.Services.AddSingleton<ITagsProjectSchemaProvider, ZLanTcpSchemaProvider>();

        // register channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, ZLanTcpChannelFactory>(ZLanTcpNames.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>(ZLanTcpNames.DriverName);
            composite.AddFactory(factory);
        });

        // register tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<ZLanDICbntBuilder>(ZLanTcpNames.DriverName, predicate: b => b.Area == "DI");
            composite.AddTagsCbntBuilder<ZLanDOCbntBuilder>(ZLanTcpNames.DriverName, predicate: b => b.Area == "DO");
        });

        return builder;
    }
}
