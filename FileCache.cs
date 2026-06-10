using DrugiProjekatSistemskoProgramiranje;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

class CacheEntry
{
    public string Value;
    public DateTime ExpireAt;
    public bool IsLoading;

    public readonly object LockObj = new object();
}

class FileCache
{
    private ConcurrentDictionary<string, CacheEntry> cache =
    new ConcurrentDictionary<string, CacheEntry>();

    private readonly int maxSize = 100;

    private TimeSpan ttl = TimeSpan.FromSeconds(30);

    public async Task<string> GetAsync(string key, Func<Task<string>> factory)
    {
        CacheEntry entry = cache.GetOrAdd(
            key,
            _ => new CacheEntry()
        );

        lock (entry.LockObj)
        {
            while (entry.IsLoading)
            {
                Monitor.Wait(entry.LockObj);
            }

            if (entry.Value != null &&
                entry.ExpireAt > DateTime.Now)
            {
                Logger.Log($"[CACHE HIT] {key}");
                return entry.Value;
            }

            entry.IsLoading = true;
        }

        string result = null;

        try
        {
            Logger.Log($"[CACHE MISS] {key}");

            result = await factory();
        }
        finally
        {
            lock (entry.LockObj)
            {
                entry.Value = result;
                entry.ExpireAt = DateTime.Now.Add(ttl);
                entry.IsLoading = false;

                Monitor.PulseAll(entry.LockObj);
            }
            EnforceSizeLimit();
        }

        return result;
    }
    private void EnforceSizeLimit()
    {
        if (cache.Count <= maxSize)
            return;

        var toRemove = cache
            .Where(x => !x.Value.IsLoading)
            .OrderBy(x => x.Value.ExpireAt)
            .Take(cache.Count - maxSize)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            cache.TryRemove(key, out _);
            Logger.Log($"[CACHE EVICTED] {key}");
        }
    }

    public void StartCleanupLoop()
    {
        while (Program.IsRunning)
        {
            try
            {
                //linq
                var expiredKeys = cache
                    .Where(x =>
                        !x.Value.IsLoading &&
                        x.Value.ExpireAt <= DateTime.Now)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    cache.TryRemove(key, out _);

                    Logger.Log($"[CACHE REMOVED] {key}");
                }

                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                Logger.Log($"[CACHE CLEANER ERROR] {ex.Message}");
            }
        }
    }
}