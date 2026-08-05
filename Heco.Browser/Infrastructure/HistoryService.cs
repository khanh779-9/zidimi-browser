using System.Collections.ObjectModel;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure;

/// <summary>Lưu trữ lịch sử duyệt web (in-memory đơn giản).</summary>
public sealed class HistoryService
{
    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public void Add(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var entry = new HistoryEntry
        {
            Url = url,
            Title = string.IsNullOrWhiteSpace(title) ? url : title,
            VisitedAt = DateTime.Now,
            Id = Entries.Count == 0 ? 1 : Entries.Max(x => x.Id) + 1,
        };
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Entries.Insert(0, entry));
    }

    public void Remove(HistoryEntry entry)
    {
        if (!Entries.Contains(entry)) return;
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Entries.Remove(entry));
    }

    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() => Entries.Clear());
    }
}
