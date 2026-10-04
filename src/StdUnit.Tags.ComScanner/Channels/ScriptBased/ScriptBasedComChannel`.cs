using Microsoft.Extensions.Logging;
using System.IO.Ports;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace StdUnit.Tags.ComScanner.Channels;


/// <summary>
/// 基于脚本的串口通信通道，每次读取数据时都会执行脚本，脚本中可以使用 serial 变量来访问串口，得到一个类型为T的对象。
/// </summary>
/// <typeparam name="T"></typeparam>
public class ScriptBasedComChannel<T> : ComChannelBase<T>
{

    /// <summary>
    /// c'tor
    /// </summary>
    public ScriptBasedComChannel(ComChannelDescriptor descriptor, ILogger<ComChannelBase<T>> logger)
        : base(descriptor, logger)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.Option.ReadScript))
        {
            this.ReadScript = descriptor.Option.ReadScript;
        }
        this.ReadScriptEmitDebugInformationEnabled = descriptor.Option.ReadScriptDebugInformationEnabled;
    }

    /// <summary>
    /// 读数据的脚本，返回值必须是类型为T的对象。可以使用 serial 变量来访问串口。
    /// </summary>
    public string? ReadScript { get; } = "return serial.ReadLine();";


    ScriptRunner<T>? _runner;

    /// <summary>
    /// 启用调试信息输出？
    /// </summary>
    public bool ReadScriptEmitDebugInformationEnabled { get; }

    private string? _oldScriptPath;

    /// <inheritdoc/>
    protected override async Task<T?> ParseDataAsync(ISerialPortHandle serial, CancellationToken ct)
    {
        if (this._runner is null)
        {
            var scriptOptions = ScriptOptions.Default
                    .AddReferences(
                        typeof(ITag).Assembly,
                        typeof(ComChannelOption).Assembly,
                        typeof(ISerialPortHandle).Assembly,
                        typeof(SerialPort).Assembly
                        )
                    .WithImports("System.IO.Ports");
            if (this.ReadScriptEmitDebugInformationEnabled)
            {
                TryClearOldScriptPath();
                var tempPath = Path.GetTempPath();
                var tempFileName = Path.Combine(tempPath, $"StdUnit.Tags.COM.{Guid.NewGuid()}.csx");
                var encoding = System.Text.Encoding.UTF8;
                await Compat.FileAsyncCompat.WriteAllTextAsync(tempFileName, this.ReadScript, encoding, ct);
                this._oldScriptPath = tempFileName;
                scriptOptions = scriptOptions
                    .WithEmitDebugInformation(true)
                    .WithFilePath(tempFileName)
                    .WithFileEncoding(encoding);
            }

            var script = CSharpScript.Create<T>(
                this.ReadScript,
                scriptOptions,
                globalsType: typeof(SerialPortGlobals)
            );
            try
            {
                this._runner = script.CreateDelegate();
            }
            catch (CompilationErrorException ex)
            {
                this._logger.LogError("脚本编译错误：{exMsg}, {strace}", ex.Message, ex.StackTrace);
                throw;
            }
        }
        var adapter = serial as SerialPortAdapter ?? throw new InvalidOperationException("ScriptBasedComChannel 需要真实的 SerialPort，请使用 SerialPortAdapter");
        var realPort = adapter.InnerPort;
        var result = await this._runner.Invoke(new SerialPortGlobals(realPort), ct);
        return result;
    }

    private void TryClearOldScriptPath()
    {
        try
        {
            if (string.IsNullOrEmpty(this._oldScriptPath))
            {
                return;
            }

            if (!File.Exists(this._oldScriptPath))
            {
                return;
            }

            File.Delete(this._oldScriptPath);
        }
        catch
        {
            // ignore any exception
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.TryClearOldScriptPath();
        base.Dispose();
    }
}
