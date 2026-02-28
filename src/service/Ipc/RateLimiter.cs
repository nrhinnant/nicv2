using System.Diagnostics;

namespace WfpTrafficControl.Service.Ipc;

/// <summary>
/// Thread-safe token bucket rate limiter for IPC request throttling.
/// Implements both per-client and global rate limiting to prevent DoS attacks.
/// </summary>
/// <remarks>
/// <para><strong>Defense in Depth:</strong></para>
/// <para>
/// This rate limiter enforces two independent limits:
/// </para>
/// <list type="bullet">
///   <item><description>Per-client limit: Prevents any single user from monopolizing the service</description></item>
///   <item><description>Global limit: Prevents bypass via multiple identities (e.g., attacker using multiple user accounts)</description></item>
/// </list>
/// <para>
/// A request must pass BOTH limits to be allowed. This prevents identity spoofing attacks
/// where an attacker with admin access creates multiple users to bypass per-user limits.
/// </para>
/// </remarks>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ClientRateState> _clients = new();
    private readonly List<string> _expiredClientsBuffer = new(); // Reusable buffer for cleanup
    private int _callCount;

    // Global rate limit state
    private int _globalTokensRemaining;
    private long _globalWindowStart;

    /// <summary>
    /// Maximum number of tokens (requests) allowed per window per client.
    /// </summary>
    public int MaxTokens { get; }

    /// <summary>
    /// Maximum number of tokens (requests) allowed per window globally across all clients.
    /// </summary>
    public int GlobalMaxTokens { get; }

    /// <summary>
    /// Window size in seconds for token refill.
    /// </summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// Default maximum tokens per window per client.
    /// </summary>
    public const int DefaultMaxTokens = 10;

    /// <summary>
    /// Default maximum tokens per window globally.
    /// Set higher than per-client to allow multiple legitimate admins,
    /// but low enough to prevent DoS via identity spoofing.
    /// </summary>
    public const int DefaultGlobalMaxTokens = 30;

    /// <summary>
    /// Default window size in seconds.
    /// </summary>
    public const int DefaultWindowSeconds = 10;

    /// <summary>
    /// Creates a new rate limiter with the specified limits.
    /// </summary>
    /// <param name="maxTokens">Maximum requests allowed per window per client (default: 10)</param>
    /// <param name="windowSeconds">Window size in seconds (default: 10)</param>
    /// <param name="globalMaxTokens">Maximum requests allowed per window globally (default: 30)</param>
    public RateLimiter(
        int maxTokens = DefaultMaxTokens,
        int windowSeconds = DefaultWindowSeconds,
        int globalMaxTokens = DefaultGlobalMaxTokens)
    {
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Must be positive");
        if (windowSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Must be positive");
        if (globalMaxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(globalMaxTokens), "Must be positive");
        if (globalMaxTokens < maxTokens)
            throw new ArgumentOutOfRangeException(nameof(globalMaxTokens),
                "Global max tokens must be >= per-client max tokens");

        MaxTokens = maxTokens;
        WindowSeconds = windowSeconds;
        GlobalMaxTokens = globalMaxTokens;

        // Initialize global rate limit state
        _globalTokensRemaining = globalMaxTokens;
        _globalWindowStart = GetCurrentTimestamp();
    }

    /// <summary>
    /// Attempts to acquire a token for the specified client.
    /// Returns true if the request is allowed, false if rate limited.
    /// </summary>
    /// <param name="clientIdentity">Unique identifier for the client (e.g., username)</param>
    /// <returns>True if request is allowed, false if rate limited</returns>
    /// <remarks>
    /// This method checks BOTH per-client and global rate limits.
    /// A request must pass both limits to be allowed. Tokens are only consumed
    /// when the request succeeds both checks (atomic behavior).
    /// </remarks>
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

            // STEP 1: Check if global tokens are available (but don't consume yet)
            if (!HasGlobalTokenAvailable(now))
            {
                return false;
            }

            // STEP 2: Check per-client rate limit and get available tokens
            if (!_clients.TryGetValue(clientIdentity, out var state))
            {
                // First request from this client - will succeed
                // Now consume both tokens atomically
                ConsumeGlobalToken(now);
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
                // Window expired, will reset tokens - request will succeed
                // Now consume both tokens atomically
                ConsumeGlobalToken(now);
                state.TokensRemaining = MaxTokens - 1; // Consume one token
                state.WindowStart = now;
                return true;
            }

            // Window still active, check if tokens available
            if (state.TokensRemaining > 0)
            {
                // Both global and per-client tokens available - consume both atomically
                ConsumeGlobalToken(now);
                state.TokensRemaining--;
                return true;
            }

            // No tokens remaining for this client - rate limited
            // Don't consume global token since request is denied
            return false;
        }
    }

    /// <summary>
    /// Checks if a global token is available. Must be called while holding _lock.
    /// Does NOT consume the token - use ConsumeGlobalToken for that.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns>True if a global token is available, false if global rate limit exceeded.</returns>
    private bool HasGlobalTokenAvailable(long now)
    {
        // Check if global window has expired - if so, tokens will be reset
        var elapsedSeconds = (now - _globalWindowStart) / Stopwatch.Frequency;
        if (elapsedSeconds >= WindowSeconds)
        {
            return true; // Window will reset, tokens available
        }

        // Window still active, check if global tokens available
        return _globalTokensRemaining > 0;
    }

    /// <summary>
    /// Consumes a global token. Must be called while holding _lock.
    /// Caller must ensure HasGlobalTokenAvailable returned true before calling this.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    private void ConsumeGlobalToken(long now)
    {
        // Check if global window has expired and reset if needed
        var elapsedSeconds = (now - _globalWindowStart) / Stopwatch.Frequency;
        if (elapsedSeconds >= WindowSeconds)
        {
            // Window expired, reset global tokens then consume one
            _globalTokensRemaining = GlobalMaxTokens - 1;
            _globalWindowStart = now;
            return;
        }

        // Window still active, consume a token
        _globalTokensRemaining--;
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
    /// Gets the number of global tokens remaining (for testing/diagnostics).
    /// </summary>
    public int GetGlobalTokensRemaining()
    {
        lock (_lock)
        {
            var now = GetCurrentTimestamp();
            var elapsedSeconds = (now - _globalWindowStart) / Stopwatch.Frequency;
            if (elapsedSeconds >= WindowSeconds)
            {
                return GlobalMaxTokens; // Window expired, would reset
            }
            return _globalTokensRemaining;
        }
    }

    /// <summary>
    /// Clears all tracked client state and resets global state (for testing).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _clients.Clear();
            _globalTokensRemaining = GlobalMaxTokens;
            _globalWindowStart = GetCurrentTimestamp();
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
    /// Uses a reusable buffer to avoid allocation per cleanup cycle.
    /// </summary>
    private void CleanupExpiredEntries(long now)
    {
        _expiredClientsBuffer.Clear();
        var expirationThreshold = WindowSeconds * 2 * Stopwatch.Frequency; // 2x window = expired

        foreach (var kvp in _clients)
        {
            var elapsed = now - kvp.Value.WindowStart;
            if (elapsed > expirationThreshold)
            {
                _expiredClientsBuffer.Add(kvp.Key);
            }
        }

        foreach (var client in _expiredClientsBuffer)
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
