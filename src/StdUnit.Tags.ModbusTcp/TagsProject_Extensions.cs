using Microsoft.Extensions.DependencyInjection;

namespace StdUnit.Tags.ModbusTcp;

/// <summary>
/// extensions for DependencyInjection
/// </summary>
public static class TagsProject_Extensions
{
    /// <summary>
    /// 添加 ModbusTcp 支持。
    /// 是 
    /// - <see cref="AddModbusTcpChannel"/>、<br/>
    /// - <see cref="AddModbusBitTagCbntBuilder"/>、<br/>
    /// - <see cref="AddModbusRegisterTagCbntBuilder"/> <br/>
    /// - 与 <see cref="AddModbusTcpDirectTagBuilder"/> 的组合<br/>
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddModbusTcpSupport(this TagsProjectServiceBuilder builder)
    {
        builder.Services.AddSingleton<ITagsProjectSchemaProvider, ModbusTcpSchemaProvider>();

        builder
            .AddModbusTcpChannel()
            .AddModbusBitTagCbntBuilder()
            .AddModbusRegisterTagCbntBuilder()
            .AddModbusTcpDirectTagBuilder();
        return builder;
    }

    #region 基本扩展
    /// <summary>
    /// 注册ModbusTcp支持——仅注册ChannelFactory，不注册DirectTagBuilder/BitTagCbntBuilder/RegisterTagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="ModbusTcpNames.DriverName"/> 时，会尝试构建一个通道。
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddModbusTcpChannel(this TagsProjectServiceBuilder builder)
    {
        // register channel factory
        builder.Services.AddKeyedSingleton<ITagChannelFactory, ModbusTcpChannelFactory>(ModbusTcpNames.DriverName);
        builder.ConfigChannelsFactory((sp, composite) =>
        {
            var factory = sp.GetRequiredKeyedService<ITagChannelFactory>(ModbusTcpNames.DriverName);
            composite.AddFactory(factory);
        });
        return builder;
    }

    /// <summary>
    /// 注册ModbusTcp支持——仅注册<see cref="ModbusBitTagCbntBuilder"/>，不注册ChannelFactory/DirectTagBuilder/RegisterTagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="ModbusTcpNames.DriverName"/> 时，会尝试构建一个位空间（线圈/离散输入）测点组合。
    /// 寄存器空间（3x/4x）的组合请使用 <see cref="AddModbusRegisterTagCbntBuilder"/>。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置TagCbntBuilder的回调</param>
    /// <param name="predicate">用于过滤TagCbntBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddModbusBitTagCbntBuilder(
        this TagsProjectServiceBuilder builder,
        Action<ModbusBitTagCbntBuilder>? configure = null,
        Func<ModbusBitTagCbntBuilder, bool>? predicate = null
        )
    {
        // register cbnt loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<ModbusBitTagCbntBuilder>(ModbusTcpNames.DriverName,
                configure: configure,
                predicate: b => b.IsNotRegisterArea() && (predicate?.Invoke(b) ?? true));
        });
        return builder;
    }

    /// <summary>
    /// 注册ModbusTcp支持——仅注册<see cref="ModbusRegisterTagCbntBuilder"/>，不注册ChannelFactory/DirectTagBuilder/BitTagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="ModbusTcpNames.DriverName"/> 时，会尝试构建一个寄存器空间（保持寄存器/输入寄存器）测点组合，
    /// 缓存为寄存器数组（每元素 = 一个寄存器值），排布由 <c>endian</c>（每个寄存器内部两个字节）与
    /// <c>interpret</c>（32/64 位里寄存器之间的顺序）描述。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置TagCbntBuilder的回调</param>
    /// <param name="predicate">用于过滤TagCbntBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddModbusRegisterTagCbntBuilder(
        this TagsProjectServiceBuilder builder,
        Action<ModbusRegisterTagCbntBuilder>? configure = null,
        Func<ModbusRegisterTagCbntBuilder, bool>? predicate = null
        )
    {
        // register cbnt loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddTagsCbntBuilder<ModbusRegisterTagCbntBuilder>(ModbusTcpNames.DriverName,
                configure: configure,
                predicate: b => b.IsRegisterArea() && (predicate?.Invoke(b) ?? true));
        });
        return builder;
    }

    /// <summary>
    /// 注册ModbusTcp支持——仅注册直接测点构建器（DirectTagBuilder），不注册ChannelFactory/TagCbntBuilder <br/>
    /// 作用是在通道的驱动为 <see cref="ModbusTcpNames.DriverName"/> 时，会尝试构建一个测点。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">配置DirectTagBuilder的回调</param>
    /// <param name="predicate">用于过滤DirectTagBuilder的谓词</param>
    /// <returns></returns>
    public static TagsProjectServiceBuilder AddModbusTcpDirectTagBuilder(
        this TagsProjectServiceBuilder builder,
        Action<ModbusTcpDirectTagBuilder>? configure = null,
        Func<ModbusTcpDirectTagBuilder, bool>? predicate = null
        )
    {
        // register tags loader
        builder.ConfigTagsLoader((sp, composite) =>
        {
            composite.AddDirectTagBuilder<ModbusTcpDirectTagBuilder>(ModbusTcpNames.DriverName, configure, predicate);
        });
        return builder;
    }
    #endregion
}
