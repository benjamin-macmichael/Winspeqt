# Monitoring Family Style Audit

## Scope

This audit covers the Monitoring views that currently live under `Views/Monitoring`:

- `Views/Monitoring/MonitoringDashboardPage.xaml`
- `Views/Monitoring/TaskManagerPage.xaml`
- `Views/Monitoring/PerformanceTrendsPage.xaml`
- `Views/Monitoring/StartupImpactPage.xaml`
- `Views/Monitoring/BackgroundProcessPage.xaml`

This document captures the current baseline for phase 1 audit work. It focuses on repeated structure, repeated styling decisions, page-family groupings, and likely extraction candidates. It does not prescribe the full final implementation.

## Recorded Decisions

The project-level style refactor decisions apply to this family:

1. This is a maintainability-first refactor, not a visual polish pass.
2. Monitoring views should look substantially the same after migration.
3. Existing accent colors should remain in use.
4. Shared colors should move toward semantic naming where practical.
5. Repeated shared structure should be extracted where it is genuinely repeated.
6. Unique layouts should remain inline rather than being abstracted just for consistency.

## High-Level Summary

The Monitoring folder currently contains two distinct sub-families plus one already-migrated dashboard page:

1. Monitoring dashboard
   - `MonitoringDashboardPage`
2. Monitoring tool/detail pages
   - `TaskManagerPage`
   - `PerformanceTrendsPage`
   - `StartupImpactPage`
   - `BackgroundProcessPage`

`MonitoringDashboardPage` is already aligned with the emerging shared dashboard system. It uses:

- `StandardDashboardHeader`
- shared dashboard spacing resources
- shared dashboard card button styles
- shared feature accent brushes

The four detail/tool pages are not yet aligned with that shared system. They still rely heavily on:

- repeated inline page headers
- repeated inline card shells
- page-local typography styles
- many hard-coded spacing and font-size values
- repeated accent hex colors

The largest refactor opportunity in this folder is not the dashboard. It is the shared detail-page shell used by `TaskManagerPage`, `PerformanceTrendsPage`, and `BackgroundProcessPage`, with `StartupImpactPage` partially fitting that same family.

## Current Shared Resource Baseline

This folder sits on top of a mixed baseline:

- `MonitoringDashboardPage` already consumes shared resources from `Styles/` and `Controls/`
- the other Monitoring pages still define most of their styling locally

Shared resources already in use here include:

- `StandardDashboardHeader`
- `DashboardPagePadding`
- `DashboardCardButtonStyle`
- `FeatureAttentionBrush`
- `FeatureSuccessBrush`
- `FeatureAccentBlueBrush`
- `FeatureHighlightBrush`
- `SubtleButtonStyle`

Built-in WinUI theme resources are also used widely:

- `ApplicationPageBackgroundThemeBrush`
- `CardBackgroundFillColorDefaultBrush`
- `CardStrokeColorDefaultBrush`
- `TextFillColorSecondaryBrush`
- `TextFillColorTertiaryBrush`
- `AccentTextFillColorPrimaryBrush`

Even with those shared pieces, most Monitoring detail-page styling decisions are still page-local and repeated.

## Page Family Grouping

### 1. Dashboard Variant

- `MonitoringDashboardPage`

This page is already in the dashboard family and should mostly be treated as baseline/reference rather than the main refactor target for the Monitoring folder.

### 2. Detail/Tool Variant

- `TaskManagerPage`
- `PerformanceTrendsPage`
- `BackgroundProcessPage`

These three pages share the clearest common structure:

- root `ScrollViewer`
- root container with `Padding="60"`
- back-button page header
- large icon + page title + subtitle
- optional top-right action button
- primary content area composed of cards
- loading overlay or refresh behavior tied to the page body

These are strong candidates for a standard Monitoring detail-page header control and shared card-shell resources.

### 3. Hybrid Diagnostic Variant

- `StartupImpactPage`

This page shares the same broad shell but is structurally more complex because it contains two in-page screens:

- screen 1: diagnostics / performance tips
- screen 2: startup applications detail

It should still consume the same shared typography, spacing, and card-shell resources, but it likely needs selective migration rather than a strict page template.

## Repeated Structural Patterns

### 1. Standard Monitoring Detail Header

Repeated almost verbatim in:

- `TaskManagerPage`
- `PerformanceTrendsPage`
- `BackgroundProcessPage`

Partially repeated in:

- `StartupImpactPage` screen 1
- `StartupImpactPage` screen 2

Repeated structure:

- transparent back button on the left
- icon + large page title in the main title area
- subtitle below the title
- optional trailing action button on the right

Common repeated values:

- root header margin bottom around `24` or `32`
- back button `Padding="8"`
- back button right margin `20`
- header icon `FontSize="40"`
- title `FontSize="36"` and bold
- subtitle `FontSize="18"`
- icon/title gap `12`
- subtitle top margin `4`

What varies:

- header icon glyph
- header accent color
- title and subtitle copy
- whether a trailing action button exists
- whether the screen uses a one-row or two-row header variant

This is the clearest extraction candidate in the folder after the dashboard header work.

### 2. Standard Metric / Summary Card

Repeated strongly between:

- `TaskManagerPage`
- `PerformanceTrendsPage`

Related but looser usage in:

- `BackgroundProcessPage` battery cards

Repeated structure:

- bordered card shell
- icon + section title row
- large primary metric value
- smaller secondary unit/value line
- supporting context beneath

Common repeated shell values:

- `Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"`
- `BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"`
- `BorderThickness="2"`
- `CornerRadius="12"`
- `Padding="28"` or `20`

This is a good candidate for a shared base card style plus a shared metric-card content pattern.

### 3. Standard List / Result Card Shell

Repeated across:

- `TaskManagerPage` process rows
- `StartupImpactPage` startup item rows
- `BackgroundProcessPage` process grid cards

Repeated structure:

- card-like `Border`
- card background and stroke
- rounded corners
- inner padding around `20`
- nested metadata rows and actions

This pattern is not one single template, but it strongly suggests a shared card shell layer:

- standard card shell
- compact card shell
- nested row card shell

### 4. Loading Overlay Pattern

Repeated in:

- `TaskManagerPage`
- `PerformanceTrendsPage`

Related but lighter inline loading states in:

- `StartupImpactPage`

Repeated overlay structure:

- full-page `Border`
- translucent dark background `#CC000000`
- centered `ProgressRing`
- loading message

This can be standardized if the team wants a reusable loading-state surface for tool pages.

### 5. Section Heading Pattern

Repeated across:

- `TaskManagerPage`
- `StartupImpactPage`
- `BackgroundProcessPage`

Repeated structure:

- section title around `20` to `24`
- semibold/bold emphasis
- bottom margin before cards or repeaters

This is a straightforward typography-token extraction candidate.

## Hard-Coded Styling Inventory

### Shared Literal Values Across The Family

The following values repeat often enough to qualify as token candidates.

### Spacing

Repeated values observed:

- `2`
- `4`
- `5`
- `6`
- `8`
- `10`
- `12`
- `16`
- `18`
- `20`
- `24`
- `28`
- `32`
- `48`
- `60`

Most common actual usages:

- root page padding: `60`
- header bottom margin: `24` and `32`
- back button padding: `8`
- back button right margin: `20`
- card padding: `20` and `28`
- section spacing: `24`, `32`, `48`
- card corner radius: `8`, `12`, `16`
- row/card margins: `12`

### Typography

Repeated font sizes:

- `10`
- `11`
- `12`
- `13`
- `14`
- `15`
- `16`
- `17`
- `18`
- `20`
- `22`
- `24`
- `28`
- `36`
- `40`
- `48`
- `56`

Common semantic groupings implied by current usage:

- page title: `36`
- page subtitle: `18`
- page header icon: `40`
- section title: `20` to `24`
- card title: `17` to `20`
- body text: `15` to `16`
- metadata / captions: `10` to `12`
- large metrics: `24`, `28`, `48`

### Radii and Borders

Repeated values:

- small radius: `4`
- compact radius: `6`
- standard radius: `8`
- card radius: `12`
- large card/banner radius: `16`
- card border thickness: `2`
- list-row border thickness: `1`
- transparent/back button border thickness: `0`

### Fixed Layout Numbers

Values that may be layout-specific rather than general tokens:

- process row min height: `112`
- background-process card min height: `140`
- trend card min height: `300`
- trend card max height: `600`
- category wrap item width: `400`
- chart responsive breakpoint: `900` in code-behind

## Hard-Coded Accent And State Colors

Raw hex colors found across the Monitoring detail pages:

- `#2196F3`
- `#FF9800`
- `#4CAF50`
- `#9C27B0`
- `#3F51B5`
- `#F44336`
- `#616161`
- `#DCEEFF`
- `#1565C0`
- `#FBFBFB`
- `#CC000000`

How they are used:

- CPU accent
- memory accent
- network accent
- disk accent
- error/end-task actions
- neutral restart action styling
- soft icon tile background/foreground pair
- chart background
- loading overlay

Observations:

- `#2196F3`, `#FF9800`, `#4CAF50`, and `#9C27B0` are reused across multiple Monitoring pages for the same conceptual resources
- these colors should be lifted into shared semantic Monitoring/detail-page resources rather than staying inline
- `PerformanceTrendsPage` already moved several of these into page-local brushes, which is a useful intermediate step but still local duplication

## Per-Page Notes

### MonitoringDashboardPage

Status:

- already substantially aligned with the new dashboard refactor direction

Notes:

- uses `StandardDashboardHeader`
- uses shared dashboard spacing/button resources
- likely needs only consistency review alongside the dashboard work already underway

### TaskManagerPage

Strengths:

- very strong repeated card structure
- clear semantic metrics
- consistent visual hierarchy

Audit notes:

- largest concentration of repeated inline styles in the folder
- process row markup is duplicated for grouped and non-grouped items
- restart/end-task button styling is duplicated inline
- header is a prime candidate for extraction

### PerformanceTrendsPage

Strengths:

- page-local styles already begin to separate typography and card shell concerns

Audit notes:

- many styles here should become shared app-level resources rather than page resources
- card layout and accent usage closely mirror `TaskManagerPage`
- responsive layout behavior in code-behind means page-level layout should stay selective even after tokenization

### StartupImpactPage

Strengths:

- already uses page-local styles for several repeated text/card patterns
- contains reusable card/banner patterns that can consume shared tokens later

Audit notes:

- this page is structurally different because it hosts two screens
- screen 1 and screen 2 duplicate header work internally
- warning banner, tip cards, and grouped expander rows are all good style-level extraction candidates
- comments contain mojibake/encoding artifacts and should be treated as a separate cleanup item

### BackgroundProcessPage

Strengths:

- `ProcessGridCardTemplate` already extracts one repeated card pattern

Audit notes:

- page still duplicates category section markup many times
- category headings rely on emoji text labels rather than shared icon/text patterns
- card/action styling remains mostly inline
- shares the same detail-page header pattern as `TaskManagerPage` and `PerformanceTrendsPage`

## Candidate Shared Resources

Based on the Monitoring family only, the highest-value shared resources would be:

### Typography

- `PageTitleTextStyle`
- `PageSubtitleTextStyle`
- `SectionTitleTextStyle`
- `BodyTextStyle`
- `CardTitleTextStyle`
- `CardBodyTextStyle`
- `MetricValueTextStyle`
- `MetricUnitTextStyle`
- `CaptionTextStyle`

### Spacing

- `Space2`
- `Space4`
- `Space8`
- `Space12`
- `Space16`
- `Space20`
- `Space24`
- `Space28`
- `Space32`
- `Space48`
- `Space60`

### Radii

- `RadiusSmall` for `4`
- `RadiusCompact` for `6`
- `RadiusMedium` for `8`
- `RadiusLarge` for `12`
- `RadiusXLarge` for `16`

### Color Roles

- `MetricCpuBrush`
- `MetricMemoryBrush`
- `MetricNetworkBrush`
- `MetricDiskBrush`
- `ActionDangerBrush`
- `ActionNeutralBrush`
- `InfoTileBackgroundBrush`
- `InfoTileForegroundBrush`
- `LoadingOverlayBrush`
- `ChartSurfaceBrush`

### Control Styles

- `DetailPageBackButtonStyle`
- `DetailPageHeaderTitleStyle`
- `DetailPageHeaderSubtitleStyle`
- `StandardCardBorderStyle`
- `CompactCardBorderStyle`
- `MetricCardBorderStyle`
- `DangerActionButtonStyle`
- `SecondaryOutlineActionButtonStyle`
- `InlineHintBannerStyle`
- `WarningBannerBorderStyle`

## Candidate Shared Structural Patterns

The best extraction candidates, in order of value:

1. Standard Monitoring detail-page header control
2. Shared card-shell styles for standard, compact, and nested-row cards
3. Shared metric-card content pattern used by `TaskManagerPage` and `PerformanceTrendsPage`
4. Shared loading overlay pattern for tool pages
5. Shared section heading / body typography tokens
6. Shared action button styles for restart / refresh / danger actions

Recommended interpretation for future implementation work:

- treat `MonitoringDashboardPage` as already on the shared-dashboard path
- refactor the four tool/detail pages as the main Monitoring-family migration wave
- start with header and card-shell extraction before attempting more aggressive template work
- keep `StartupImpactPage` selective because of its two-screen structure

## Risks And Notes Found During Audit

### 1. The Folder Is Mid-Migration

The dashboard page already uses shared resources while the detail pages do not. That means the Monitoring folder currently spans two styling systems.

### 2. Detail-Page Header Drift Is Likely

`TaskManagerPage`, `PerformanceTrendsPage`, and `BackgroundProcessPage` are close enough to one shared header that keeping them separate will continue to invite drift.

### 3. `StartupImpactPage` Should Not Be Forced Into The Same Full Template

It shares tokens and partial structure, but its in-page navigation and two-screen layout make it a poor candidate for a rigid page-level template.

### 4. Repeated Accent Colors Already Have Stable Meanings

CPU, memory, network, and disk colors are already acting like semantic tokens even though they are still hard-coded in several places.

### 5. Existing Encoding Artifacts Exist In `StartupImpactPage.xaml`

There are visible mojibake characters in comment separators. This is not a styling issue, but it should be kept as a known cleanup item.

## Recommended Baseline Conclusions For Future Sessions

For future Monitoring refactor work, this folder can be treated as:

1. One Monitoring family with two implementation states
   - dashboard page already on shared resources
   - detail/tool pages still largely inline
2. A strong candidate for extracting a shared detail-page header control
3. A strong candidate for card-shell and typography tokenization
4. A moderate candidate for shared metric-card patterns
5. A selective candidate for banner/loading-state standardization

With the current project decisions applied, the practical first pass for Monitoring should be:

1. preserve current visuals
2. extract the repeated detail-page header
3. standardize shared card shells and action-button styles
4. lift repeated Monitoring accent colors into semantic resources
5. migrate `StartupImpactPage` more selectively than the other tool pages

## Suggested Definition Of Done For A Monitoring-Family First Pass

A reasonable first-pass definition of done for the Monitoring detail pages would be:

- repeated detail-page header markup removed from page-local XAML
- repeated card shell values removed from page-local XAML
- CPU/memory/network/disk accent colors come from shared semantic resources
- restart, refresh, and danger action styles are no longer duplicated inline
- section-title/body typography comes from shared resources
- `StartupImpactPage` consumes shared tokens where applicable without forcing its unique layout into a generic template
- no behavior changes introduced

## Out Of Scope For This Audit

Not captured in this document:

- screenshot baseline
- full view-model behavior audit
- manual interaction-state validation
- performance or virtualization review
- cross-family comparison outside the Monitoring folder
