using StdUnit.Tags;
using StdUnit.Tags.R3;
using R3;
using System.Windows;

namespace WpfDemo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private IDisposable _disposables;

    public MainWindow()
    {
        InitializeComponent();

        var app = App.Current as App ?? throw new InvalidCastException("App.Current is not of type App");

        var projobs = app.Ctrl.ObserveStartedOrStopped()
            .Select(evt => evt.IsStarted ? evt.Project : null)
            // 测点项目由后台线程在 Application_Startup 中启动，可能早于本窗口构造；
            // StartedOrStopped 是普通事件、不会重放历史记录，因此用当前 Project 作首值兜底。
            .Prepend(() => app.Ctrl.Project)
            .DistinctUntilChanged()
            .Publish()
            .RefCount();
        this._disposables = projobs.Select(proj => Observable.Create<Unit>(observer => {
                return proj is null ?
                    Disposable.Empty :
                    SubscribeTags(proj!.Tags) ;
            }))
            .Switch()
            .Subscribe();
    }

    private IDisposable SubscribeTags(ITagGrp tags)
    {
        var req = tags.SelectTag("IoBox/通用状态/PLC/心跳请求");
        var ack = tags.SelectTag("IoBox/通用状态/MST/心跳响应");
        var interval = tags.SelectTag("IoBox/通用状态/MST/扫描周期");

        var d = Disposable.CreateBuilder();
        req.Watch()
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(evt =>
            {
                this.txtReq.Text = evt.NewValue?.ToString();
            })
            .AddTo(ref d);

        ack.Watch()
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(evt =>
            {
                this.txtAck.Text = evt.NewValue?.ToString();
            })
            .AddTo(ref d);

        interval.Watch()
            .Chunk(5)
            .Select(wnd =>
                wnd.Select(evt =>
                {
                    var val = evt.NewValue;
                    return val is null ? 0 : (float)val;
                })
                .Average()
            )
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(val =>
            {
                this.txtInterval.Text = $"{val:F3} ms";
            })
            .AddTo(ref d);
        return d.Build();
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}