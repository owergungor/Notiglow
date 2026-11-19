using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class NotificationDeduplicator
    {
        private readonly Dictionary<string, DateTime> _seenHashes = new();
        private readonly TimeSpan _dedupWindow = TimeSpan.FromSeconds(2.5);

        public bool IsDuplicate(NotificationItem item)
        {
            if (item == null) return false;

            string rawKey = $"{item.AppId}_{item.AppName}_{item.Timestamp.Ticks / TimeSpan.TicksPerSecond}";
            string hash = ComputeHash(rawKey);

            DateTime now = DateTime.Now;

            // Clean up old hashes older than 10 seconds
            if (_seenHashes.Count > 0)
            {
                List<string>? staleKeys = null;
                foreach (var kvp in _seenHashes)
                {
                    if (now - kvp.Value > TimeSpan.FromSeconds(10))
                    {
                        staleKeys ??= new List<string>();
                        staleKeys.Add(kvp.Key);
                    }
                }
                if (staleKeys != null)
                {
                    foreach (var key in staleKeys)
                    {
                        _seenHashes.Remove(key);
                    }
                }
            }

            if (_seenHashes.TryGetValue(hash, out DateTime lastSeen))
            {
                if (now - lastSeen < _dedupWindow)
                {
                    return true; // Duplicate detected within 2.5s window!
                }
            }

            _seenHashes[hash] = now;
            return false;
        }

        private static string ComputeHash(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
