using System.Xml.Linq;

namespace StdUnit.Tags;



/// <summary>
/// extensions for <see cref="CompositeTagsLoader"/> to register specific driver Tag Loader
/// </summary>
public static class TagsLoaderExtensions
{
    /// <summary>
    /// 注册特定驱动的 Tag Loader: <br/>
    ///     如果将来被送入加载器的Tag的channel与这里指定的驱动相同，则会尝试构建一个测点；<br/>
    ///     如果配置了predicate且 predicate调用后给出true，则还会再尝试一次过滤<br/>
    /// </summary>
    /// <typeparam name="TTagBuilder"></typeparam>
    /// <param name="loader"></param>
    /// <param name="driver"></param>
    /// <param name="configure"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static CompositeTagsLoader AddDirectTagBuilder<TTagBuilder>(
        this CompositeTagsLoader loader,
        string driver,
        Action<TTagBuilder>? configure = null,
        Func<TTagBuilder, bool>? predicate = null
    ) where TTagBuilder : TagBuilderBase, new()
    {
        return loader.AddDirectTagBuilder((channel, descriptor) =>
        {
            if (channel.Driver() != driver)
            {
                return null;
            }
            var builder = new TTagBuilder();
            builder.WithChannel(channel);
            builder.WithTagDescriptor(descriptor);
            configure?.Invoke(builder);
            var flag = predicate is null ? true : predicate(builder);
            if (!flag)
            {
                return null;
            }
            return builder;
        });
    }

    /// <summary>
    /// 注册特定驱动的 TagsCbnt 加载器: 
    ///     如果将来被送入加载器的Tag的channel与这里指定的驱动相同，则会尝试构建一个测点组合；<br/>
    ///     如果配置了predicate且 predicate调用后给出true，则还会再尝试一次过滤<br/>
    /// </summary>
    /// <typeparam name="TCbntBuilder"></typeparam>
    /// <param name="loader"></param>
    /// <param name="driver"></param>
    /// <param name="configure"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static CompositeTagsLoader AddTagsCbntBuilder<TCbntBuilder>(
        this CompositeTagsLoader loader,
        string driver,
        Action<TCbntBuilder>? configure = null,
        Func<TCbntBuilder, bool>? predicate = null)
        where TCbntBuilder : TagCbntBuilderBase, new()
    {
        return loader.AddTagsCbntBuilder((channel, descriptor) =>
        {
            if (channel.Driver() != driver)
            {
                return null;
            }
            var builder = new TCbntBuilder();
            builder.WithCbntDescriptor(descriptor);
            configure?.Invoke(builder);
            var flag = predicate is null ? true : predicate(builder);
            if (!flag)
            {
                return null;
            }
            return builder;
        });
    }
}