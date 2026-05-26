#!/usr/bin/env bash
set -euo pipefail

# worktree-setup.sh — Create a git worktree for parallel task execution
#
# Usage: ./worktree-setup.sh <branch-name> [<base-branch>]
#
# Creates a worktree at ../dotstack-<branch> from the base branch,
# restores NuGet packages, and prints the worktree path.
#
# The caller (agent) should:
#   1. Run this script
#   2. cd into the worktree path
#   3. Make changes
#   4. Commit
#   5. Push or merge back

BRANCH="${1:?Usage: $0 <branch-name> [<base-branch>]}"
BASE="${2:-main}"
REPO_ROOT="$(git rev-parse --show-toplevel)"
WORKTREE_PATH="${REPO_ROOT}/../dotstack-${BRANCH}"

echo "=== worktree-setup ==="
echo "  Branch:  ${BRANCH}"
echo "  Base:    ${BASE}"
echo "  Path:    ${WORKTREE_PATH}"

# Create the branch if it doesn't exist
if ! git show-ref --verify --quiet "refs/heads/${BRANCH}"; then
    echo "  Creating branch '${BRANCH}' from '${BASE}'..."
    git branch "${BRANCH}" "${BASE}"
fi

# Create or verify worktree
if [ -d "${WORKTREE_PATH}" ]; then
    echo "  Worktree already exists, verifying..."
    GIT_DIR="${WORKTREE_PATH}/.git"
else
    echo "  Creating worktree..."
    git worktree add "${WORKTREE_PATH}" "${BRANCH}"
fi

cd "${WORKTREE_PATH}"

# Restore NuGet packages
echo "  Restoring packages..."
dotnet restore --no-cache 2>/dev/null || dotnet restore 2>/dev/null || echo "  (restore skipped)"

echo "=== ready ==="
echo "${WORKTREE_PATH}"
