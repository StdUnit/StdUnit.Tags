using StdUnit.Tags.R3;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using R3;


namespace StdUnit.Tags.BlazorLib.Components.Tags;

/// <summary>
/// 测点视图组件，显示测点的基本信息和当前值，并提供编辑功能。
/// </summary>
public partial class TagView : IDisposable
{
    /// <summary>
    /// 测点所属的<see cref="ITagsProject"/>
    /// </summary>
    [Parameter]
    public ITagsProject? Project { get; set; }

    /// <summary>
    /// 对应的<see cref="ITag"/>
    /// </summary>
    [Parameter]
    public ITag? Tag { get; set; }


    private string TagName { get; set; } = string.Empty;
    private string TagAddress { get; set; } = string.Empty;
    private string TagKind { get; set; } = BuiltinTagKinds.Unknown;
    private int TagSize { get; set; }

    /// <summary>
    /// （冒泡式）访问模式。
    /// </summary>
    private TagAccessMode TagAccessMode { get; set; }
    private bool IsMarkNotScannedBtnDisabled { get; set; } = true;

    /// <summary>
    /// 值
    /// </summary>
    public object? Value { get; set; }
    private DateTime Timestamp { get; set; }


    /// <summary>
    /// 只读？
    /// </summary>
    public bool IsReadOnly { get; private set; } = true;


    /// <inheritdoc/>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (parameters.TryGetValue<ITag>(nameof(Tag), out var tag))
        {
            _disposable?.Dispose();
            _disposable = null;

            if (tag is not null)
            {
                TagName = tag.TagName();
                TagAddress = tag.NormalizedAddress();
                TagKind = tag.TagKind();
                TagSize = tag.TagDescriptor.TagSize;
                IsReadOnly = tag.IsReadOnly();
                TagAccessMode = tag.SearchAccessMode();
                // Throttle updates and avoid re-rendering if nothing actually changed.
                _disposable = tag.Watch()
                    .TakeUntil(_destroySignal)
                    .ThrottleLast(TimeSpan.FromMilliseconds(50))
                    .Subscribe(ev =>
                    {
                        if (disposedValue)
                            return;
                        var val = ev.NewValue;
                        var ts = ev.Timestamp;

                        if (IsSame(Value, val) && ts == Timestamp)
                            return;

                        _ = InvokeAsync(() =>
                        {
                            if (this.disposedValue)
                            {
                                return;
                            }

                            Value = val;
                            Timestamp = ts;
                            StateHasChanged();
                        }).ContinueWith(t =>
                        {
                            if (t.Exception != null)
                            {
                                // 记录日志或忽略（组件已释放时的异常）
                                foreach (var ex in t.Exception.InnerExceptions)
                                {
                                    if (ex is ObjectDisposedException or InvalidOperationException)
                                    {
                                        ; // 安全忽略
                                    }
                                    else
                                    {
                                        throw ex; // 其他异常不应吞没
                                    }
                                }
                            }
                        }, TaskScheduler.Default);
                    });
            }
        }

        return base.SetParametersAsync(parameters);
    }

    private static bool IsSame(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        return a.Equals(b);
    }

    private async Task OpenEditDialog()
    {
        if (Tag is null)
            return;

        var parameters = new DialogParameters<TagEditDialog>
        {
            { x => x.Project, this.Project },
            { x => x.Tag, Tag },
        };

        var options = new DialogOptions { CloseOnEscapeKey = true, FullWidth = true, MaxWidth = MaxWidth.ExtraSmall };
        await DialogService.ShowAsync<TagEditDialog>("编辑测点", parameters, options);
    }


    /// <summary>
    /// 标记测点为未扫描状态，对于R1W型测点，可以强制刷新测点的值。<br/>
    /// 对于 TagCbnt 下的子测点，标记未扫描会冒泡到父级 TagCbnt，标记父级为未扫描。<br/>
    /// </summary>
    /// <returns></returns>
    private async Task MarkNotScanned()
    {
        var tag = this.Tag;
        var project = this.Project;
        if (project is null || tag is null)
            return;

        var parent = tag.Parent;
        Task task = Task.CompletedTask;

        var entry = tag.SearchEntry();
        if (entry is null)
            return;

        parent?.Map(
            cbnt =>
            {
                project.WriteIntent(entry.TagName(), (entry, ct) =>
                {
                    cbnt.IsScaned = false;
                    return ValueTask.CompletedTask;
                },
                out task);
                return ValueTuple.Create();
            },
            grp =>
            {
                project.WriteIntent(
                    entry.TagName(),
                    (entry, ct) =>
                    {
                        tag.IsScaned = false;
                        return ValueTask.CompletedTask;
                    },
                    out task
                );
                return ValueTuple.Create();
            }
        );

        await task;
    }

    #region IDisposable Support
    private IDisposable? _disposable;
    private bool disposedValue;
    private readonly Subject<Unit> _destroySignal = new Subject<Unit>();

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                try
                {
                    this._destroySignal.OnNext(Unit.Default);
                }
                catch { }

                try
                {
                    this._destroySignal.OnCompleted();
                }
                catch { }

                try
                {
                    _disposable?.Dispose();
                }
                catch { }
            }
            disposedValue = true;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}