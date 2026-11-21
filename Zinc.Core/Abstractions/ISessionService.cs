using Zinc.Core.Models;

namespace Zinc.Core.Abstractions;

/// <summary>会话持久化服务：保存和加载打开的文件与光标位置。</summary>
public interface ISessionService
{
    /// <summary>加载上次保存的会话快照；不存在或损坏时返回 null。</summary>
    Task<SessionSnapshot?> LoadAsync();

    /// <summary>保存会话快照（内部防抖，连续调用仅最后一次落盘）。</summary>
    void Save(SessionSnapshot snapshot);
}
