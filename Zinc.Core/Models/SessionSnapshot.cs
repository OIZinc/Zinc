namespace Zinc.Core.Models;

/// <summary>会话快照：记录上次退出时打开的标签页及各自的光标位置。</summary>
public class SessionSnapshot
{
    public List<TabSession> Tabs { get; set; } = new();

    /// <summary>上次活动的文件路径，用于启动时定位。</summary>
    public string? ActiveFilePath { get; set; }
}

/// <summary>单个标签页的会话状态。</summary>
public class TabSession
{
    public string FilePath { get; set; } = string.Empty;

    /// <summary>光标在文档中的字符偏移量。</summary>
    public int CaretOffset { get; set; }
}
