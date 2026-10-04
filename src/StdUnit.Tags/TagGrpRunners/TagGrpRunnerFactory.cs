using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StdUnit.Tags;

internal class TagGrpRunnerFactory : ITagGrpRunnerFactory
{
    private readonly IServiceProvider _sp;

    /// <summary>
    /// c'tor
    /// </summary>
    public TagGrpRunnerFactory(IServiceProvider sp)
    {
        this._sp = sp;
    }

    /// <inheritdoc/>
    public ITagGrpRunner Create(ITagsProject project)
    {
        var logger = this._sp.GetRequiredService<ILogger<TagGrpRunner>>();
        var retryStrategy = this._sp.GetService<ITagGrpRunnerRetryStrategy>();
        var pollDelayStrategy = this._sp.GetService<ITagGrpRunnerPollDelayStrategy>();
        var disconnectStrategy = this._sp.GetService<ITagGrpRunnerDisconnectStrategy>();
        var runner = new TagGrpRunner(project, logger, retryStrategy, pollDelayStrategy, disconnectStrategy);
        return runner;
    }
}
