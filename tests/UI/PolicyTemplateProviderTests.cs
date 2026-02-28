using WfpTrafficControl.UI.Services;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for PolicyTemplateProvider.
/// </summary>
public class PolicyTemplateProviderTests
{
    private readonly IPolicyTemplateProvider _provider;

    public PolicyTemplateProviderTests()
    {
        _provider = new PolicyTemplateProvider();
    }

    [Fact]
    public void GetTemplatesReturnsNonEmptyList()
    {
        // Act
        var templates = _provider.GetTemplates();

        // Assert
        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
    }

    [Fact]
    public void GetCategoriesReturnsDistinctCategories()
    {
        // Act
        var categories = _provider.GetCategories();

        // Assert
        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
        Assert.Equal(categories.Count, categories.Distinct().Count());
    }

    [Fact]
    public void GetTemplatesByCategoryReturnsMatchingTemplates()
    {
        // Arrange
        var categories = _provider.GetCategories();
        Assert.NotEmpty(categories);
        var category = categories[0];

        // Act
        var templates = _provider.GetTemplatesByCategory(category);

        // Assert
        Assert.NotEmpty(templates);
        Assert.All(templates, t => Assert.Equal(category, t.Category, ignoreCase: true));
    }

    [Fact]
    public void GetTemplatesByCategoryWithNonExistentCategoryReturnsEmpty()
    {
        // Act
        var templates = _provider.GetTemplatesByCategory("NonExistentCategory");

        // Assert
        Assert.Empty(templates);
    }

    [Fact]
    public void TemplatesContainBlockCloudflareDns()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-cloudflare-dns");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("Privacy", template.Category);
        Assert.Contains("Cloudflare", template.Name);

        var policy = template.CreatePolicy();
        Assert.NotEmpty(policy.Rules);
        Assert.Contains(policy.Rules, r => r.Remote?.Ip?.Contains("1.1.1.1") == true);
    }

    [Fact]
    public void TemplatesContainBlockGoogleServices()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-google-services");

        // Assert
        Assert.NotNull(template);
        Assert.NotNull(template.Warning); // Google template should have a warning
        Assert.Contains("Google", template.Name);

        var policy = template.CreatePolicy();
        Assert.NotEmpty(policy.Rules);
        Assert.Contains(policy.Rules, r => r.Remote?.Ip?.Contains("8.8.8.8") == true);
    }

    [Fact]
    public void TemplatesContainBlockWindowsTelemetry()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-windows-telemetry");

        // Assert
        Assert.NotNull(template);
        Assert.NotNull(template.Warning); // Telemetry template should have a warning

        var policy = template.CreatePolicy();
        Assert.NotEmpty(policy.Rules);
    }

    [Fact]
    public void TemplatesContainBlockSocialMedia()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-social-media");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("Productivity", template.Category);

        var policy = template.CreatePolicy();
        Assert.NotEmpty(policy.Rules);
    }

    [Fact]
    public void TemplatesContainBlockAllTraffic()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-all-traffic");

        // Assert
        Assert.NotNull(template);
        Assert.NotNull(template.Warning); // Should have a warning
        Assert.Equal("Security", template.Category);

        var policy = template.CreatePolicy();
        Assert.Equal("block", policy.DefaultAction);
        // Should have loopback allowance
        Assert.Contains(policy.Rules, r => r.Action == "allow");
    }

    [Fact]
    public void TemplatesContainAllowWebBrowsingOnly()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "allow-web-only");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("Security", template.Category);

        var policy = template.CreatePolicy();
        Assert.Equal("block", policy.DefaultAction);
        // Should allow HTTP/HTTPS
        Assert.Contains(policy.Rules, r => r.Action == "allow" && r.Remote?.Ports?.Contains("443") == true);
        Assert.Contains(policy.Rules, r => r.Action == "allow" && r.Remote?.Ports?.Contains("80") == true);
    }

    [Fact]
    public void TemplatesContainDevelopmentLockdown()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "development-lockdown");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("Development", template.Category);

        var policy = template.CreatePolicy();
        Assert.Equal("block", policy.DefaultAction);
        // Should allow SSH for Git
        Assert.Contains(policy.Rules, r => r.Action == "allow" && r.Remote?.Ports?.Contains("22") == true);
    }

    [Fact]
    public void TemplatesContainBlockAdsAndTrackers()
    {
        // Act
        var templates = _provider.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "block-ads-trackers");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("Privacy", template.Category);

        var policy = template.CreatePolicy();
        Assert.NotEmpty(policy.Rules);
        Assert.All(policy.Rules, r => Assert.Equal("block", r.Action));
    }

    [Fact]
    public void AllTemplatesHaveValidPolicies()
    {
        // Act
        var templates = _provider.GetTemplates();

        // Assert
        foreach (var template in templates)
        {
            var policy = template.CreatePolicy();
            Assert.NotNull(policy);
            Assert.NotNull(policy.Version);
            Assert.NotNull(policy.DefaultAction);
            Assert.NotNull(policy.Rules);

            // All rules should have required fields
            foreach (var rule in policy.Rules)
            {
                Assert.False(string.IsNullOrWhiteSpace(rule.Id), $"Template {template.Id} has rule with empty Id");
                Assert.False(string.IsNullOrWhiteSpace(rule.Action), $"Template {template.Id} rule {rule.Id} has empty Action");
                Assert.False(string.IsNullOrWhiteSpace(rule.Direction), $"Template {template.Id} rule {rule.Id} has empty Direction");
                Assert.False(string.IsNullOrWhiteSpace(rule.Protocol), $"Template {template.Id} rule {rule.Id} has empty Protocol");
            }
        }
    }

    [Fact]
    public void AllTemplatesRulesHaveUniqueIds()
    {
        // Act
        var templates = _provider.GetTemplates();

        // Assert
        foreach (var template in templates)
        {
            var policy = template.CreatePolicy();
            var ruleIds = policy.Rules.Select(r => r.Id).ToList();
            Assert.True(ruleIds.Count == ruleIds.Distinct().Count(),
                $"Template {template.Id} has duplicate rule IDs");
        }
    }
}
