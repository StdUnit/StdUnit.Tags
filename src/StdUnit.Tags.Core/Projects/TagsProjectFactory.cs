using StdUnit.Tags.Core.Projects;
using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 默认的 TagsProject 工厂
/// </summary>
public class TagsProjectFactory : ITagsProjectFactory
{
    private readonly ITagGrpRunnerFactory _grpRunnerFactory;
    private readonly ITagChannelsLoader _channelsLoader;
    private readonly ITagsLoader _tagsLoader;
    private readonly ILogicetsLoader _logicetLoader;
    private readonly IServiceProvider _sp;
    private readonly IReadOnlyList<ITagsProjectValidator> _validators;

    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="grpRunnerFactory"></param>
    /// <param name="channelsLoader"></param>
    /// <param name="tagsLoader"></param>
    /// <param name="logicetLoader"></param>
    /// <param name="sp"></param>
    /// <param name="validators">
    ///     可选的加载期校验器集合（可注册多个，如 XSD 校验、channel 引用校验）；
    ///     未注册时为空。
    /// </param>
    public TagsProjectFactory(
        ITagGrpRunnerFactory grpRunnerFactory,
        ITagChannelsLoader channelsLoader,
        ITagsLoader tagsLoader,
        ILogicetsLoader logicetLoader,
        IServiceProvider sp,
        IEnumerable<ITagsProjectValidator>? validators = null)
    {
        this._grpRunnerFactory = grpRunnerFactory;
        this._channelsLoader = channelsLoader;
        this._tagsLoader = tagsLoader;
        this._logicetLoader = logicetLoader;
        this._sp = sp;
        this._validators = validators?.ToArray() ?? Array.Empty<ITagsProjectValidator>();
    }

    /// <inheritdoc/>
    public virtual ITagsProject Create(string projRoot, XElement? root = null)
    {
        var project = new TagsProject(this._grpRunnerFactory, _channelsLoader, _tagsLoader, _logicetLoader, this._sp, this._validators);
        project.Initialize(projRoot, root);
        return project;
    }
}