---
name: git-workflow
description: Streamlined git add/commit with conventional commits, and git worktree management for parallel task isolation. Use when user says commit, worktree, merge, push, or after completing a task that needs a commit.
---

# Git Workflow

## Quick start

```bash
# Stage relevant files + commit with conventional message
git add -A
git commit -m "feat(S3): add null guards for v4 SDK collections"
```

## Workflows

### Smart commit

After completing a task in dotstack:

1. **Stage** only source/test/config files (exclude `obj/` `bin/` `node_modules/` `.DS_Store`)

   ```bash
   git add -- :/*.cs :/*.csproj :/*.props :/*.slnx :/*.md
   ```

2. **Check what's staged**

   ```bash
   git diff --cached --stat
   ```

3. **Determine commit type** from diff content:

   | Diff contains                          | Type       |
   |----------------------------------------|------------|
   | New public method/class/record         | `feat`     |
   | Bug fix, null guard, edge case         | `fix`      |
   | Interface change, rename, restructure  | `refactor` |
   | New test file/method                   | `test`     |
   | Config, build, dependency change       | `chore`    |
   | Comments, docs, readme                 | `docs`     |

4. **Determine scope** from changed files:

   | Files changed        | Scope     |
   |----------------------|-----------|
   | `Core/S3/*`          | `S3`      |
   | `Core/Ssm/*`         | `SSM`     |
   | `Core/Sqs/*`         | `SQS`     |
   | `Core/Sns/*`         | `SNS`     |
   | `Core/Configuration/*` | `config` |
   | `Core/Aws/*`         | `aws`     |
   | `Cli/Commands/*`     | `cli`     |
   | `Tui/*`              | `tui`     |
   | Multiple areas       | omit      |

5. **Commit**

   ```bash
   git commit -m "feat(S3): add null guards for v4 SDK collections"
   ```

   Multi-line if description needed:

   ```bash
   git commit -m "fix(S3): guard null DeleteErrors in v4 SDK" \
              -m "AWSSDK v4 defaults collection properties to null. Add null-coalescing to prevent NRE in EmptyBucketAsync."
   ```

**Safety**: Never commit directly to `main` without user confirmation.

### Worktree management

Worktrees let you work on multiple branches without stashing or conflict cleanup. Use when user wants parallel work or before starting a potentially disruptive refactor.

#### Create worktree (scripted)

```bash
.pi/skills/git-workflow/scripts/worktree-setup.sh my-feature main
# → ../dotstack-my-feature/
```

The script:
- Creates branch `my-feature` from `main` if missing
- Adds git worktree at `../dotstack-my-feature/`
- Runs `dotnet restore`
- Prints the worktree path

Agent should `cd` into the worktree, make changes, commit, then notify user.

#### List worktrees

```bash
git worktree list
```

#### Remove worktree (after merging)

```bash
git worktree remove ../dotstack-my-feature
git branch -d my-feature
```

#### Clean up stale worktrees

```bash
git worktree prune
```

### Parallel subagent worktrees

When using `pi-subagents` parallel mode, set `worktree: true` to auto-create isolated worktrees per task:

```javascript
subagent({
  tasks: [
    { agent: "worker", task: "add SSM test", worktree: true },
    { agent: "worker", task: "fix S3 null bug", worktree: true }
  ],
  concurrency: 2
})
```

This creates worktrees at `../dotstack-<task-id>/` for each task, runs the agent there, and reports per-worktree diffs. See [pi-subagents skill](../../../../../../home/shane/.pi/agent/git/github.com/nicobailon/pi-subagents/skills/pi-subagents/SKILL.md) for details.

### Push workflow

**Constraint**: Remote uses SSH (`git@github.com:`). Agent cannot handle interactive SSH passphrase prompts. Always check SSH connectivity before attempting push.

```bash
# 1. Check SSH connectivity first
ssh -T git@github.com 2>&1 | grep -q "successfully authenticated"

# 2. If SSH works, dry-run
git push --dry-run

# 3. Push
git push origin HEAD
```

**If SSH check fails**: Do NOT run `git push`. Tell user SSH key isn't available and show the push command for manual execution.

## Advanced features

### Commit message format

```
<type>(<scope>): <short summary>

<body>  # optional
```

Types: `feat` `fix` `refactor` `test` `chore` `docs`

### Undo last commit (before push)

```bash
git reset --soft HEAD~1
```

Discard staged changes:
```bash
git checkout -- .
```
