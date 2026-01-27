# Feature Documentation

This directory contains documentation for each implemented feature of the WFP traffic control system.

## Naming Convention

- `000-project-overview.md` — Project scope and constraints (this milestone)
- `001-<feature-name>.md` — First implemented feature
- `002-<feature-name>.md` — Second implemented feature
- (continue sequentially)

## Required Sections per Feature Doc

Each feature document must include:

1. **Behavior** — What the feature does, user-facing and internal
2. **Configuration/Policy Schema Changes** — Any new fields or schema updates
3. **How to Run/Test** — Commands or steps to exercise the feature
4. **Rollback/Uninstall Behavior** — How to safely remove or disable
5. **Known Limitations** — Edge cases, unsupported scenarios, future work

## Updating Docs

- Update the relevant doc when modifying an existing feature
- Create a new numbered doc when adding a new feature
- Keep docs concise but complete enough for an engineer unfamiliar with the codebase
