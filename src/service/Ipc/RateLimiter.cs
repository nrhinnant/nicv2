using System.Diagnostics;

namespace WfpTrafficControl.Service.Ipc;

/// <summary>
/// Thread-safe token bucket rate limiter for IPC request throttling.
/// Tracks rate limits per client identity to prevent DoS attacks.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ClientRateState> _clients = new();
    private int _callCount;

    /// <summary>
    /// Maximum number of tokens (requests) allowed per window.
    /// </summary>
    public int MaxTokens { get; }

    /// <summary>
    /// Window size in seconds for token refill.
    /// </summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// Default maximum tokens per window.
    /// </summary>
    public const int DefaultMaxTokens = 10;

    /// <summary>
    /// Default window size in seconds.
    /// </summary>
    public const int DefaultWindowSeconds = 10;

    /// <summary>
    /// Creates a new rate limiter with the specified limits.
    /// </summary>
    /// <param name="maxTokens">Maximum requests allowed per window (default: 10)</param>
    /// <param name="windowSeconds">Window size in seconds (default: 10)</param>
    public RateLimiter(int maxTokens = DefaultMaxTokens, int windowSeconds = DefaultWindowSeconds)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Must be positive");
        if (windowSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Must be positive");

        MaxTokens = maxTokens;
        WindowSeconds = windowSeconds;
    }

    /// <summary>
    /// Attempts to acquire a token for the specified client.
    /// Returns true if the request is allowed, false if rate limited.
    /// </summary>
    /// <param name="clientIdentity">Unique identifier for the client (e.g., username)</param>
    /// <returns>True if request is allowed, false if rate limited</returns>
    public bool TryAcquire(string clientIdentity)
    {
        if (string.IsNullOrEmpty(clientIdentity))
        {
            // SECURITY: Fail-closed - empty identity cannot bypass rate limiting.
            // Callers MUST provide a valid client identity for rate tracking.
            // Returning false prevents any bypass via null/empty identity.
            return false;
        }

        var now = GetCurrentTimestamp();

        lock (_lock)
        {
            // Clean up expired entries periodically (every 100 calls)
            _callCount++;
            if (_clients.Count > 0 && _callCount % 100 == 0)
            {
                CleanupExpiredEntries(now);
            }

            if (!_clients.TryGetValue(clientIdentity, out var state))
            {
                // First request from this client
                state = new ClientRateState
                {
                    TokensRemaining = MaxTokens - 1, // Consume one token
                    WindowStart = now
                };
                _clients[clientIdentity] = state;
                return true;
            }

            // Check if window has expired and reset if needed
            var elapsedSeconds = (now - state.WindowStart) / Stopwatch.Frequency;
            if (elapsedSeconds >= WindowSeconds)
            {
                // Window expired, reset tokens
                state.TokensRemaining = MaxTokens - 1; // Consume one token
                state.WindowStart = now;
                return true;
            }

            // Window still active, check if tokens available
            if (state.TokensRemaining > 0)
            {
                state.TokensRemaining--;
                return true;
            }

            // No tokens remaining - rate limited
            return false;
        }
    }

    /// <summary>
    /// Gets the number of tokens remaining for a client (for testing/diagnostics).
    /// Returns -1 if client not tracked.
    /// </summary>
    public int GetTokensRemaining(string clientIdentity)
    {
        if (string.IsNullOrEmpty(clientIdentity))
            return -1;

        lock (_lock)
        {
            if (_clients.TryGetValue(clientIdentity, out var state))
            {
                var now = GetCurrentTimestamp();
                var elapsedSeconds = (now - state.WindowStart) / Stopwatch.Frequency;
                if (elapsedSeconds >= WindowSeconds)
                {
                    return MaxTokens; // Window expired, would reset
                }
                return state.TokensRemaining;
            }
            return -1;
        }
    }

    /// <summary>
    /// Clears all tracked client state (for testing).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _clients.Clear();
        }
    }

    /// <summary>
    /// Gets the current monotonic timestamp using Stopwatch.
    /// </summary>
    private static long GetCurrentTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Removes expired client entries to prevent memory leaks.
    /// Must be called while holding _lock.
    /// </summary>
    private void CleanupExpiredEntries(long now)
    {
        var expiredClients = new List<string>();
        var expirationThreshold = WindowSeconds * 2 * Stopwatch.Frequency; // 2x window = expired

        foreach (var kvp in _clients)
        {
            var elapsed = now - kvp.Value.WindowStart;
            if (elapsed > expirationThreshold)
            {
                expiredClients.Add(kvp.Key);
            }
        }

        foreach (var client in expiredClients)
        {
            _clients.Remove(client);
        }
    }

    /// <summary>
    /// Internal state tracking for a client.
    /// </summary>
    private sealed class ClientRateState
    {
        public int TokensRemaining { get; set; }
        public long WindowStart { get; set; }
    }
}
