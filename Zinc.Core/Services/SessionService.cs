using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Zinc.Core.Abstractions;
using Zinc.Core.Models;

namespace Zinc.Core.Services;

/// <summary>会话持久化服务：保存和加载上次退出时打开的标签页与光标位置。</summary>
public sealed class SessionService : ISessionService, IDisposable
{
    /// <summary>属性连续变化时，合并为一次落盘的等待时间。</summary>
    private const int SaveDebounceMilliseconds = 500;

    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _sync = new();
    private string? _pendingJson;
    private Timer? _saveTimer;
    private bool _disposed;

    public SessionService(string? appName = null)
    {
        appName ??= "Zinc";

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, appName, "sessions");
        Directory.CreateDirectory(directory);

        _filePath = Path.Combine(directory, "session.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>加载上次保存的会话快照；不存在或损坏时返回 null。</summary>
    public async Task<SessionSnapshot?> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = await File.ReadAllTextAsync(_filePath);
            var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json, _jsonOptions);
            return snapshot;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[SessionService] 读取会话失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>保存会话快照（内部防抖，连续调用仅最后一次落盘）。</summary>
    public void Save(SessionSnapshot snapshot)
    {
        string json;
        try
        {
            json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[SessionService] 序列化会话失败: {ex.Message}");
            return;
        }

        lock (_sync)
        {
            _pendingJson = json;
        }

        if (_disposed)
            return;

        _saveTimer ??= new Timer(_ => FlushPending(), null, Timeout.Infinite, Timeout.Infinite);
        _saveTimer.Change(SaveDebounceMilliseconds, Timeout.Infinite);
    }

    private void FlushPending()
    {
        string? json;
        lock (_sync)
        {
            json = _pendingJson;
            _pendingJson = null;
        }

        if (json is null)
            return;

        lock (_sync)
        {
            WriteFile(json);
        }
    }

    private void WriteFile(string json)
    {
        try
        {
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[SessionService] 写入会话失败: {_filePath}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Timer? timer;
        lock (_sync)
        {
            timer = _saveTimer;
            _saveTimer = null;
        }

        timer?.Dispose();
        FlushPending();
        GC.SuppressFinalize(this);
    }
}
