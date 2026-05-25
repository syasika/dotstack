# Multi-project architecture: CLI, Core, TUI

The Go source (`go-base/`) is a single module with flat packages. The .NET port splits responsibilities into three projects — `DotStack.Cli`, `DotStack.Core`, and `DotStack.Tui` — to enforce a clean dependency hierarchy and make each layer independently testable.

**Why not a single project?** The Go code has three distinct concerns (command parsing in `cmd/`, AWS operations in `internal/`, TUI in `cmd/browse/`) that share no cyclic dependencies. A single .NET project would allow accidental coupling between them. Three projects with one-way references (Cli → Core, Cli → Tui, Tui → Core) prevent that.

**Why not per-AWS-service projects?** The four AWS services (S3, SSM, SQS, SNS) are thin wrappers around AWSSDK calls. Splitting them further would add project overhead (5+ projects, 5+ test projects) without meaningful isolation — they all depend on the same `AWSSDK.Core` and follow the same stateless-functions pattern.
