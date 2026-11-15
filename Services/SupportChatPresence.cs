using System.Collections.Concurrent;

namespace WebApplication1.Services;

public sealed class SupportChatPresence
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(75);
    private readonly ConcurrentDictionary<string, DateTime> lastSeenUtc = new(StringComparer.Ordinal);

    public void SetOnline(string userId) => lastSeenUtc[userId] = DateTime.UtcNow;

    public void SetOffline(string userId) => lastSeenUtc.TryRemove(userId, out _);

    public bool IsOnline(string userId)
    {
        if (!lastSeenUtc.TryGetValue(userId, out var lastSeen))
        {
            return false;
        }

        if (lastSeen >= DateTime.UtcNow - OnlineWindow)
        {
            return true;
        }

        lastSeenUtc.TryRemove(userId, out _);
        return false;
    }

    public string? GetAvailableEmployee()
    {
        var cutoff = DateTime.UtcNow - OnlineWindow;
        foreach (var entry in lastSeenUtc)
        {
            if (entry.Value < cutoff)
            {
                lastSeenUtc.TryRemove(entry.Key, out _);
            }
        }

        return lastSeenUtc.OrderBy(entry => entry.Value).Select(entry => entry.Key).FirstOrDefault();
    }
}
