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
    public void ConstructorWithDefaultsUsesDefaultValues()
    {
        var limiter = new RateLimiter();

        Assert.Equal(RateLimiter.DefaultMaxTokens, limiter.MaxTokens);
        Assert.Equal(RateLimiter.DefaultWindowSeconds, limiter.WindowSeconds);
        Assert.Equal(RateLimiter.DefaultGlobalMaxTokens, limiter.GlobalMaxTokens);
    }

    [Fact]
    public void ConstructorWithCustomValuesSetsCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 30, globalMaxTokens: 50);

        Assert.Equal(5, limiter.MaxTokens);
        Assert.Equal(30, limiter.WindowSeconds);
        Assert.Equal(50, limiter.GlobalMaxTokens);
    }

    [Fact]
    public void ConstructorWithZeroMaxTokensThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(maxTokens: 0));
    }

    [Fact]
    public void ConstructorWithNegativeMaxTokensThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(maxTokens: -1));
    }

    [Fact]
    public void ConstructorWithZeroWindowSecondsThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(windowSeconds: 0));
    }

    [Fact]
    public void ConstructorWithNegativeWindowSecondsThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(windowSeconds: -1));
    }

    [Fact]
    public void ConstructorWithZeroGlobalMaxTokensThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(globalMaxTokens: 0));
    }

    [Fact]
    public void ConstructorWithNegativeGlobalMaxTokensThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(globalMaxTokens: -1));
    }

    [Fact]
    public void ConstructorWithGlobalMaxTokensLessThanMaxTokensThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(maxTokens: 100, globalMaxTokens: 50));
    }

    #endregion

    #region TryAcquire Basic Tests

    [Fact]
    public void TryAcquireFirstRequestAllowsRequest()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        var result = limiter.TryAcquire("user1");

        Assert.True(result);
    }

    [Fact]
    public void TryAcquireWithinLimitAllowsAllRequests()
    {
        var limiter = new RateLimiter(maxTokens: 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire("user1"), $"Request {i + 1} should be allowed");
        }
    }

    [Fact]
    public void TryAcquireExceedingLimitBlocksExcessRequests()
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
    public void TryAcquireNullClientIdentityDeniesRequest()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        // Null identity should be denied (fail-closed security)
        Assert.False(limiter.TryAcquire(null!));
    }

    [Fact]
    public void TryAcquireEmptyClientIdentityDeniesRequest()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        // Empty identity should be denied (fail-closed security)
        Assert.False(limiter.TryAcquire(""));
    }

    #endregion

    #region Client Isolation Tests

    [Fact]
    public void TryAcquireDifferentClientsTrackedSeparately()
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
    public void TryAcquireCaseSensitiveIdentitiesTrackedSeparately()
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
    public void GetTokensRemainingNewClientReturnsNegativeOne()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        var remaining = limiter.GetTokensRemaining("unknown");

        Assert.Equal(-1, remaining);
    }

    [Fact]
    public void GetTokensRemainingAfterOneRequestReturnsCorrectCount()
    {
        var limiter = new RateLimiter(maxTokens: 10);

        limiter.TryAcquire("user1");
        var remaining = limiter.GetTokensRemaining("user1");

        Assert.Equal(9, remaining);
    }

    [Fact]
    public void GetTokensRemainingAfterExhaustionReturnsZero()
    {
        var limiter = new RateLimiter(maxTokens: 3);

        limiter.TryAcquire("user1");
        limiter.TryAcquire("user1");
        limiter.TryAcquire("user1");

        var remaining = limiter.GetTokensRemaining("user1");

        Assert.Equal(0, remaining);
    }

    [Fact]
    public void GetTokensRemainingNullIdentityReturnsNegativeOne()
    {
        var limiter = new RateLimiter();

        Assert.Equal(-1, limiter.GetTokensRemaining(null!));
    }

    [Fact]
    public void GetTokensRemainingEmptyIdentityReturnsNegativeOne()
    {
        var limiter = new RateLimiter();

        Assert.Equal(-1, limiter.GetTokensRemaining(""));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void ResetClearsAllClientState()
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
    public void TryAcquireRapidRequestsBlocksAfterLimit()
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
    public void TryAcquireMultipleClientsRapidRequestsEachLimitedIndependently()
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

    #region Global Rate Limiting Tests

    [Fact]
    public void TryAcquireGlobalLimitEnforcedAcrossAllClients()
    {
        // Set per-client limit to 10, global limit to 15
        // This means 2 clients with 10 tokens each would need 20, but only 15 are globally available
        var limiter = new RateLimiter(maxTokens: 10, windowSeconds: 60, globalMaxTokens: 15);

        int user1Success = 0;
        int user2Success = 0;

        // User 1 takes 10 tokens (all of their per-user limit)
        for (int i = 0; i < 15; i++)
        {
            if (limiter.TryAcquire("user1")) user1Success++;
        }

        // User 2 tries to take 10 tokens, but only 5 global tokens remain
        for (int i = 0; i < 15; i++)
        {
            if (limiter.TryAcquire("user2")) user2Success++;
        }

        Assert.Equal(10, user1Success); // User 1 gets all 10 per-user tokens
        Assert.Equal(5, user2Success);  // User 2 only gets 5 (global limit hit)
    }

    [Fact]
    public void GetGlobalTokensRemainingReturnsCorrectValue()
    {
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 60, globalMaxTokens: 10);

        Assert.Equal(10, limiter.GetGlobalTokensRemaining());

        limiter.TryAcquire("user1");
        Assert.Equal(9, limiter.GetGlobalTokensRemaining());

        limiter.TryAcquire("user2");
        Assert.Equal(8, limiter.GetGlobalTokensRemaining());
    }

    [Fact]
    public void ResetClearsGlobalState()
    {
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 60, globalMaxTokens: 10);

        // Consume some global tokens
        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire($"user{i}");
        }

        Assert.Equal(5, limiter.GetGlobalTokensRemaining());

        // Reset
        limiter.Reset();

        // Global tokens should be restored
        Assert.Equal(10, limiter.GetGlobalTokensRemaining());
    }

    [Fact]
    public void TryAcquireGlobalLimitReachedBlocksAllClients()
    {
        // Use 3 clients with per-user limit of 5 each (15 total potential)
        // But global limit is 8, so not all can get their full quota
        var limiter = new RateLimiter(maxTokens: 5, windowSeconds: 60, globalMaxTokens: 8);

        // User1 takes all 5 tokens (5 global consumed)
        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire("user1"));
        }
        Assert.False(limiter.TryAcquire("user1")); // user1 per-user exhausted

        // User2 takes 3 tokens (8 global consumed total)
        for (int i = 0; i < 3; i++)
        {
            Assert.True(limiter.TryAcquire("user2"));
        }

        // Global limit reached - all users blocked
        Assert.False(limiter.TryAcquire("user2")); // user2 blocked (global exhausted)
        Assert.False(limiter.TryAcquire("user3")); // new user3 blocked too
        Assert.False(limiter.TryAcquire("user1")); // user1 still blocked
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task TryAcquireConcurrentAccessThreadSafe()
    {
        var limiter = new RateLimiter(maxTokens: 100, windowSeconds: 60, globalMaxTokens: 200);
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
    public async Task TryAcquireConcurrentDifferentClientsNoInterference()
    {
        // Set globalMaxTokens high enough for 5 clients x 10 tokens each = 50 total
        var limiter = new RateLimiter(maxTokens: 10, windowSeconds: 60, globalMaxTokens: 100);
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
    public void DefaultConstantsHaveExpectedValues()
    {
        Assert.Equal(10, RateLimiter.DefaultMaxTokens);
        Assert.Equal(10, RateLimiter.DefaultWindowSeconds);
        Assert.Equal(30, RateLimiter.DefaultGlobalMaxTokens);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TryAcquireSingleTokenLimitWorksCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 1, windowSeconds: 60);

        Assert.True(limiter.TryAcquire("user"));
        Assert.False(limiter.TryAcquire("user"));
        Assert.False(limiter.TryAcquire("user"));
    }

    [Fact]
    public void TryAcquireLargeTokenLimitWorksCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 10000, windowSeconds: 1, globalMaxTokens: 20000);

        for (int i = 0; i < 10000; i++)
        {
            Assert.True(limiter.TryAcquire("user"), $"Request {i + 1} should succeed");
        }

        Assert.False(limiter.TryAcquire("user"), "Request 10001 should fail");
    }

    [Fact]
    public void TryAcquireSpecialCharactersInIdentityTrackedCorrectly()
    {
        var limiter = new RateLimiter(maxTokens: 1);

        Assert.True(limiter.TryAcquire(@"DOMAIN\user.name"));
        Assert.False(limiter.TryAcquire(@"DOMAIN\user.name"));

        // Different identity
        Assert.True(limiter.TryAcquire(@"DOMAIN\other.user"));
    }

    #endregion
}
