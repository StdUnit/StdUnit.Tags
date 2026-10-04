using StdUnit.Tags.Core.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 通道加载器
/// </summary>
public class TagChannelsLoader : ITagChannelsLoader
{
    private readonly ITagChannelFactory _channelFactory;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="channelFactory"></param>
    public TagChannelsLoader(ITagChannelFactory channelFactory)
    {
        this._channelFactory = channelFactory;
    }


    /// <inheritdoc/>
    public virtual IList<ITagChannel> LoadChannels(IEnumerable<TagChannelDescriptor> descriptors)
    {
        var channels = descriptors
            .Select(this._channelFactory.Create)
            .ToList();
        return channels;
    }


}
