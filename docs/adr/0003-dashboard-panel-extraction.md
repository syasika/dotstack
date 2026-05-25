# Dashboard panel extraction: per-service panels behind IServicePanel

The original `BrowseDashboard` was a ~450-line God class holding state, rendering logic, refresh/delete operations, and keyboard handling for all four AWS services (S3, SSM, SQS, SNS) in one file. Adding a fifth service required editing ~10+ locations in the same class.

The dashboard now delegates each service's concerns to a dedicated panel class implementing `IServicePanel`, keeping `BrowseDashboard` as a thin orchestrator.

**Why extract panels?** The previous inline approach made the dashboard untestable (the only test checked that the `ServiceMode` enum had 4 values) and created a cognitive bottleneck — understanding any service's TUI behavior meant reading through interleaved state for all four services. Adding a service required touching switch statements, state fields, render methods, and key handlers scattered across the file.

**Why an interface instead of an abstract base class?** `IServicePanel` defines the seam (render, refresh, handle key, help text, sub-navigation status). Each panel is independently testable and can be swapped without affecting others. An abstract base class would couple panels through shared mutable state. A static helper (`Confirm`, cursor math) is shared via direct call instead.

**Why keep container status in the dashboard?** The container header is always-visible presentation — it has no cursor, no delete, no enter action. Making it a panel would force the interface to accommodate a non-navigable element, adding complexity for no benefit.

## What was added

- **`dotstack.Tui/IServicePanel.cs`** — interface with `HandleKey`, `RefreshAsync`, `Render`, `HelpText`, `IsAtRootLevel`
- **`dotstack.Tui/S3Panel.cs`** — bucket list / object list navigation, delete, refresh
- **`dotstack.Tui/SsmPanel.cs`** — parameter list, get value, delete, refresh
- **`dotstack.Tui/SqsPanel.cs`** — queue list / message list navigation, delete, refresh
- **`dotstack.Tui/SnsPanel.cs`** — topic list, delete, refresh
- **`dotstack.Tui/BrowseDashboard.cs`** (rewritten) — thin orchestrator: creates panels, routes keyboard events, switches on tab press, renders header + container status + active panel

## What wasn't added (and why)

- **No per-panel test coverage** — each panel is testable through `IServicePanel` but no tests were written as part of this extraction. Adding them is deferred to a test-focused follow-up.
- **No `IAwsClientFactory` seam** — panels still take concrete AWS SDK clients. This is candidate 3 from the architecture review — deferred because it's a separate decision orthogonal to the panel extraction.

## Trade-offs

| Concern | Decision | Rationale |
|---------|----------|-----------|
| Panel communication | Dashboard holds `Dictionary<ServiceMode, IServicePanel>` | Simple routing. No events, no DI. |
| Sub-navigation | Each panel manages its own depth (bucket → objects, queue → messages) | Dashboard doesn't need to know about drill-down state. `IsAtRootLevel` is the only signal it reads. |
| Key routing | Panel `HandleKey` returns `bool` — true = consumed, false = dashboard handles globally | Dashboard handles mode switching (1-4) and quit (q/esc). Panels handle navigation, enter, delete, escape-back, refresh. |
| AWS client lifecycle | Each panel owns and disposes its client | Matches the previous design where the dashboard owned all clients. Panels are disposed in `BrowseDashboard.Dispose()`. |
