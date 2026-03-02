# UI Icons and Spacing - Visual Test Plan

**Feature**: Issues #5 & #11 - Visual Hierarchy and Spacing Standardization
**Test Type**: Manual Visual Inspection
**Test Date**: 2026-03-01
**Tester**: [To be filled by QA/User]

## Test Environment

### Prerequisites

- ✅ Build succeeded: `dotnet build WfpTrafficControl.UI.csproj` (0 errors)
- ✅ Windows 10+ (Segoe MDL2 Assets font guaranteed)
- ✅ .NET 8.0 Runtime installed
- ✅ WfpTrafficControl service installed and running

### Test System Specifications

| Component | Specification |
|-----------|--------------|
| OS | Windows 10/11 |
| Display | 1920x1080 or higher |
| DPI Scaling | 100%, 125%, 150% |
| Theme | Light & Dark modes |

---

## Test Suite 1: Icon Presence & Visibility

**Objective**: Verify all 34 buttons display Segoe MDL2 icons correctly.

### 1.1 DashboardView (9 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 1 | Apply Policy... | 📄 Page | Quick Actions | ☐ | |
| 2 | Rollback | ↩️ Undo | Quick Actions | ☐ | |
| 3 | Revert to LKG | 🕐 History | Quick Actions | ☐ | |
| 4 | Refresh | 🔄 Sync | Quick Actions | ☐ | |
| 5 | Validate JSON... | ✅ CheckMark | Quick Actions | ☐ | |
| 6 | Enable... | ▶️ Play | Hot Reload | ☐ | Visible when disabled |
| 7 | Disable | ⏸️ Pause | Hot Reload | ☐ | Visible when enabled |
| 8 | Re-initialize WFP | 🔄 Refresh | Maintenance | ☐ | Inside Expander |
| 9 | Teardown All | 🗑️ Delete | Maintenance | ☐ | Inside Expander |

**Pass Criteria**: All 9 buttons display icons, icons are visible and aligned with text.

---

### 1.2 PolicyEditorView (15 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 10 | New | 📁 NewFolder | Toolbar | ☐ | |
| 11 | Open | 📄 OpenFile | Toolbar | ☐ | |
| 12 | Save | 💾 Save | Toolbar | ☐ | |
| 13 | Save As... | 💾 SaveAs | Toolbar | ☐ | |
| 14 | Load (Template) | ⬇️ Download | Toolbar | ☐ | |
| 15 | Validate | ✅ CheckMark | Toolbar | ☐ | |
| 16 | Apply to Service | 📄 Page | Toolbar | ☐ | |
| 17 | New Policy | 📁 NewFolder | No Policy State | ☐ | When no policy loaded |
| 18 | Open Policy | 📄 OpenFile | No Policy State | ☐ | When no policy loaded |
| 19 | Load Template | ⬇️ Download | Template Cards | ☐ | Multiple cards |
| 20 | Copy | 📋 Copy | Rule Actions | ☐ | |
| 21 | Up | ⬆️ UpArrow | Rule Actions | ☐ | |
| 22 | Down | ⬇️ DownArrow | Rule Actions | ☐ | |
| 23 | ... (Browse) | 📁 Folder | Process Picker | ☐ | Icon-only button |
| 24 | Pick | 🔍 Search | Process Picker | ☐ | |

**Pass Criteria**: All 15 buttons display icons, template cards show icons consistently.

---

### 1.3 LogsView (3 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 25 | Refresh | 🔄 Sync | Toolbar | ☐ | |
| 26 | Clear Filter | ❌ ClearFilter | Toolbar | ☐ | |
| 27 | Export to CSV | 💾 Save | Toolbar | ☐ | |

**Pass Criteria**: All 3 buttons display icons.

---

### 1.4 BlockRulesView (2 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 28 | Refresh | 🔄 Sync | Toolbar | ☐ | |
| 29 | Copy Rule | 📋 Copy | Toolbar | ☐ | |

**Pass Criteria**: Both buttons display icons.

---

### 1.5 ConnectionMonitorView (3 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 30 | Refresh | 🔄 Sync | Toolbar | ☐ | |
| 31 | Copy | 📋 Copy | Toolbar | ☐ | |
| 32 | Clear Filters | ❌ ClearFilter | Filters | ☐ | |

**Pass Criteria**: All 3 buttons display icons.

---

### 1.6 AnalyticsDashboardView (2 Buttons)

| # | Button Text | Expected Icon | Location | ✅/❌ | Notes |
|---|------------|--------------|----------|-------|-------|
| 33 | Collect Now | 🔄 Sync | Toolbar | ☐ | |
| 34 | Clear Data | 🗑️ Delete | Toolbar | ☐ | |

**Pass Criteria**: Both buttons display icons.

---

## Test Suite 2: Spacing & Alignment

**Objective**: Verify 8-point grid spacing is correctly applied.

### 2.1 Icon-Text Spacing (8px)

**Test Steps**:
1. Open browser DevTools (use Snoop for WPF)
2. Inspect any button with icon
3. Measure margin between icon TextBlock and text TextBlock

**Expected**: `Margin="0,0,8,0"` on icon TextBlock

| View | Sample Button | Spacing Correct | ✅/❌ |
|------|--------------|-----------------|-------|
| Dashboard | Apply Policy | 8px | ☐ |
| PolicyEditor | Save | 8px | ☐ |
| Logs | Refresh | 8px | ☐ |
| BlockRules | Copy Rule | 8px | ☐ |
| ConnectionMonitor | Clear Filters | 8px | ☐ |
| AnalyticsDashboard | Collect Now | 8px | ☐ |

**Pass Criteria**: All sampled buttons have 8px icon-text spacing.

---

### 2.2 Button Horizontal Spacing (12px)

**Test Steps**:
1. Visually inspect button groups
2. Verify consistent gaps between adjacent buttons
3. Use Snoop to measure `Margin` property

**Expected**: `Margin="0,0,12,0"` or `Margin="0,0,8,0"` for tight groups

| View | Button Group | Spacing Correct | ✅/❌ |
|------|-------------|-----------------|-------|
| Dashboard | Quick Actions (5 buttons) | 12px | ☐ |
| PolicyEditor | Toolbar (7 buttons) | 8-12px | ☐ |
| Logs | Toolbar (3 buttons) | 8-12px | ☐ |

**Pass Criteria**: Consistent horizontal spacing, no visual crowding.

---

## Test Suite 3: Theme Compatibility

**Objective**: Verify icons work in both light and dark themes.

### 3.1 Light Theme

**Test Steps**:
1. Launch application in light theme
2. Navigate to all 6 views
3. Verify all icons are visible (dark color on light background)

| View | All Icons Visible | ✅/❌ | Issues |
|------|------------------|-------|--------|
| Dashboard | ☐ | | |
| PolicyEditor | ☐ | | |
| Logs | ☐ | | |
| BlockRules | ☐ | | |
| ConnectionMonitor | ☐ | | |
| AnalyticsDashboard | ☐ | | |

---

### 3.2 Dark Theme

**Test Steps**:
1. Toggle to dark theme (Settings > Theme or theme toggle button)
2. Navigate to all 6 views
3. Verify all icons are visible (light color on dark background)

| View | All Icons Visible | ✅/❌ | Issues |
|------|------------------|-------|--------|
| Dashboard | ☐ | | |
| PolicyEditor | ☐ | | |
| Logs | ☐ | | |
| BlockRules | ☐ | | |
| ConnectionMonitor | ☐ | | |
| AnalyticsDashboard | ☐ | | |

**Pass Criteria**: Icons inherit text color and are visible in both themes.

---

## Test Suite 4: LoadingOverlay Consistency

**Objective**: Verify LoadingOverlayStyle is applied to all loading states.

### 4.1 LoadingOverlay Visual Check

**Test Steps**:
1. Trigger loading states in each view
2. Verify overlay appearance (95% opacity, 24px padding)
3. Check loading text is centered

| View | Trigger Action | Overlay Appears | Styling Correct | ✅/❌ |
|------|---------------|----------------|-----------------|-------|
| Dashboard | Click Refresh | ☐ | ☐ | ☐ |
| PolicyEditor | Open Policy | ☐ | ☐ | ☐ |
| Logs | Click Refresh | ☐ | ☐ | ☐ |
| BlockRules | Click Refresh | ☐ | ☐ | ☐ |
| ConnectionMonitor | Click Refresh | ☐ | ☐ | ☐ |
| AnalyticsDashboard | Click Collect Now | ☐ | ☐ | ☐ |

**Pass Criteria**: All views show consistent loading overlay with LoadingOverlayStyle.

---

## Test Suite 5: Accessibility

**Objective**: Verify icons don't break screen reader functionality.

### 5.1 Screen Reader Test (Narrator)

**Test Steps**:
1. Enable Windows Narrator (Win + Ctrl + Enter)
2. Navigate to buttons using Tab key
3. Verify Narrator reads button text (not icon Unicode)

| View | Button | Narrator Reads Text | ✅/❌ |
|------|--------|---------------------|-------|
| Dashboard | Apply Policy | "Apply Policy..." | ☐ |
| PolicyEditor | Save | "Save" | ☐ |
| Logs | Export to CSV | "Export to CSV" | ☐ |

**Pass Criteria**: Screen readers announce button text correctly, icons are supplementary.

---

### 5.2 Keyboard Navigation

**Test Steps**:
1. Navigate buttons using Tab key
2. Verify focus indicators are visible
3. Activate buttons using Enter/Space

| View | Keyboard Navigation Works | Focus Visible | ✅/❌ |
|------|--------------------------|---------------|-------|
| Dashboard | ☐ | ☐ | ☐ |
| PolicyEditor | ☐ | ☐ | ☐ |
| Logs | ☐ | ☐ | ☐ |

**Pass Criteria**: All buttons are keyboard accessible, focus indicators visible.

---

## Test Suite 6: DPI Scaling

**Objective**: Verify icons render correctly at different DPI scales.

### 6.1 DPI Scaling Test

**Test Steps**:
1. Change Windows display scaling (Settings > Display > Scale)
2. Restart application
3. Verify icons are not pixelated or clipped

| DPI Scale | Icons Sharp | Text Aligned | ✅/❌ | Notes |
|-----------|------------|-------------|-------|-------|
| 100% | ☐ | ☐ | ☐ | |
| 125% | ☐ | ☐ | ☐ | |
| 150% | ☐ | ☐ | ☐ | |
| 200% | ☐ | ☐ | ☐ | High DPI laptops |

**Pass Criteria**: Icons remain sharp and aligned at all DPI scales (Segoe MDL2 is a vector font).

---

## Test Suite 7: Cross-View Consistency

**Objective**: Verify same action uses same icon across all views.

### 7.1 Icon Consistency Check

| Action | Expected Icon | Views | Consistent | ✅/❌ |
|--------|--------------|-------|-----------|-------|
| Refresh | 🔄 Sync (\uE72C) | Dashboard, Logs, BlockRules, ConnectionMonitor, AnalyticsDashboard | ☐ | ☐ |
| Copy | 📋 Copy (\uE8C8) | PolicyEditor, BlockRules, ConnectionMonitor | ☐ | ☐ |
| Delete/Clear | 🗑️ Delete (\uE74D) | Dashboard, AnalyticsDashboard | ☐ | ☐ |
| Save | 💾 Save (\uE74E) | PolicyEditor, Logs | ☐ | ☐ |

**Pass Criteria**: Same actions use identical icons across all views.

---

## Summary & Sign-Off

### Test Results

| Test Suite | Passed | Failed | Notes |
|-----------|--------|--------|-------|
| 1. Icon Presence (34 buttons) | ☐ | ☐ | |
| 2. Spacing & Alignment | ☐ | ☐ | |
| 3. Theme Compatibility | ☐ | ☐ | |
| 4. LoadingOverlay Consistency | ☐ | ☐ | |
| 5. Accessibility | ☐ | ☐ | |
| 6. DPI Scaling | ☐ | ☐ | |
| 7. Cross-View Consistency | ☐ | ☐ | |

### Overall Result

- [ ] ✅ **PASS** - All tests passed, ready for production
- [ ] ⚠️ **PASS WITH NOTES** - Minor issues documented, acceptable for production
- [ ] ❌ **FAIL** - Critical issues found, requires fixes before release

### Tester Sign-Off

| Field | Value |
|-------|-------|
| Tester Name | |
| Test Date | |
| Environment | |
| Build Version | |
| Overall Result | ☐ PASS / ☐ FAIL |
| Signature | |

### Issues Found (if any)

| Issue # | Description | Severity | View | Status |
|---------|------------|----------|------|--------|
| | | | | |

---

## Automated Build Verification

**Run before manual testing**:

```bash
cd "c:\Users\nrhin\OneDrive\Documents\Github Repos\nicv2"
dotnet build src/ui/WfpTrafficControl.UI/WfpTrafficControl.UI.csproj
```

**Expected**: ✅ Build succeeded (0 errors)

---

## Notes for Testers

1. **Segoe MDL2 Assets**: Emoji representations (🔄, 💾, etc.) in this document are for illustration only. Actual icons are monochrome glyphs from Segoe MDL2 Assets font.

2. **Icon-Only Buttons**: The "..." Browse button in PolicyEditor is icon-only (no text). Verify the folder icon is centered.

3. **Template Cards**: PolicyEditorView shows multiple "Load Template" buttons in template cards - verify all have identical icons.

4. **Hot Reload Buttons**: Enable/Disable buttons in DashboardView are mutually exclusive - only one is visible at a time.

5. **Maintenance Section**: Re-initialize WFP and Teardown All are inside an Expander - expand "Maintenance (Advanced)" to test them.

6. **Theme Toggle**: If theme toggle button is in MainWindow header, use that for quick theme switching during testing.
