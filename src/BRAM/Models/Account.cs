using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RobloxAccountManager.Models;

public sealed class Account : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _label = "";
    public string Label { get => _label; set { if (_label == value) return; _label = value; OnChanged(); OnChanged(nameof(Display)); } }

    private string _username = "";
    public string Username { get => _username; set { if (_username == value) return; _username = value; OnChanged(); OnChanged(nameof(Display)); } }

    public long UserId { get; set; }

    public string SecurityCookie { get; set; } = "";

    private string _group = "General";
    public string Group { get => _group; set { if (_group == value) return; _group = value; OnChanged(); } }

    public string Notes { get; set; } = "";
    public DateTime DateAdded { get; set; } = DateTime.Now;

    private DateTime? _lastUsed;
    public DateTime? LastUsed
    {
        get => _lastUsed;
        set { _lastUsed = value; OnChanged(); OnChanged(nameof(LastUsedText)); }
    }

    private string _status = "Unknown";
    [JsonIgnore] public string Status { get => _status; set { if (_status == value) return; _status = value; OnChanged(); } }

    private string _avatarUrl = "";
    [JsonIgnore] public string AvatarUrl { get => _avatarUrl; set { if (_avatarUrl == value) return; _avatarUrl = value; OnChanged(); } }

    private bool _isBusy;
    [JsonIgnore] public bool IsBusy { get => _isBusy; set { if (_isBusy == value) return; _isBusy = value; OnChanged(); } }

    [JsonIgnore] public string Display => string.IsNullOrWhiteSpace(Label) ? Username : $"{Label}  ({Username})";
    [JsonIgnore] public string LastUsedText => LastUsed is null ? "never launched" : $"last used {LastUsed.Value:g}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
