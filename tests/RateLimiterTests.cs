using WfpTrafficControl.Service.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for the RateLimiter class.
/// </summary>
public class RateLimiterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaults_UsesDefaultValues()
    {
        var limiter = new RateLimiter();

        Assert.Equal(RateLimiter.DefaultMaxTokens, limiter.MaxTokens);
        Assert.Equal(RateLimiter.DefaultWindowSeconds, limiter.WindowSeconds);
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 30);

        Assert.Equal(5, limiter.MaxTokens);
        Assert.Equal(30, limiter.WindowSeconds);
    }

    [Fact]
    public void Constructor_WithZeroMaxTokens_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(maxTokens: 0));
    }

    [Fact]
    public void Constructor_WithNegativeMaxTokens_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(maxTokens: -1));
    }

    [Fact]
    public void Constructor_WithZeroWindowSeconds_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(windowSeconds: 0));
    }

    [Fact]
    public void Constructor_WithNegativeWindowSeconds_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(windowSeconds: -1));
    }

    #endregion

    #region TryAcquire Basic Tests

    [Fact]
    public void TryAcquire_FirstRequest_AllowsRequest()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        var result = limiter.TryAcquire("user1");

        Assert.True(result);
    }

    [Fact]
    public void TryAcquire_WithinLimit_AllowsAllRequests()
    {
        var limiter = new RateLimiter(maxTokens: 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire("user1"), $"Request {i + 1} should be allowed");
        }
    }

    [Fact]
    public void TryAcquire_ExceedingLimit_BlocksExcessRequests()
    {
        var limiter = new RateLimiter(maxTokens: 5);

        // First 5 should succeed
        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire("user1"), $"Request {i + 1} should be allowed");
        }

        // 6th should fail
        Assert.False(limiter.TryAcquire("user1"), "Request 6 should be blocked");
    }

    [Fact]
    public void TryAcquire_NullClientIdentity_DeniesRequest()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        // Null identity should be denied (fail-closed security)
        Assert.False(limiter.TryAcquire(null!));
    }

    [Fact]
    public void TryAcquire_EmptyClientIdentity_DeniesRequest()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        // Empty identity should be denied (fail-closed security)
        Assert.False(limiter.TryAcquire(""));
    }

    #endregion

    #region Client Isolation Tests

    [Fact]
    public void TryAcquire_DifferentClients_TrackedSeparately()
    {
        var limiter = new RateLimiter(maxTokens: 2);

        // User1 uses 2 tokens
        Assert.True(limiter.TryAcquire("user1"));
        Assert.True(limiter.TryAcquire("user1"));
        Assert.False(limiter.TryAcquire("user1"));

        // User2 should still have tokens
        Assert.True(limiter.TryAcquire("user2"));
        Assert.True(limiter.TryAcquire("user2"));
        Assert.False(limiter.TryAcquire("user2"));
    }

    [Fact]
    public void TryAcquire_CaseSensitiveIdentities_TrackedSeparately()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        Assert.True(limiter.TryAcquire("User1"));
        Assert.False(limiter.TryAcquire("User1"));

        // Different case = different client
        Assert.True(limiter.TryAcquire("user1"));
    }

    #endregion

    #region GetTokensRemaining Tests

    [Fact]
    public void GetTokensRemaining_NewClient_ReturnsNegativeOne()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        var remaining = limiter.GetTokensRemaining("unknown");

        Assert.Equal(-1, remaining);
    }

    [Fact]
    public void GetTokensRemaining_AfterOneRequest_ReturnsCorrectCount()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        limiter.TryAcquire("user1");
        var remaining = limiter.GetTokensRemaining("user1");

        Assert.Equal(9, remaining);
    }

    [Fact]
    public void GetTokensRemaining_AfterExhaustion_ReturnsZero()
    {
        var limiter = new RateLimiter(maxTokens: 3);

        limiter.TryAcquire("user1");
        limiter.TryAcquire("user1");
        limiter.TryAcquire("user1");

        var remaining = limiter.GetTokensRemaining("user1");

        Assert.Equal(0, remaining);
    }

    [Fact]
    public void GetTokensRemaining_NullIdentity_ReturnsNegativeOne()
    {
        var limiter = new RateLimiter();

        Assert.Equal(-1, limiter.GetTokensRemaining(null!));
    }

    [Fact]
    public void GetTokensRemaining_EmptyIdentity_ReturnsNegativeOne()
    {
        var limiter = new RateLimiter();

        Assert.Equal(-1, limiter.GetTokensRemaining(""));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllClientState()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        // Exhaust tokens for multiple clients
        limiter.TryAcquire("user1");
        limiter.TryAcquire("user2");

        Assert.False(limiter.TryAcquire("user1"));
        Assert.False(limiter.TryAcquire("user2"));

        // Reset
        limiter.Reset();

        // Should work again
        Assert.True(limiter.TryAcquire("user1"));
        Assert.True(limiter.TryAcquire("user2"));
    }

    #endregion

    #region Rapid Request Tests (Simulating DoS)

    [Fact]
    public void TryAcquire_RapidRequests_BlocksAfterLimit()
    {
        // This is the key test requested:
        // "Add test that sends 20 rapid requests, verify first 10 succeed, remaining fail"
        var limiter = new RateLimiter(maxTokens: 10, windowSeconds: 10);
        var client = "TestAdmin";

        int successCount = 0;
        int failCount = 0;

        // Send 20 rapid requests
        for (int i = 0; i < 20; i++)
        {
            if (limiter.TryAcquire(client))
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        Assert.Equal(10, successCount);
        Assert.Equal(10, failCount);
    }

    [Fact]
    public void TryAcquire_MultipleClientsRapidRequests_EachLimitedIndependently()
    {
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 10);

        int user1Success = 0;
        int user2Success = 0;

        // Interleave requests from two users
        for (int i = 0; i < 10; i++)
        {
            if (limiter.TryAcquire("user1")) user1Success++;
            if (limiter.TryAcquire("user2")) user2Success++;
        }

        Assert.Equal(5, user1Success);
        Assert.Equal(5, user2Success);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task TryAcquire_ConcurrentAccess_ThreadSafe()
    {
        var limiter = new RateLimiter(maxTokens: 100, windowSeconds: 60);
        var client = "concurrent_user";
        int successCount = 0;

        // Run 10 threads, each trying to acquire 20 tokens
        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    if (limiter.TryAcquire(client))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Should have exactly 100 successes (the maxTokens limit)
        Assert.Equal(100, successCount);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentDifferentClients_NoInterference()
    {
        var limiter = new RateLimiter(maxTokens: 10, windowSeconds: 60);
        var results = new int[5]; // Track success count per client

        var tasks = new Task[5];
        for (int t = 0; t < 5; t++)
        {
            int clientIndex = t;
            string client = $"client_{clientIndex}";
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 15; i++)
                {
                    if (limiter.TryAcquire(client))
                    {
                        Interlocked.Increment(ref results[clientIndex]);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Each client should have exactly 10 successes
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(10, results[i]);
        }
    }

    #endregion

    #region Default Constants Tests

    [Fact]
    public void DefaultConstants_HaveExpectedValues()
    {
        Assert.Equal(10, RateLimiter.DefaultMaxTokens);
        Assert.Equal(10, RateLimiter.DefaultWindowSeconds);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TryAcquire_SingleTokenLimit_WorksCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 1, windowSeconds: 60);

        Assert.True(limiter.TryAcquire("user"));
        Assert.False(limiter.TryAcquire("user"));
        Assert.False(limiter.TryAcquire("user"));
    }

    [Fact]
    public void TryAcquire_LargeTokenLimit_WorksCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 10000, windowSeconds: 1);

        for (int i = 0; i < 10000; i++)
        {
            Assert.True(limiter.TryAcquire("user"), $"Request {i + 1} should succeed");
        }

        Assert.False(limiter.TryAcquire("user"), "Request 10001 should fail");
    }

    [Fact]
    public void TryAcquire_SpecialCharactersInIdentity_TrackedCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        Assert.True(limiter.TryAcquire(@"DOMAIN\user.name"));
        Assert.False(limiter.TryAcquire(@"DOMAIN\user.name"));

        // Different identity
        Assert.True(limiter.TryAcquire(@"DOMAIN\other.user"));
    }

    #endregion
}
