namespace StdUnit.Tags.SimpleFiles;


/// <summary>
/// SimpleFiles 通道设置
/// </summary>
/// <param name="BaseDir">基础目录，所有的文件地址都是相对于这个目录的</param>
public record SimpleFilesSettings(string? BaseDir);
