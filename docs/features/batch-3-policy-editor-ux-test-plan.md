# Policy Editor UX Improvements - Test Plan

**Feature**: Issues #7, #8, #13 - Policy Editor UX Enhancements
**Test Type**: Manual Visual and Functional Testing
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

## Test Suite 1: Delete Rule Confirmation Dialog (Issue #8)

**Objective**: Verify custom delete confirmation dialog displays complete rule details.

### 1.1 Dialog Appearance

**Test Steps**:
1. Launch WfpTrafficControl.UI
2. Navigate to Policy Editor tab
3. Open or create a policy with at least 3 rules
4. Select a rule with all fields populated (process, remote/local endpoints, comment)
5. Click the **Delete** button (trash icon) or right-click → Delete

**Expected Results**:

| Item | Expected | ✅/❌ | Notes |
|------|----------|-------|-------|
| Dialog opens | 500x350px window, centered on main window | ☐ | |
| Warning header | Yellow/orange background with warning icon (ℹ) | ☐ | |
| Warning text | "Delete Firewall Rule" title and description | ☐ | |
| Rule ID displayed | Shows complete rule ID | ☐ | |
| Action displayed | Shows "allow" or "block" | ☐ | |
| Direction displayed | Shows "inbound", "outbound", or "both" | ☐ | |
| Protocol displayed | Shows "tcp", "udp", or "any" | ☐ | |
| Process displayed | Shows full path or "Any" if empty | ☐ | |
| Remote endpoint | Shows "IP:Ports" or "Any" if not specified | ☐ | |
| Local endpoint | Shows "IP:Ports" or "Any" if not specified | ☐ | |
| Priority displayed | Shows numeric priority value | ☐ | |
| Comment displayed | Shows comment text or "(none)" if empty | ☐ | |
| Cancel button | Visible, default focus (highlighted) | ☐ | |
| Delete button | Visible, red/danger style, delete icon (🗑️) | ☐ | |

**Pass Criteria**: All 14 items display correctly.

---

### 1.2 Dialog Functionality

**Test Steps**:
1. With dialog open from 1.1
2. Click **Cancel** button
3. Verify dialog closes and rule is NOT deleted
4. Click **Delete** button again
5. Press **Esc** key
6. Verify dialog closes and rule is NOT deleted
7. Click **Delete** button again
8. Click **Delete** button
9. Verify dialog closes and rule IS deleted

| Test Case | Expected Behavior | ✅/❌ | Notes |
|-----------|------------------|-------|-------|
| Click Cancel | Dialog closes, rule remains | ☐ | |
| Press Esc | Dialog closes, rule remains | ☐ | |
| Click Delete | Dialog closes, rule removed from list | ☐ | |
| Undo available | Undo button becomes enabled after delete | ☐ | |

**Pass Criteria**: All 4 test cases pass.

---

### 1.3 Edge Cases

**Test Steps**:
1. Create a rule with minimal fields (only ID, action, direction, protocol)
2. Delete the rule
3. Verify dialog shows "Any" for empty fields
4. Create a rule with very long comment (>200 characters)
5. Delete the rule
6. Verify comment is fully visible with scrolling

| Edge Case | Expected Behavior | ✅/❌ | Notes |
|-----------|------------------|-------|-------|
| Minimal rule | Shows "Any" for Process, Remote, Local | ☐ | |
| Long comment | ScrollViewer allows scrolling to see full text | ☐ | |
| Rule with special characters | Comment displays correctly (no escaping issues) | ☐ | |

**Pass Criteria**: All 3 edge cases handled correctly.

---

## Test Suite 2: Priority Helper & Context (Issue #7)

**Objective**: Verify priority badges and evaluation order context display correctly.

### 2.1 Priority Badge Appearance

**Test Steps**:
1. Create a policy with 6 rules having priorities: 50, 100, 150, 200, 250, 300
2. Navigate to Policy Editor and observe rule list

**Expected Results**:

| Priority Value | Expected Badge Color | ✅/❌ | Notes |
|----------------|---------------------|-------|-------|
| 300 | Green (#4CAF50) | ☐ | High priority |
| 250 | Green (#4CAF50) | ☐ | High priority |
| 200 | Green (#4CAF50) | ☐ | High priority threshold |
| 150 | Orange (#FF9800) | ☐ | Medium priority |
| 100 | Orange (#FF9800) | ☐ | Medium priority threshold |
| 50 | Red (#F44336) | ☐ | Low priority |

**Badge Format**:
- Each badge should show: `#{position} (P:{priority})`
- Example: "#1 (P:300)", "#2 (P:250)", etc.

| Rule Priority | Expected Badge Text | ✅/❌ | Notes |
|---------------|-------------------|-------|-------|
| 300 | #1 (P:300) | ☐ | Evaluated first |
| 250 | #2 (P:250) | ☐ | |
| 200 | #3 (P:200) | ☐ | |
| 150 | #4 (P:150) | ☐ | |
| 100 | #5 (P:100) | ☐ | |
| 50 | #6 (P:50) | ☐ | Evaluated last |

**Pass Criteria**: All badges display with correct colors and format.

---

### 2.2 Priority Context Panel

**Test Steps**:
1. Using policy from 2.1, select the rule with priority 300
2. Observe the Priority section in the detail panel (right side)

**Expected Results**:

| Element | Expected Content | ✅/❌ | Notes |
|---------|-----------------|-------|-------|
| Priority text box | Shows "300" | ☐ | Editable |
| Information box | Blue/info background, 8px padding | ☐ | |
| "Evaluation Order" header | Bold, primary text color | ☐ | |
| Context text | "Evaluated FIRST" | ☐ | Special case for #1 |
| After rule | Not shown (first rule) | ☐ | |
| Before rule | "Before: {rule-id-of-priority-250}" | ☐ | |
| Help text | "Higher priority rules are evaluated first..." | ☐ | Italic, secondary color |

**Test Steps** (continued):
3. Select the rule with priority 50 (last rule)
4. Verify context shows "Evaluated LAST"

| Element | Expected Content | ✅/❌ | Notes |
|---------|-----------------|-------|-------|
| Context text | "Evaluated LAST" | ☐ | Special case for last |
| After rule | "After: {rule-id-of-priority-100}" | ☐ | |
| Before rule | Not shown (last rule) | ☐ | |

**Test Steps** (continued):
5. Select a middle rule (priority 150, position #4)
6. Verify context shows position and neighboring rules

| Element | Expected Content | ✅/❌ | Notes |
|---------|-----------------|-------|-------|
| Context text | "Evaluated 4 of 6" | ☐ | Middle position |
| After rule | "After: {rule-id-of-priority-200}" | ☐ | |
| Before rule | "Before: {rule-id-of-priority-100}" | ☐ | |

**Pass Criteria**: All context elements display correctly for first/middle/last rules.

---

### 2.3 Dynamic Priority Updates

**Test Steps**:
1. Select a rule and change its priority from 150 to 350
2. Observe badge and context updates

| Action | Expected Result | ✅/❌ | Notes |
|--------|----------------|-------|-------|
| Badge color changes | Changes from orange to green | ☐ | Real-time update |
| Badge position updates | Shows "#1" (now highest priority) | ☐ | |
| Context updates | Shows "Evaluated FIRST" | ☐ | |
| Other badges renumber | All other badges shift (#2, #3, etc.) | ☐ | Cascade update |

**Test Steps** (continued):
3. Move a rule up in the list (click Up arrow button)
4. Verify ordinal positions recalculate

| Action | Expected Result | ✅/❌ | Notes |
|--------|----------------|-------|-------|
| Moved rule renumbers | Ordinal position changes | ☐ | |
| Context updates | Before/After rules change | ☐ | |
| Other rules renumber | Adjacent rules update positions | ☐ | |

**Pass Criteria**: All dynamic updates occur immediately without needing to refresh.

---

### 2.4 Priority Context with Equal Priorities

**Test Steps**:
1. Create 3 rules with identical priority 100
2. Observe how evaluation order is determined

| Scenario | Expected Behavior | ✅/❌ | Notes |
|----------|------------------|-------|-------|
| Equal priorities | Ordinal position determined by list order | ☐ | Top rule = lower ordinal |
| Badge displays | Shows different ordinal numbers (#X, #Y, #Z) | ☐ | |
| Context shows neighbors | Correctly identifies before/after rules | ☐ | |
| Help text visible | Explains "Equal priorities use list order" | ☐ | |

**Pass Criteria**: Equal priorities handled correctly using list order as tiebreaker.

---

## Test Suite 3: Undo/Redo (Issue #13)

**Objective**: Verify undo/redo functionality for all rule operations.

### 3.1 Undo/Redo Button States

**Test Steps**:
1. Create a new policy (New button)
2. Observe initial undo/redo button states

| State | Expected | ✅/❌ | Notes |
|-------|----------|-------|-------|
| Undo button | Disabled (grayed out) | ☐ | No history yet |
| Redo button | Disabled (grayed out) | ☐ | No redoable operations |

**Test Steps** (continued):
3. Add a new rule (Add button)
4. Observe button states

| State | Expected | ✅/❌ | Notes |
|-------|----------|-------|-------|
| Undo button | Enabled | ☐ | "Add Rule" operation |
| Undo tooltip | "Undo (Add Rule) (Ctrl+Z)" | ☐ | Descriptive text |
| Redo button | Still disabled | ☐ | No redo history |

**Pass Criteria**: Button states and tooltips reflect current undo/redo availability.

---

### 3.2 Undo Operations

**Test Steps**:
1. Starting with policy from 3.1, perform the following operations in sequence:
   - Delete a rule
   - Add a new rule
   - Move a rule up
   - Copy a rule
   - Change a rule's priority
2. After each operation, verify undo button tooltip updates

| Operation | Undo Tooltip Expected | ✅/❌ | Notes |
|-----------|---------------------|-------|-------|
| After delete | "Undo (Delete Rule: {id})" | ☐ | Shows deleted rule ID |
| After add | "Undo (Add Rule)" | ☐ | |
| After move up | "Undo (Move Rule Up: {id})" | ☐ | |
| After copy | "Undo (Copy Rule: {id})" | ☐ | |

**Test Steps** (continued):
3. Click **Undo** button (or press Ctrl+Z)
4. Verify last operation (copy) is undone

| Verification | Expected Result | ✅/❌ | Notes |
|--------------|----------------|-------|-------|
| Copied rule removed | Rule disappears from list | ☐ | |
| Redo button enabled | Button becomes clickable | ☐ | |
| Redo tooltip | "Redo (Copy Rule: {id})" | ☐ | |
| Undo tooltip updates | Shows previous operation (Move Rule Up) | ☐ | |

**Test Steps** (continued):
5. Click **Undo** 4 more times
6. Verify all operations undone in reverse order

| Undo Count | Expected State | ✅/❌ | Notes |
|------------|---------------|-------|-------|
| Undo 1 | Priority change reverted | ☐ | |
| Undo 2 | Rule move reverted | ☐ | |
| Undo 3 | Added rule removed | ☐ | |
| Undo 4 | Deleted rule restored | ☐ | |
| Undo button disabled | No more operations to undo | ☐ | |

**Pass Criteria**: All operations undo correctly in reverse order.

---

### 3.3 Redo Operations

**Test Steps**:
1. From end of 3.2, click **Redo** button 4 times
2. Verify all operations re-applied in original order

| Redo Count | Expected State | ✅/❌ | Notes |
|------------|---------------|-------|-------|
| Redo 1 | Deleted rule removed again | ☐ | |
| Redo 2 | Added rule reappears | ☐ | |
| Redo 3 | Rule moved back up | ☐ | |
| Redo 4 | Priority change re-applied | ☐ | |
| Redo 5 | Copied rule reappears | ☐ | |
| Redo button disabled | No more operations to redo | ☐ | |

**Pass Criteria**: All operations redo correctly, restoring to exact previous state.

---

### 3.4 Redo Stack Clearing

**Test Steps**:
1. From end of 3.3, click **Undo** 2 times
2. Verify redo button is enabled
3. Make a new change (e.g., delete a rule)
4. Verify redo button becomes disabled

| Action | Expected Result | ✅/❌ | Notes |
|--------|----------------|-------|-------|
| After undo | Redo button enabled | ☐ | Can redo 2 operations |
| After new change | Redo button disabled | ☐ | Redo stack cleared |
| Undo button | Still enabled | ☐ | New change added to undo stack |

**Pass Criteria**: Redo stack properly cleared on new user action.

---

### 3.5 Keyboard Shortcuts

**Test Steps**:
1. Create a policy with 3 rules
2. Add a new rule
3. Press **Ctrl+Z**
4. Verify added rule is removed (undo worked)
5. Press **Ctrl+Y**
6. Verify added rule reappears (redo worked)

| Shortcut | Expected Result | ✅/❌ | Notes |
|----------|----------------|-------|-------|
| Ctrl+Z | Undoes last operation | ☐ | Same as clicking Undo button |
| Ctrl+Y | Redoes last undone operation | ☐ | Same as clicking Redo button |
| Ctrl+Z when disabled | No action | ☐ | Does not error |
| Ctrl+Y when disabled | No action | ☐ | Does not error |

**Pass Criteria**: Keyboard shortcuts work identically to button clicks.

---

### 3.6 Undo/Redo with Full State Restoration

**Test Steps**:
1. Create a policy with specific configuration:
   - Policy version: "2.0.0"
   - Default action: "deny"
   - 3 rules with specific IDs, priorities, and fields
2. Change policy version to "2.1.0"
3. Change default action to "allow"
4. Delete 1 rule
5. Modify priority of another rule
6. Click **Undo** until back to original state
7. Verify ALL state restored:
   - Policy version back to "2.0.0"
   - Default action back to "deny"
   - All 3 original rules present
   - Rule priorities unchanged

| State Element | Restored Correctly | ✅/❌ | Notes |
|---------------|-------------------|-------|-------|
| Policy version | ☐ | | Should be "2.0.0" |
| Default action | ☐ | | Should be "deny" |
| Rule count | ☐ | | Should be 3 |
| Rule IDs | ☐ | | Original IDs preserved |
| Rule priorities | ☐ | | Original values restored |
| Rule enabled states | ☐ | | Preserved |
| Rule endpoints | ☐ | | All fields restored |

**Pass Criteria**: Complete policy state restored, not just rule list.

---

### 3.7 Undo Stack Size Limit

**Test Steps**:
1. Create a policy with 1 rule
2. Perform 52 operations:
   - Add 25 new rules
   - Delete 15 rules
   - Move 12 rules
3. Verify undo button is still enabled
4. Click **Undo** button repeatedly until disabled
5. Count number of undo operations

| Metric | Expected Value | Actual Value | ✅/❌ | Notes |
|--------|---------------|--------------|-------|-------|
| Max undo operations | 50 | | ☐ | Oldest 2 operations discarded |
| Undo button behavior | Disables after 50 undos | | ☐ | |
| No errors | No crashes or exceptions | | ☐ | |

**Pass Criteria**: Stack size limited to 50 operations, oldest operations discarded.

---

## Test Suite 4: Cross-Feature Integration

**Objective**: Verify features work together correctly.

### 4.1 Delete + Undo Integration

**Test Steps**:
1. Create policy with 3 rules
2. Delete a rule using custom confirmation dialog
3. Verify undo button enabled
4. Click **Undo**
5. Verify deleted rule reappears with all data intact

| Verification | Expected Result | ✅/❌ | Notes |
|--------------|----------------|-------|-------|
| Delete dialog shows | Custom dialog with rule details | ☐ | |
| After delete, undo enabled | "Undo (Delete Rule: {id})" | ☐ | |
| After undo | Deleted rule fully restored | ☐ | |
| Rule data preserved | All fields match original | ☐ | ID, action, endpoints, etc. |

**Pass Criteria**: Delete and undo work together seamlessly.

---

### 4.2 Priority Change + Undo Integration

**Test Steps**:
1. Create policy with 3 rules (priorities 100, 200, 300)
2. Change middle rule priority from 200 to 350
3. Observe priority badges update (becomes #1)
4. Click **Undo**
5. Verify priority reverts to 200 and badges update

| Verification | Expected Result | ✅/❌ | Notes |
|--------------|----------------|-------|-------|
| Priority change | Badge changes to green, #1 | ☐ | |
| Other badges renumber | #1 becomes #2, #2 becomes #3 | ☐ | |
| After undo | Priority back to 200, orange badge | ☐ | |
| Badges renumber back | Original ordinal positions restored | ☐ | |

**Pass Criteria**: Priority helper updates correctly after undo.

---

### 4.3 Multiple Operations + Full Undo/Redo Cycle

**Test Steps**:
1. Create policy with 2 rules
2. Perform complex sequence:
   - Add rule
   - Change priority of new rule
   - Delete original rule
   - Move remaining rule
   - Copy rule
3. Undo all 5 operations
4. Redo all 5 operations
5. Verify end state matches state before first undo

| Phase | Expected Result | ✅/❌ | Notes |
|-------|----------------|-------|-------|
| After all operations | 3 rules with specific configuration | ☐ | |
| After full undo | Back to original 2 rules | ☐ | |
| After full redo | 3 rules, matching pre-undo state | ☐ | |
| Priority badges | Correct throughout cycle | ☐ | Update at each step |

**Pass Criteria**: Full undo/redo cycle restores exact state.

---

## Test Suite 5: Theme Compatibility

**Objective**: Verify features work in light and dark themes.

### 5.1 Light Theme

**Test Steps**:
1. Set application to light theme
2. Open delete confirmation dialog
3. Verify all text is readable (dark on light)
4. Check priority badges
5. Check undo/redo buttons

| Element | Readable in Light Theme | ✅/❌ | Notes |
|---------|------------------------|-------|-------|
| Delete dialog header | Yellow background, dark text | ☐ | |
| Delete dialog content | White background, dark text | ☐ | |
| Priority badges | Colored background, white text | ☐ | |
| Priority context box | Light info background, dark text | ☐ | |
| Undo/Redo buttons | Standard button colors | ☐ | |

**Pass Criteria**: All elements readable and visually pleasing in light theme.

---

### 5.2 Dark Theme

**Test Steps**:
1. Set application to dark theme
2. Repeat tests from 5.1

| Element | Readable in Dark Theme | ✅/❌ | Notes |
|---------|----------------------|-------|-------|
| Delete dialog header | Orange background, dark text | ☐ | |
| Delete dialog content | Dark background, light text | ☐ | |
| Priority badges | Colored background, white text | ☐ | Same as light theme |
| Priority context box | Dark info background, light text | ☐ | |
| Undo/Redo buttons | Dark button colors | ☐ | |

**Pass Criteria**: All elements readable and visually pleasing in dark theme.

---

## Test Suite 6: Accessibility

**Objective**: Verify features are keyboard accessible.

### 6.1 Keyboard Navigation

**Test Steps**:
1. Open delete confirmation dialog
2. Verify Tab key navigates between Cancel and Delete buttons
3. Verify Enter key activates focused button
4. Verify Esc key closes dialog (canceling)

| Action | Expected Result | ✅/❌ | Notes |
|--------|----------------|-------|-------|
| Tab | Moves focus between Cancel/Delete | ☐ | Visual focus indicator |
| Enter on Cancel | Closes dialog, rule not deleted | ☐ | |
| Enter on Delete | Closes dialog, rule deleted | ☐ | |
| Esc | Closes dialog, rule not deleted | ☐ | |

**Test Steps** (continued):
5. Navigate to Policy Editor using Tab
6. Use Tab to reach Undo/Redo buttons
7. Press Enter to activate

| Action | Expected Result | ✅/❌ | Notes |
|--------|----------------|-------|-------|
| Tab to Undo | Button receives focus | ☐ | |
| Enter on Undo | Undo operation executes | ☐ | |
| Tab to Redo | Button receives focus | ☐ | |
| Enter on Redo | Redo operation executes | ☐ | |

**Pass Criteria**: All features fully keyboard accessible.

---

## Test Suite 7: Error Handling

**Objective**: Verify graceful handling of edge cases and errors.

### 7.1 Null/Empty Rule Fields

**Test Steps**:
1. Create rule with all optional fields empty
2. Delete rule
3. Verify dialog displays "Any" for empty fields (no null reference errors)

| Field | Expected Display | ✅/❌ | Notes |
|-------|-----------------|-------|-------|
| Empty process | "Any" | ☐ | |
| Empty remote IP | "Any" | ☐ | |
| Empty local IP | "Any" | ☐ | |
| Empty comment | "(none)" | ☐ | |

**Pass Criteria**: No errors, appropriate placeholder text displayed.

---

### 7.2 Rapid Operations

**Test Steps**:
1. Rapidly click Add button 10 times
2. Verify 10 rules added
3. Rapidly click Undo 10 times
4. Verify all rules removed
5. Check for any errors or UI freezing

| Scenario | Expected Result | ✅/❌ | Notes |
|----------|----------------|-------|-------|
| Rapid additions | All 10 rules added | ☐ | No missed operations |
| Rapid undos | All 10 rules removed | ☐ | |
| UI responsive | No freezing or lag | ☐ | |
| No errors | No exceptions thrown | ☐ | |

**Pass Criteria**: Operations complete successfully without errors or performance issues.

---

## Summary & Sign-Off

### Test Results

| Test Suite | Passed | Failed | Notes |
|-----------|--------|--------|-------|
| 1. Delete Confirmation (Issue #8) | ☐ | ☐ | |
| 2. Priority Helper (Issue #7) | ☐ | ☐ | |
| 3. Undo/Redo (Issue #13) | ☐ | ☐ | |
| 4. Cross-Feature Integration | ☐ | ☐ | |
| 5. Theme Compatibility | ☐ | ☐ | |
| 6. Accessibility | ☐ | ☐ | |
| 7. Error Handling | ☐ | ☐ | |

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

| Issue # | Description | Severity | Feature | Status |
|---------|------------|----------|---------|--------|
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

1. **Delete Confirmation**: Dialog is modal - cannot interact with main window while open

2. **Priority Badges**: Colors are hard-coded thresholds (200=high, 100=medium, <100=low)

3. **Undo/Redo**: History cleared when:
   - Application closes
   - New policy loaded
   - Template loaded

4. **Undo Stack Limit**: Only last 50 operations stored - older operations cannot be undone

5. **Priority Context**: Automatically updates - no manual refresh needed

6. **Keyboard Shortcuts**: Ctrl+Z (Undo), Ctrl+Y (Redo) - same as standard Windows applications

7. **Theme Testing**: Use theme toggle in main window header to switch between light/dark

8. **Multiple Windows**: Each Policy Editor window has independent undo/redo history
