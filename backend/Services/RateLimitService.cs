using System.Collections.Concurrent;

namespace DnDSheetApi.Services;

public class RateLimitService
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();

    /// <summary>
    /// Returns true if the request should be BLOCKED.
    /// </summary>
    public bool IsRateLimited(string key, int maxRequests, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var timestamps = _requests.GetOrAdd(key, _ => new List<DateTime>());

        lock (timestamps)
        {
            // Remove old entries outside the window
            timestamps.RemoveAll(t => now - t > window);

            if (timestamps.Count >= maxRequests)
                return true;

            timestamps.Add(now);
            return false;
        }
    }

    /// <summary>
    /// Periodically clean up old entries to prevent memory leaks.
    /// </summary>
    public void Cleanup()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _requests)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => now - t > TimeSpan.FromMinutes(10));
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
            _requests.TryRemove(key, out _);
    }
}
