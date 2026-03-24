# Styling Refactor Plan

## Purpose

This document defines a practical plan to reduce styling duplication, standardize formatting, and introduce shared UI resources across the WinUI/XAML app without attempting a risky one-shot rewrite.

The current codebase shows the same UI patterns repeated across many pages:

- Repeated page headers with back button, icon, title, subtitle, and optional action button
- Repeated card containers with similar `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, and `Padding`
- Many hard-coded values for `FontSize`, `Padding`, `Margin`, `CornerRadius`, and colors
- Inconsistent use of shared styles across pages
- Some pages already referencing shared styles while others still inline nearly everything

The objective is to standardize the reusable parts of the UI system while preserving necessary page-specific variation.

## Goals

- Reduce repeated XAML for common layout and visual patterns
- Centralize typography, spacing, radius, and color decisions
- Standardize page structure where it improves consistency
- Preserve semantic variation such as success, warning, and danger states
- Make future pages easier to build correctly
- Keep migration incremental so the app remains stable during refactor

## Non-Goals

- Rewriting every page at once
- Forcing every feature area into identical visuals
- Replacing working layouts that are unique for a good reason
- Changing feature behavior as part of the styling refactor

## Guiding Principles

- Prefer semantic resources over hard-coded visual values
- Prefer a few well-used shared patterns over a large and fragile style library
- Standardize page families, not just isolated controls
- Extract shared controls only when structure repeats, not just appearance
- Migrate incrementally and validate visually after each batch
- Prioritize maintainability over visible polish; the app should look substantially the same after refactor
- Preserve existing color usage, but rename shared colors semantically where practical
- Extract reusable controls where the structure is truly shared, and leave unique layouts inline

## Project Decisions

The following decisions are now in effect for this refactor:

1. This is a maintainability-first refactor, not a UI polish pass.
2. Existing visual appearance should remain substantially the same unless a small visual change is required to enable reuse.
3. Existing colors should remain in use, but shared colors should be given general semantic names where practical.
4. The standard shared page header should move directly to a reusable `UserControl`.
5. Reuse should be applied selectively: extract repeated structure, and leave truly unique layout inline.

---

## Phase 1: Audit And Baseline

### Objectives

- Identify the highest-value duplication
- Group pages into reusable layout families
- Establish a baseline before making structural changes

### Work

1. Audit repeated patterns across all pages.
2. Catalog hard-coded values used for:
   - spacing
   - font sizes
   - corner radii
   - icon sizes
   - accent colors
   - card shell properties
3. Group pages by structural similarity.
4. Capture representative screenshots for before/after comparison.

### Proposed Page Families

- Dashboard pages
  - Security dashboard
  - Monitoring dashboard
  - Optimization dashboard
- Detail/tool pages
  - Task manager
  - Network security
  - App security
  - Large file finder
  - App usage
- Settings/config pages
  - Settings
  - Recommendations/configuration pages
- Scan/result pages
  - Pages with summary cards, result cards, and status banners

### Decision Recorded

This effort is a maintainability refactor. Visible polish is out of scope for now, and migrated pages should look substantially the same after the work is complete.

### Deliverables

- Audit spreadsheet or markdown summary
- List of repeated structures
- List of candidate shared resources
- Screenshot baseline for high-traffic pages

---

## Phase 2: Define The Shared Design System

### Objectives

- Create a minimal but opinionated shared resource layer
- Remove ad hoc visual decisions from individual pages

### Proposed Resource Structure

Create a new folder such as `Styles/` or `Themes/` and split resources by concern:

- `Styles/Colors.xaml`
- `Styles/Typography.xaml`
- `Styles/Spacing.xaml`
- `Styles/Radii.xaml`
- `Styles/Buttons.xaml`
- `Styles/Cards.xaml`
- `Styles/PageLayouts.xaml`
- `Styles/States.xaml`
- `Styles/Templates.xaml`

Merge these dictionaries in `App.xaml`.

### Resource Types To Introduce

#### 1. Color Roles

Use semantic names rather than page-specific names.

Examples:

- `AppSurfaceBrush`
- `AppSurfaceSubtleBrush`
- `AppCardBrush`
- `AppCardBorderBrush`
- `AppTextPrimaryBrush`
- `AppTextSecondaryBrush`
- `AppAccentBrush`
- `AppInfoBrush`
- `AppSuccessBrush`
- `AppWarningBrush`
- `AppDangerBrush`

#### 2. Typography Tokens

Examples:

- `PageTitleTextStyle`
- `PageSubtitleTextStyle`
- `SectionLabelTextStyle`
- `CardTitleTextStyle`
- `CardBodyTextStyle`
- `MetricValueTextStyle`
- `MetricUnitTextStyle`
- `CaptionTextStyle`

#### 3. Spacing Tokens

Prefer a constrained scale instead of arbitrary values.

Examples:

- `Space4`
- `Space8`
- `Space12`
- `Space16`
- `Space20`
- `Space24`
- `Space32`
- `Space40`
- `Space60`

#### 4. Radius Tokens

Examples:

- `RadiusSmall`
- `RadiusMedium`
- `RadiusLarge`
- `RadiusXLarge`

#### 5. Common Control Styles

Examples:

- `PageBackButtonStyle`
- `PrimaryActionButtonStyle`
- `SecondaryActionButtonStyle`
- `SubtleIconButtonStyle`
- `StandardCardBorderStyle`
- `SectionCardBorderStyle`
- `InfoBannerBorderStyle`

### Decision Needed

> Comment: Decide whether spacing and radius should be defined as literal numeric resources only, or whether the team wants a stronger design-token system with strict naming and documentation. The latter is better long term but adds initial ceremony.

### Decision Recorded

Feature areas should keep their existing colors. Shared colors should still be named semantically where possible so they can be reused without page-specific naming.

### Deliverables

- Shared resource dictionary structure
- Approved token naming conventions
- Initial token set for colors, typography, spacing, and radii

---

## Phase 3: Extract Shared Structural Patterns

### Objectives

- Remove duplicated XAML structure, not just duplicated visual values
- Create reusable page building blocks

### Highest-Value Shared Patterns

#### 1. Standard Page Header

Common repeated structure:

- back button
- feature icon
- page title
- subtitle
- optional top-right action button

This appears in multiple pages including settings, security, monitoring, and optimization pages.

Possible implementation options:

- `UserControl` with bindable properties
- `DataTemplate` for a shared header model
- reusable style/template pattern with page-provided content slots

Recommended direction:

- Use a reusable `UserControl` for the standard header if the structure is truly consistent
- Use page-level composition only if there are too many layout exceptions

Decision recorded:

- Move to a reusable `UserControl` for the shared page header in the first pass where the structure is consistent enough to support it

#### 2. Standard Card Shell

Common repeated structure:

- `Border`
- card background
- border brush
- border thickness
- corner radius
- padding

This should become at least a shared style, and possibly a wrapper control if behavior becomes associated with it.

#### 3. Metric / Summary Card

Common repeated structure:

- title
- icon
- large metric value
- optional unit
- supporting text
- accent color

This appears across dashboard and monitoring-style pages.

#### 4. Info Banner / Hint Banner

Common repeated structure:

- icon
- informational copy
- neutral or accent background

#### 5. Empty / Loading / Error States

Common repeated structure:

- icon
- primary message
- supporting message
- optional action

### Decision Recorded

The standard page header should be implemented as a reusable control in the first pass where the repeated structure supports it.

### Decision Needed

> Comment: Decide whether metric cards should be standardized aggressively. If some teams rely on custom card layouts, it may be better to standardize card shells first and only later standardize full metric-card structure.

### Deliverables

- Shared header strategy
- Shared card-shell strategy
- Shared state-pattern strategy
- List of reusable controls/templates to build

---

## Phase 4: Standardize Page Families

### Objectives

- Refactor one family at a time
- Prove the shared system works before migrating everything

### Recommended Migration Order

#### Wave 1: Dashboard Pages

Why first:

- High duplication
- Lower behavioral risk
- Good visual payoff

Target pages:

- `Views/Security/SecurityDashboardPage.xaml`
- `Views/Monitoring/MonitoringDashboardPage.xaml`
- `Views/Optimization/OptimizationDashboardPage.xaml`
- `Views/DashboardPage.xaml`

Focus:

- card shell
- header structure
- section spacing
- metric card consistency

#### Wave 2: Settings And Simple Detail Pages

Why second:

- Moderate duplication
- Fewer complex data presentations

Target pages:

- `Views/SettingsPage.xaml`
- `Views/Security/SettingsRecommendationsPage.xaml`
- `Views/Optimization/AppDataCleanupCard.xaml`

Focus:

- section labels
- settings cards
- info banners
- button consistency

#### Wave 3: Complex Tool Pages

Why third:

- More custom layouts
- Higher chance of layout regression

Target pages:

- `Views/Monitoring/TaskManagerPage.xaml`
- `Views/Security/NetworkSecurityPage.xaml`
- `Views/Security/AppSecurityPage.xaml`
- `Views/Optimization/AppUsagePage.xaml`
- `Views/Optimization/LargeFileFinder.xaml`
- `Views/Optimization/OptimizationPage.xaml`

Focus:

- shared header usage
- shared card shells
- standardized typography and spacing
- selective extraction of repeated sub-panels

Migration rule:

- Refactor where reuse clearly improves maintainability
- Leave unique or one-off layout details inline rather than over-abstracting them

### Decision Needed

> Comment: Confirm whether the migration should prioritize the most frequently used pages first, or the easiest pages first. Those are often not the same set. Usage-based prioritization gives better product impact; simplicity-based prioritization lowers refactor risk.

### Deliverables

- Ordered migration backlog
- Definition of done for each page migration

---

## Phase 5: Verification Strategy

### Objectives

- Prevent visual regressions
- Ensure the shared system actually improves consistency

### Verification Checklist

For each migrated page, validate:

- layout spacing still looks intentional
- text hierarchy remains clear
- action buttons still align correctly
- light and dark theme behavior still works
- hover/focus/pressed states still look correct
- bindings and commands still work
- long text still wraps correctly
- loading/empty/error states still render correctly

### Suggested Validation Methods

- Maintain before/after screenshots for representative pages
- Manual QA checklist by page family
- Optional screenshot diffing if the team has appetite for it

### Decision Needed

> Comment: Decide whether visual review is handled informally by developers or whether a formal signoff process is required for each migrated wave. A formal signoff slows the work but prevents design drift during the transition.

### Deliverables

- Page migration QA checklist
- Screenshot baseline and comparison set

---

## Phase 6: Prevent Regression

### Objectives

- Keep duplication from returning after the refactor

### Team Conventions To Establish

1. New pages must use shared resource dictionaries.
2. New page headers should use the standard header pattern unless there is a documented exception.
3. New cards should start from shared card styles/templates.
4. Hard-coded colors, font sizes, radii, and spacing should be treated as exceptions.
5. Page-specific styles should stay local unless reused in at least two places.

### Suggested Review Prompts

Add these checks to PR review:

- Is this style already available as a shared resource?
- Is this hard-coded value semantically meaningful or just convenient?
- Does this page fit an existing page family?
- Should this repeated structure be extracted before merging more copies of it?

### Decision Needed

> Comment: Decide how strict review enforcement should be. If standards are too loose, duplication will return. If standards are too strict too early, teams may work around the system instead of using it.

### Deliverables

- UI contribution guidelines
- PR checklist items for styling consistency

---

## Concrete First Iteration

This is the recommended first implementation slice.

### Step 1

Create shared resource dictionaries and merge them through `App.xaml`.

### Step 2

Extract and standardize:

- typography styles
- card shell style
- back button style
- section label style
- page title and subtitle styles

### Step 3

Refactor the dashboard family first:

- `Views/DashboardPage.xaml`
- `Views/Security/SecurityDashboardPage.xaml`
- `Views/Monitoring/MonitoringDashboardPage.xaml`
- `Views/Optimization/OptimizationDashboardPage.xaml`

### Step 4

Build and adopt the reusable standard page header control where the repeated structure is consistent enough to support it.

### Step 5

Use what is learned from dashboards to migrate settings and simple pages.

---

## Concrete Decisions Already Made

The following decisions are already set for implementation:

1. This is a maintainability-only refactor, not a visual polish pass.
2. Accent colors should remain as they are today, but shared colors should receive general semantic names where practical.
3. The shared page header should be a `UserControl` in the first pass where the repeated structure supports it.
4. Reuse should be selective: extract repeated structure and leave unique layout inline.

The following decisions may still be clarified during implementation:

1. Should migration be prioritized by page usage or by implementation simplicity?
2. How formal should visual signoff be during migration?
3. How strict should code review be about introducing new hard-coded styling values?

---

## Suggested Ownership Model

If multiple developers are involved, split responsibility by layer instead of by page:

- One owner for design tokens and base resource dictionaries
- One owner for shared controls/templates
- One owner per migration wave/page family
- One reviewer responsible for consistency checks across all migrated pages

### Decision Needed

> Comment: Decide whether a single person should own the shared style system. Without clear ownership, shared resources often become inconsistent or grow without discipline.

---

## Risks

- Over-abstracting too early and making XAML harder to understand
- Creating too many tokens or styles that nobody remembers to use
- Breaking layout/bindings while extracting controls
- Standardizing pages that should remain intentionally different
- Mixing behavior changes into the styling cleanup

## Risk Mitigation

- Start with the most repeated patterns only
- Prefer semantic naming
- Migrate one page family at a time
- Validate each wave before continuing
- Keep behavior changes out of scope unless required by the refactor

---

## Definition Of Success

This effort is successful if:

- common page shells are recognizable and consistent
- most repeated visual values come from shared resources
- repeated page structures are extracted where it makes sense
- new pages have a clear path to follow
- the codebase becomes easier to maintain without making UI work slower
