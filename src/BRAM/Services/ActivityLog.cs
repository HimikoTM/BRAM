using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RobloxAccountManager.Services;

public sealed record LogEntry(string Time, string Tag, string Text, string Color);

public sealed class ActivityLog
{
    private const int Cap = 500;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly List<LogEntry> _entries = new();

    public ActivityLog() => Load();

    public IReadOnlyList<LogEntry> Entries => _entries;

    public LogEntry Add(string tag, string text, string color)
    {
        var entry = new LogEntry(DateTime.Now.ToString("HH:mm:ss"), tag, text, color);
        _entries.Add(entry);
        if (_entries.Count > Cap) _entries.RemoveRange(0, _entries.Count - Cap);
        Save();
        return entry;
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    public LogEntry[] Tail(int count = 60) =>
        _entries.Skip(Math.Max(0, _entries.Count - count)).ToArray();

    private void Load()
    {
        string? json = Storage.ReadAllTextOrNull(Storage.ActivityLogPath);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<LogEntry>>(json, JsonOpts);
            if (loaded != null) _entries.AddRange(loaded.TakeLast(Cap));
        }
        catch { }
    }

    private void Save()
    {
        try { Storage.WriteAllText(Storage.ActivityLogPath, JsonSerializer.Serialize(_entries, JsonOpts)); }
        catch { }
    }
}
