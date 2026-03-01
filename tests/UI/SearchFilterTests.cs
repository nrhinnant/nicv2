using System.ComponentModel;
using System.Windows.Data;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for search and filter functionality across all ViewModels.
/// </summary>
public class SearchFilterTests
{
    #region PolicyEditorViewModel Search Tests

    [Fact]
    public void PolicyEditor_SearchText_FiltersRulesByIdCaseInsensitive()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-http", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-HTTPS", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "block-all", Action = "block" });

        // Act
        vm.SearchText = "http";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, r => r.Id == "rule-http");
        Assert.Contains(filtered, r => r.Id == "rule-HTTPS");
    }

    [Fact]
    public void PolicyEditor_SearchText_FiltersRulesByProcess()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Process = @"C:\Program Files\Chrome\chrome.exe" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Process = @"C:\Program Files\Firefox\firefox.exe" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3", Process = null });

        // Act
        vm.SearchText = "chrome";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rule-1", filtered[0].Id);
    }

    [Fact]
    public void PolicyEditor_SearchText_FiltersRulesByRemoteIp()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", RemoteIp = "192.168.1.0/24" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", RemoteIp = "10.0.0.0/8" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3", RemoteIp = "192.168.2.100" });

        // Act
        vm.SearchText = "192.168";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void PolicyEditor_SearchText_FiltersRulesByComment()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Comment = "Allow web browsing" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Comment = "Block telemetry" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3", Comment = null });

        // Act
        vm.SearchText = "web";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rule-1", filtered[0].Id);
    }

    [Fact]
    public void PolicyEditor_ActionFilter_FiltersRulesByAction()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Action = "block" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3", Action = "allow" });

        // Act
        vm.ActionFilter = "block";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rule-2", filtered[0].Id);
    }

    [Fact]
    public void PolicyEditor_CombinedSearchAndFilter_WorksTogether()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "http-allow", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "http-block", Action = "block" });
        vm.Rules.Add(new RuleViewModel { Id = "dns-allow", Action = "allow" });

        // Act
        vm.ActionFilter = "allow";
        vm.SearchText = "http";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Single(filtered);
        Assert.Equal("http-allow", filtered[0].Id);
    }

    [Fact]
    public void PolicyEditor_FilteredRuleCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Action = "block" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3", Action = "allow" });

        // Act
        vm.ActionFilter = "allow";

        // Assert
        Assert.Equal(2, vm.FilteredRuleCount);
        Assert.Equal(3, vm.Rules.Count);
    }

    [Fact]
    public void PolicyEditor_ClearSearch_ResetsSearchAndFilter()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Action = "allow" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Action = "block" });

        vm.SearchText = "test";
        vm.ActionFilter = "allow";

        // Act
        vm.ClearSearchCommand.Execute(null);

        // Assert
        Assert.Equal("", vm.SearchText);
        Assert.Equal("all", vm.ActionFilter);
    }

    #endregion

    #region LogsViewModel Search Tests

    [Fact]
    public void Logs_SearchText_FiltersLogsByEvent()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new LogsViewModel(mockClient, mockDialog);

        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-01", Event = "apply", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-02", Event = "rollback", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-03", Event = "apply", Status = "failure" });

        // Act
        vm.SearchText = "rollback";

        // Assert
        var filtered = vm.LogEntriesView.Cast<AuditLogEntryDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rollback", filtered[0].Event);
    }

    [Fact]
    public void Logs_EventFilter_FiltersLogsByEventType()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new LogsViewModel(mockClient, mockDialog);

        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-01", Event = "apply", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-02", Event = "rollback", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-03", Event = "startup", Status = "success" });

        // Act
        vm.EventFilter = "apply";

        // Assert
        var filtered = vm.LogEntriesView.Cast<AuditLogEntryDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("apply", filtered[0].Event);
    }

    [Fact]
    public void Logs_StatusFilter_FiltersLogsByStatus()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new LogsViewModel(mockClient, mockDialog);

        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-01", Event = "apply", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-02", Event = "apply", Status = "failure" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-03", Event = "apply", Status = "success" });

        // Act
        vm.StatusFilter = "failure";

        // Assert
        var filtered = vm.LogEntriesView.Cast<AuditLogEntryDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("failure", filtered[0].Status);
    }

    [Fact]
    public void Logs_CombinedFilters_WorksTogether()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new LogsViewModel(mockClient, mockDialog);

        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-01", Event = "apply", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-02", Event = "apply", Status = "failure" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-03", Event = "rollback", Status = "failure" });

        // Act
        vm.EventFilter = "apply";
        vm.StatusFilter = "failure";

        // Assert
        var filtered = vm.LogEntriesView.Cast<AuditLogEntryDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("apply", filtered[0].Event);
        Assert.Equal("failure", filtered[0].Status);
    }

    [Fact]
    public void Logs_FilteredLogCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new LogsViewModel(mockClient, mockDialog);

        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-01", Event = "apply", Status = "success" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-02", Event = "apply", Status = "failure" });
        vm.LogEntries.Add(new AuditLogEntryDto { Timestamp = "2024-01-03", Event = "apply", Status = "success" });

        // Act
        vm.StatusFilter = "success";

        // Assert
        Assert.Equal(2, vm.FilteredLogCount);
        Assert.Equal(3, vm.LogEntries.Count);
    }

    #endregion

    #region BlockRulesViewModel Search Tests

    [Fact]
    public void BlockRules_SearchText_FiltersRulesById()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "block-chrome", Direction = "outbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "block-telemetry", Direction = "outbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "block-chrome-update", Direction = "outbound" });

        // Act
        vm.SearchText = "chrome";

        // Assert
        var filtered = vm.BlockRulesView.Cast<BlockRuleDto>().ToList();
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void BlockRules_DirectionFilter_FiltersRulesByDirection()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-1", Direction = "inbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-2", Direction = "outbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-3", Direction = "both" });

        // Act
        vm.DirectionFilter = "inbound";

        // Assert
        var filtered = vm.BlockRulesView.Cast<BlockRuleDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rule-1", filtered[0].Id);
    }

    [Fact]
    public void BlockRules_ProtocolFilter_FiltersRulesByProtocol()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-1", Direction = "outbound", Protocol = "tcp" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-2", Direction = "outbound", Protocol = "udp" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-3", Direction = "outbound", Protocol = "any" });

        // Act
        vm.ProtocolFilter = "tcp";

        // Assert
        var filtered = vm.BlockRulesView.Cast<BlockRuleDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("rule-1", filtered[0].Id);
    }

    [Fact]
    public void BlockRules_CombinedFilters_WorksTogether()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "tcp-out", Direction = "outbound", Protocol = "tcp" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "tcp-in", Direction = "inbound", Protocol = "tcp" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "udp-out", Direction = "outbound", Protocol = "udp" });

        // Act
        vm.DirectionFilter = "outbound";
        vm.ProtocolFilter = "tcp";

        // Assert
        var filtered = vm.BlockRulesView.Cast<BlockRuleDto>().ToList();
        Assert.Single(filtered);
        Assert.Equal("tcp-out", filtered[0].Id);
    }

    [Fact]
    public void BlockRules_FilteredRuleCount_ReturnsCorrectCount()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-1", Direction = "outbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-2", Direction = "inbound" });
        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-3", Direction = "outbound" });

        // Act
        vm.DirectionFilter = "outbound";

        // Assert
        Assert.Equal(2, vm.FilteredRuleCount);
        Assert.Equal(3, vm.BlockRules.Count);
    }

    [Fact]
    public void BlockRules_ClearSearch_ResetsAllFilters()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var vm = new BlockRulesViewModel(mockClient);

        vm.BlockRules.Add(new BlockRuleDto { Id = "rule-1", Direction = "outbound", Protocol = "tcp" });

        vm.SearchText = "test";
        vm.DirectionFilter = "outbound";
        vm.ProtocolFilter = "tcp";

        // Act
        vm.ClearSearchCommand.Execute(null);

        // Assert
        Assert.Equal("", vm.SearchText);
        Assert.Equal("all", vm.DirectionFilter);
        Assert.Equal("all", vm.ProtocolFilter);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void PolicyEditor_EmptySearchText_ShowsAllRules()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-3" });

        // Act
        vm.SearchText = "";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public void PolicyEditor_WhitespaceOnlySearch_ShowsAllRules()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2" });

        // Act
        vm.SearchText = "   ";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void PolicyEditor_NoMatchingResults_ReturnsEmpty()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel { Id = "rule-1", Comment = "test" });
        vm.Rules.Add(new RuleViewModel { Id = "rule-2", Comment = "example" });

        // Act
        vm.SearchText = "nonexistent";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Empty(filtered);
        Assert.Equal(0, vm.FilteredRuleCount);
    }

    [Fact]
    public void PolicyEditor_NullFieldsDoNotCauseException()
    {
        // Arrange
        var mockClient = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var mockTemplates = new PolicyTemplateProvider();
        var vm = new PolicyEditorViewModel(mockClient, mockDialog, mockTemplates);

        vm.Rules.Add(new RuleViewModel
        {
            Id = "rule-1",
            Process = null,
            RemoteIp = null,
            RemotePorts = null,
            LocalIp = null,
            LocalPorts = null,
            Comment = null
        });

        // Act - should not throw
        vm.SearchText = "test";

        // Assert
        var filtered = vm.RulesView.Cast<RuleViewModel>().ToList();
        Assert.Empty(filtered);
    }

    #endregion
}
