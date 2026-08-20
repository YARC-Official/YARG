# Migrating YARG.Core to YARG (Monorepo)

## Table of contents

- [Summary](#summary)
- [Current structure](#current-structure)
- [What is bad about YARG.Core and how a monorepo fixes it](#what-is-bad-about-yargcore-and-how-a-monorepo-fixes-it)
- [What to do with the YARG.Core repo](#what-to-do-with-the-yargcore-repo)
  - [Could sparse checkout replace the mirror?](#could-sparse-checkout-replace-the-mirror)
- [Migration plan](#migration-plan)
  - [Risk assessment](#risk-assessment)
- [Migration steps](#migration-steps)
  - [Enable mirror (before PR)](#1-enable-mirror-before-pr)
  - [Make the PR](#2-make-the-pr)
  - [Update your clone (after merge)](#3-update-your-clone-after-merge)
  - [Handle open PRs (after merge)](#after-the-monorepo--open-prs)
- [Appendix: One-shot migration script](#appendix-one-shot-migration-script)
- [Mirror automation details](#mirror-automation-details)
- [Appendix: Dry run on a fork - real CI without touching your fork](#appendix-dry-run-on-a-fork--real-ci-without-touching-your-fork-12h-zero-risk-to-yarc-official)

## Summary

Right now YARG and YARG.Core are two separate repos. YARG includes Core as a git submodule which is actually a link to a commit in the YARG.Core repo.

This causes a lot of developer friction.  Any feature that touches both repos needs two pull requests, two reviews, and an extra commit just to update the pointer. Git history is split across two places. We also need special code just to make the submodule work with Unity and Visual Studio.

This proposal is to move all of YARG.Core's files and history into the main YARG repo, so everything lives in one place.  The existing YARG.Core repo will still exist, but as a read-only mirror.

---

## Current structure

```
YARC-Official/YARG          <- main Unity project
  YARG.Core/                <- not a normal folder, it's a link to the other repo
  .gitmodules               <- says where that link points
  Packages/manifest.json    <- tells Unity: load Core from "../YARG.Core/YARG.Core"
  Assets/Plugins/Editor/Submodule/
    ProjectAdder.cs         <- hacks Unity's solution file so Rider/VS can see Core
    SubmoduleHelper.cs      <- runs "git rev-parse HEAD" to check if Core changed
```

## What is bad about YARG.Core and how a monorepo fixes it

### 1. Submodules make git confusing

* A plain `git clone` doesn't clone everything. `YARG.Core/` is just left empty.  You have to remember `git submodule update --init --recursive`
  * *After:* `git clone dev https://github.com/YARC-Official/YARG.git` just works

* If you commit inside `YARG.Core/` without creating a branch, it's easy to lose work
  * *After:* It's just a normal folder on a normal branch. You can edit `YARG.Core/Chart/ChartParser.cs` like any other file.

* `git status` just says `modified: YARG.Core (new commits)` with no details. You need `git diff --submodule=diff` to see what actually changed. 
  * *After:* `git status` shows all actual file changes together

### 2. Work takes longer

* Most changes touch both repos. You open a PR in Core and a PR in YARG, wait for two reviews, maintainers merge the Core PR, then YARG PR, then switch to `dev` and update the core pointer and push.
  * *After:* One PR with everything together. One review, one merge

* `git log` is full of `Core pointer update for xyz`. `Git log` doesnt show what's in these changes.  We have to change repos to figure it out.
  * *After:* One clean history. `git blame YARG.Core/Engine/EngineManager.cs` would show the full changes.

* A bug caused by a change in both repos can't easily be found with `git bisect`  And reverting a change needs three steps: revert YARG, revert Core, push a new core pointer.
  * *After:* `git bisect` works, reverting with `git revert` is easy

### 3. Submodules require special handling in Unity and IDE

* Unity rewrites `*.sln` and `*.csproj` on every reload. To make Rider and VS see Core, `ProjectAdder.cs` does some magic to patch Core projects back in. It needs an undocumented Unity hook.  We also have a hack in `SubmoduleHelper.cs` that checks if Core changed.
  * *After:* We can delete the whole `Assets/Plugins/Editor/Submodule/` folder

* `.editorconfig` and build props exist in both repos and can drift.
  * *After:* One editor config for both

### 4. Author credits are missing

* `#git-tracker` in Discord and also in the Nightly logs doesn't give credit for YARG.Core changes. The maintainer who pushes the YARG.Core pointer has to put the credit and the description inside the commit message.
  * *After:* `#git-tracker`, and Nightly logs credit the author and show the changes

## What to do with the YARG.Core repo

The YARG.Core repo is used by other community tools. We keep the repo available as a read-only mirror, so it can be used the same as it is today.

Read-only mirror summary:

* On every merge to `dev` in `YARG`, a CI workflow pushes the `YARG.Core` folder and its history to the YARG.Core repo:
  * `git subtree push --prefix=YARG.Core https://github.com/YARC-Official/YARG.Core master`
* That workflow authenticates as a GitHub App so it never expires. The YARG.Core repo is locked with branch protection rules on `master` that allow only the mirror GitHub App (and admins) to push.  This is standard practice with precedent for migrating to a monorepo.

If you only need Core, you can still clone the small mirror, not the whole Unity project.

## Migration plan

<details>
<summary>Summary</summary>

First, we set up the YARG.Core mirroring.

Next, create a single PR to `dev` that does the following:
1. Import Core's files into `YARG/YARG.Core` (one commit; core's history lives on in the mirror)
2. Point Unity at the new folder
3. Delete the submodule hacks
4. Update the docs

Once the PR merges, two steps remain:
1. Move open Core PRs into `YARG`
2. Inform contributors how to update their clone.

</details>

### Risk assessment

The ([dry run](#dry-run-status)) deliberately exercised the entire flow, including the worst case: a full undo. This section is based on observed behavior from the dry run.  In summary, no step in this plan destroys work. The worst realistic outcome of any failure is a stale mirror for a while and a documented recovery command. The riskiest moment is the merge, which is both pre-validated and revertible. 

- **YARG history: zero risk.** Nothing in the plan force-pushes `YARG` or rewrites its history. Every step is a normal, append-only commit; the merge and even a full undo are plain commits. Worst case, the migration commit is reverted like any other.
- **Mirror workflow: low risk.** If the mirror ever breaks, the failure is a red workflow run. The worst realistic outcome is that `YARG.Core` goes stale for a while. The fix is a re-run. The mirror is the only repo the plan ever force-pushes, and only in the documented undo recovery.
- **Merge commit: low risk** The merge must include three trailer lines in its message, for the mirror workflow.  If the trailer is wrong, e.g. the user merged the PR using GitHub instead of following the command, the merge still succeeds and everything looks normal. The mirror workflow will fail. But we have a safeguard for this: the pre-merge verification tests the trailer right after the merge, before anything is pushed

## Migration steps

### 1. Enable the mirror workflow (before PR)

A github admin does a one-time setup to enable the mirroring to YARG.Core. On every `dev` push touching `YARG.Core/` CI auto-syncs to `YARG.Core:master`. The mirror is read-only for everyone else. Steps to do this are in [Mirror automation details](#mirror-automation-details).

### 2. Make the PR

Below are the detailed steps to make this branch. If `dev` moves after branching, the branch must be rebased before the merge.

1. **Import Core's files into YARG.** This step turns the submodule link into a real folder - as a single import commit, not Core's full history.

   From the **YARG** repo:
   ```bash
   git checkout -b monorepo-merge dev
   # The submodule must be removed first
   git submodule deinit -f YARG.Core
   git rm -f YARG.Core .gitmodules
   git commit -m "Remove YARG.Core submodule"
   git remote add yarg-core https://github.com/YARC-Official/YARG.Core.git
   git fetch yarg-core
   # import Core's files as one commit
   git read-tree --prefix=YARG.Core/ yarg-core/master^{tree}
   git commit -m "Import YARG.Core into monorepo"
   ```
   This adds Core's files as a single import commit - the ~2k core commits do not enter YARG's history:
   * `git log` stays YARG's own history plus the import and merge commits - no core commits mixed in.
   * The existing `Core pointer update...` commits stay in YARG's history.
   * Core's full history lives on in the mirror repo: the merge commit's trailer records the core tip, and the split fetches the original core commits from the mirror repo itself - nothing is lost.
2. **Verify in Unity.** Open the project in Unity and check it compiles with no errors.
3. **Delete the submodule editor hacks.**
   ```bash
   git rm -r Assets/Plugins/Editor/Submodule
   ```
   The folder patched Unity's `*.sln` so Rider/VS could see Core and checked if the pointer changed.  Now it is no longer needed.
4. **Update README.md and CONTRIBUTING.md**
   ```diff
   - git clone -b dev --recursive https://github.com/YARC-Official/YARG.git
   - git submodule update --init --recursive
   + git clone -b dev https://github.com/YARC-Official/YARG.git
   ```
5. **Open the PR** from `monorepo-merge` to `dev`.
6. **Merge the PR**

   ```bash
   git checkout dev
   git merge --no-ff monorepo-merge -m "Merge pull request #N from YARG/monorepo-merge

   git-subtree-dir: YARG.Core
   git-subtree-mainline: $(git rev-parse dev)
   git-subtree-split: $(git rev-parse yarg-core/master)"
   git push origin dev
   ```
   `--no-ff` is required - a fast-forward would not create a merge commit and the trailers would be lost.

   Do not use the GitHub merge button, as it cannot add trailers. Without the trailers every mirror run fails with a segfault and `Maximum function recursion depth (1000) reached`

### 3. Update your clone (after merge)

After `dev` merges, run in your existing clone:
```bash
git checkout dev
git submodule deinit -f YARG.Core
rm -rf .git/modules/Assets/Plugins/YARG.Core
git pull
```

<a id="after-the-monorepo--open-prs"></a>
### 4. Handle open PRs (after merge)

After we do this, the YARG.Core repo becomes read-only and PRs will no longer be able to merge. Github will show a greyed out merge button. All Core work will have to be migrated to `YARG` repository.  This can be done by maintainers without losing git authorship.

* **Single Core PR** - close the old Core PR and open a new one in `YARG` under `YARG.Core/`
* **Paired YARG + Core feature** - keep the YARG PR open and push the Core changes into that same branch, so one YARG PR has both. Then close the Core PR with a link to the YARG PR.

To do the move, first export it as a patch file
```bash
# In your YARG.Core checkout, on your feature branch
git checkout my-core-feature
# format-patch saves commits as patch files for any changes not on master
git format-patch origin/master --stdout > ~/my-core-pr.patch
```

*For a single Core PR - create a new branch:*
```bash
# new branch from dev
git checkout -b my-feature dev
# replay commits keeping original author
git am --directory=YARG.Core/ ~/my-core-pr.patch
git push
```
*For a paired YARG + Core feature - add to the existing YARG PR branch:*
```bash
# switch to your existing YARG PR branch
git checkout existing-yarg-pr-branch
# clear the submodule worktree first: merging dev replaces the gitlink with a
# folder, and the checked-out submodule files block the merge
# ("untracked working tree files would be overwritten")
git submodule deinit -f YARG.Core
# merge the new dev in first: the branch still has the old submodule link, and
# git am refuses to add files under it ("appears as both a file and as a directory")
git merge dev
# resolve conflicts by keeping dev's versions (.gitmodules is deleted, YARG.Core becomes a normal folder)
# replay commits keeping original author
git am --directory=YARG.Core/ ~/my-core-pr.patch
git push
```


## Mirror automation details

`https://github.com/YARC-Official/YARG.Core` stays available and read-only.  On every merge to `dev` that touches `YARG.Core` in the YARG repo, CI pushes those changes to YARG.Core master branch automatically.

### 1. Create a GitHub App and store its credentials.
* In `YARC-Official` > Settings > Developer settings > GitHub Apps > New GitHub App:
  * Name `YARG Core Mirror`
  * `Homepage URL` = `https://github.com/YARC-Official/YARG`
  * Permissions: `Contents: Read & write` (and `Metadata: Read`)
  * `Repository access: Only select repositories` → select `YARG` and `YARG.Core`
  * Install it on both repos
* In `YARC-Official/YARG` > Settings > Secrets and variables > Actions > New repository secrets:
  * `MIRROR_APP_ID` = App ID from the App's page.
  * `MIRROR_APP_PRIVATE_KEY` = the App's private key (PEM).
* No secret needed in `YARC-Official/YARG.Core` - the token is minted in the workflow below and lives for 1 hour.

### 2. Add the workflow in YARG. 

Create `.github/workflows/mirror-yarg-core.yml`:
```yaml
name: Mirror YARG.Core
on:
  push:
    branches: [dev]
    paths: ['YARG.Core/**']
  workflow_dispatch:
concurrency:
  group: mirror-yarg-core
  cancel-in-progress: false
permissions:
  contents: read
jobs:
  mirror:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Check YARG.Core is a real folder
        id: guard
        run: |
          if git ls-tree HEAD YARG.Core | grep -q '^160000'; then
            echo "skip=true" >> "$GITHUB_OUTPUT"
            echo "YARG.Core is still a gitlink; nothing to mirror"
          fi
      - name: Configure git
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git config --unset-all http.https://github.com/.extraheader || true
      - name: Generate App token
        if: steps.guard.outputs.skip != 'true'
        id: app-token
        uses: actions/create-github-app-token@v1
        with:
          app-id: ${{ secrets.MIRROR_APP_ID }}
          private-key: ${{ secrets.MIRROR_APP_PRIVATE_KEY }}
          owner: YARC-Official
          repositories: YARG.Core
      - name: Push subtree to YARG.Core mirror
        if: steps.guard.outputs.skip != 'true'
        env:
          MIRROR_TOKEN: ${{ steps.app-token.outputs.token }}
        run: |
          git remote add mirror "https://x-access-token:${MIRROR_TOKEN}@github.com/YARC-Official/YARG.Core.git" 2>/dev/null || \
            git remote set-url mirror "https://x-access-token:${MIRROR_TOKEN}@github.com/YARC-Official/YARG.Core.git"
          git subtree split --prefix=YARG.Core -b mirror-temp HEAD "https://x-access-token:${MIRROR_TOKEN}@github.com/YARC-Official/YARG.Core.git"
          git push mirror mirror-temp:master
          git branch -D mirror-temp
```

### 3. Lock the mirror repo.

In `YARC-Official/YARG.Core` > Settings > Branches > Add branch protection rule for `master`: enable **Restrict who can push to matching branches** and allow only the GitHub App (`YARG Core Mirror`) and admins. This is what makes the mirror read-only. Direct pushes will fail and the Merge button on old Core PRs shows *Merging is blocked*.

### 4. Test before merging the monorepo PR.
Verify the merge-then-split locally before pushing anything. The merge script's `merge` phase does this automatically on a throwaway branch (so `dev` stays clean if it fails): it runs the trailer-bearing test merge, splits `YARG.Core/`, and aborts if the split tip does not equal the core tip.
Do not merge without the trailers - the split dies (`Maximum function recursion depth (1000) reached` on the runner's dash, a segfault in Git Bash), and the same happens if the import commit also carries trailer lines (`fatal: cache for <hash> already exists!`).

After the workflow lands on `dev` and the monorepo merge lands, push a trivial commit touching `YARG.Core/README.md` and verify https://github.com/YARC-Official/YARG.Core/commits/master shows it within ~1 min.

<a id="undo-the-monorepo-merge"></a>

**If you need to undo the whole monorepo merge:** the revert is not enough on its own.

The monorepo merge is one merge commit on `dev` that brought `YARG.Core/` in. It has two parents: parent 1 is `dev` as it was before the merge, parent 2 is the imported Core history. Undoing it takes two steps:

1. **Revert the post-merge commits first.** If anything landed on `dev` after the merge and touched `YARG.Core/`, revert those commits first (newest first). A revert of the merge deletes the whole `YARG.Core/` folder; if a later commit changed files in it, the deletion conflicts with those changes.
2. **Revert the merge commit itself:** `git revert -m 1 <merge commit>`. Git refuses to revert a merge commit without `-m` because it has two parents and git needs to know which side to keep. `-m 1` means "keep parent 1" — the pre-merge state of `dev` — and undo everything the merge introduced. After this, `dev` is back to its pre-merge state and `YARG.Core` is a gitlink again.

That only fixes YARG. The mirror does **not** follow the revert:

* The workflow run after that push skips (the guard sees the gitlink again), and even if a run pushed, pushing a deletion commit would only delete files — the monorepo-era commits would stay in the mirror's history. The workflow can never delete history.
* So an admin must force-push the mirror's `master` back to its last pre-merge commit exactly once — the tip of Core's original history, i.e. the commit `master` pointed at before the workflow pushed the merge. Any clone of Core from before the merge still has it (in the dry run: `tmp-core-mirror` from step 1): `git -C ./tmp-core-mirror push --force <mirror-url> master`.


<a id="appendix-dry-run-on-a-fork--real-ci-without-touching-your-fork-12h-zero-risk-to-yarc-official"></a>
## Appendix: Dry run plan

<details>
<summary>Click to expand</summary>

This is a plan to test the monorepo migration without touching YARC-Official and without touching your existing you/YARG fork.

To do this we make 2 temporary private copies of the upstream repos. All testing happens there. Delete them when done.

In the commands below, you means your GitHub username. you/YARG-dryrun is a new temporary repo — not your existing you/YARG fork.

These copies are plain repos created with gh repo create, not GitHub Forks (gh repo fork). Plain repos keep the test isolated from upstream.

1. Create throwaway test repos.
   ```bash
   # create private dry-run copy of YARG
   gh repo create you/YARG-dryrun --private
   # create private dry-run copy of YARG.Core
   gh repo create you/YARG.Core-dryrun --private
   # mirror all YARG branches to temp folder
   git clone --mirror https://github.com/YARC-Official/YARG.git ./tmp-yarg-mirror
   # push heads and tags to your dry-run YARG
   # NOTE: `git push --mirror` FAILS here - GitHub rejects refs/pull/*
   # ("deny updating a hidden ref") and exits non-zero, even though all
   # heads/tags went through. Push the two namespaces explicitly instead.
   git -C ./tmp-yarg-mirror push https://github.com/you/YARG-dryrun.git 'refs/heads/*:refs/heads/*' 'refs/tags/*:refs/tags/*'
   # mirror all YARG.Core branches to temp folder
   git clone --mirror https://github.com/YARC-Official/YARG.Core.git ./tmp-core-mirror
   # same explicit push for Core
   git -C ./tmp-core-mirror push https://github.com/you/YARG.Core-dryrun.git 'refs/heads/*:refs/heads/*' 'refs/tags/*:refs/tags/*'
   ```
   After pushing, set the default branch of both repos to `master` - the first push sets it to whatever ref arrived first (the dry run got `ColorProfile-struct`).

   **LFS - required or every clone of the dry-run repo aborts.** `git push --mirror` does not transfer Git LFS objects (they live in GitHub's separate LFS storage, keyed by content OID). The dry-run YARG then has all pointer files but no LFS storage, so `git clone` fails during checkout (`smudge filter lfs failed`, and with `filter.lfs.required=true` the checkout aborts leaving an empty index and partial worktree - the clone exits non-zero). Fix: upload the LFS objects for `dev`'s tree from a local clone that has them:
   ```bash
   git clone --single-branch -b dev https://github.com/you/YARG-dryrun.git lfs-src
   cd lfs-src
   git remote add upstream https://github.com/YARC-Official/YARG.git
   git fetch upstream dev
   git lfs fetch upstream HEAD   # downloads dev's LFS objects (only missing ones)
   git lfs push origin HEAD      # pushes exactly dev's objects; NOT `--all` (that includes old release-tag versions and fails on uncached ones)
   ```
   After this, normal clones smudge fully. YARG.Core has no LFS - the Core-dryrun clone is unaffected.

2. Install the mirror App and land the workflow on `dev`.
   * Create the GitHub App from [Mirror automation details](#mirror-automation-details), install it on `YARG-dryrun` and `YARG.Core-dryrun`, and add the `MIRROR_APP_ID` and `MIRROR_APP_PRIVATE_KEY` secrets to `you/YARG-dryrun`.
   * Copy `.github/workflows/mirror-yarg-core.yml` into the dry-run repo, adapted to `owner: you`, `repositories: YARG.Core-dryrun`, and push URL `https://github.com/you/YARG.Core-dryrun.git`. Commit it to `dev` and push - the workflow must be on `dev` before the next steps, or the test pushes won't trigger it.
   * Lock `YARG.Core-dryrun` `master` with "Restrict who can push to matching branches" (only the App and admins), not "Require a pull request". **Private-repo caveat:** branch protection on private repos needs GitHub Pro (the API returns 403 "Upgrade to GitHub Pro or make this repository public"). The real target is public, so it works there. For the dry run either skip the protection test, or make the two dry-run repos public first (their content is public upstream anyway) - then protection works on the free plan.

3. Test the guard step. `dev` still has `YARG.Core` as a gitlink, so simulate a maintainer's pointer bump and verify nothing fails:
   ```bash
   git clone --recursive -b dev https://github.com/you/YARG-dryrun.git guard-test && cd guard-test
   # bump the pointer to some other real Core commit, like a real pointer update
   # (if upstream master happens to equal the current gitlink, pick the previous
   # master instead - the bump must actually change the gitlink)
   git -C YARG.Core fetch origin
   git -C YARG.Core checkout -B bump origin/master
   git add YARG.Core
   git commit -m "test: simulate pointer update"
   git push origin dev
   ```
   **Verified result:** the workflow does NOT trigger at all. GitHub's `paths: ['YARG.Core/**']` filter does not match a bare gitlink change (the path has no slash), so pointer-update pushes create no run - no red runs and no mirror push in the pre-merge window, regardless of the guard. The guard step only matters for manual `workflow_dispatch` runs while `YARG.Core` is still a gitlink. Verify `YARG.Core-dryrun` `master` has no new commits after the push.

   **Clone verification rule (all steps):** do not pipe `git clone` without `set -o pipefail` and never trust its exit code blindly - if the checkout aborts (LFS smudge failure, etc.) the clone still returns 0 through a pipe and you can end up committing a tree with everything deleted (this happened in the first dry-run execution). After every clone: `git status --porcelain` must be empty and `git ls-files | wc -l` must be > 0.

4. Make the contributor clones at gitlink-era `dev`, plus the paired-PR branch:
   ```bash
   git clone --recursive -b dev https://github.com/you/YARG-dryrun.git yarg-old-recursive
   git clone -b dev https://github.com/you/YARG-dryrun.git yarg-old-plain
   cd yarg-old-recursive
   git checkout -b paired-pr
   # give the branch its own YARG-side commit, then push it like an open PR
   echo test > test-file.txt && git add test-file.txt
   git commit -m "test: YARG-side change"
   git push origin paired-pr
   git checkout dev
   ```
   Both clones stay on pre-merge `dev` - they are updated in steps 9 and 12.

5. Make the monorepo branch and open the PR.
   ```bash
   git clone --recursive -b dev https://github.com/you/YARG-dryrun.git yarg-fork && cd yarg-fork
   git checkout -b monorepo-test
   # remove the submodule completely
   git submodule deinit -f YARG.Core
   git rm -f YARG.Core; git rm -f .gitmodules; rm -rf .git/modules/Assets/Plugins/YARG.Core
   git commit -m "test: remove submodule"
   # import Core's files as one commit
   git remote add core https://github.com/you/YARG.Core-dryrun.git; git fetch core
   git read-tree --prefix=YARG.Core/ core/master^{tree}
   git commit -m "test: import YARG.Core subtree"
   git push origin monorepo-test
   ```
   Step by step, what this does:
   * **Remove the submodule** - `deinit` clears the submodule's files from the working copy, `git rm YARG.Core` removes the submodule link, `git rm .gitmodules` removes the file that defines submodules, and `rm -rf .git/modules/Assets/Plugins/YARG.Core` deletes git's hidden cached copy of the submodule's history (every recursive clone stores one). The path must be completely free before the import.
   * **Make sure the dry-run mirror is current** - if upstream `YARG.Core` master advanced since step 1, push the new master to `YARG.Core-dryrun` first. The mirror test in step 8 only fast-forwards if the imported code matches the mirror's current tip; importing stale code fails the test for the wrong reason.
   * **Import Core's files** - `git read-tree` copies the Core tree into `YARG.Core/` and commits it as one import commit. Do NOT use `git subtree add` here: it would write its own note lines onto this commit, colliding with the merge commit's notes in step 7 (`fatal: cache for <hash> already exists!`). The merge commit is the only place the notes may live.
   * **Do NOT touch `Packages/manifest.json`** - its `in.yarg.core` line points Unity at the Core package folder: `file:../YARG.Core/YARG.Core` resolves relative to `Packages/`, i.e. to `<project>/YARG.Core/YARG.Core` - exactly where the import lands (the same spot the submodule occupied). The dry run changed it to `file:./YARG.Core/YARG.Core` and Unity broke (`Packages/YARG.Core/YARG.Core/package.json` not found). Leave it alone.
   Open a PR from `monorepo-test` to `dev` in `you/YARG-dryrun` and check the diff shows `.gitmodules` deleted, the `YARG.Core` gitlink deleted, and the `YARG.Core/` tree added - and does NOT touch `Packages/manifest.json`. The repo's normal CI also runs on this PR - a bonus check that the monorepo layout builds.
   On Windows use Git Bash. In PowerShell use `Remove-Item` instead of `rm`.

   **Do not branch from an old dev commit.** The branch must be created from the exact `dev` tip that will be merged. If dev commits land between the branch point and the merge, those commits become uncached boundary commits in the split traversal and it dies (the first dry run branched early and hit this; the fix was re-creating the branch from the current tip).

6. Verify the monorepo branch builds and Core's tests pass.
   * **NuGet restore is a prerequisite for any fresh clone** (dev included): `Assets/Packages/` is untracked and populated by NuGetForUnity from `Assets/packages.config` on editor load. In Unity batchmode the restore does not run, so copy the local packages in first (`cp -r <existing YARG clone>/Assets/Packages yarg-fork/Assets/`) or open the project in the GUI once.
   * Compile check (batchmode):
     ```bash
     "/c/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Unity.exe" -batchmode -nographics -quit \
       -projectPath "$PWD/yarg-fork" -logFile unity-import.log
     ```
     Verified result: 0 compile errors on `monorepo-test`.
   * Core's real test suite is `dotnet test`, not the Unity Test Runner (the `YARG.Core.UnitTests` folder is outside the UPM package, so Unity never compiles it - Core's own CI runs `dotnet test YARG.Core.sln`):
     ```bash
     cd yarg-fork/YARG.Core
     dotnet restore YARG.Core.sln
     dotnet test YARG.Core.sln --configuration Debug --no-restore
     ```
     Verified result: 463 passed, 0 failed, 2 skipped on `monorepo-test`.

7. Merge the PR from `monorepo-test` to `dev` in `you/YARG-dryrun` with a trailer-bearing merge commit - do not use the GitHub merge button:
   ```bash
   cd yarg-fork
   git checkout dev && git pull
   git merge --no-ff monorepo-test -m "Merge monorepo YARG.Core subtree

   git-subtree-dir: YARG.Core
   git-subtree-mainline: $(git rev-parse dev)
   git-subtree-split: $(git rev-parse core/master)"
   git push origin dev
   ```
   `--no-ff` is required (a fast-forward loses the trailers). If `dev` moved since the branch was created, rebase `monorepo-test` onto the new tip first, then use the new tip as `git-subtree-mainline`.

8. Test the mirror. The merge push already triggers a sync run - its split must succeed and push an up-to-date state (the split tip equals the existing `master` tip, proving the hashes match). Then push a small commit touching `YARG.Core/README.md` to `dev` and verify `you/YARG.Core-dryrun` `master` updates in about a minute. This push must fast-forward - if the split had not reproduced the original commits exactly, it would be rejected.

   **Verified failure modes (all hit during the dry run, all fixed by the steps above):**
   * Merge commit without trailers -> split dies: `Maximum function recursion depth (1000) reached` (dash on the runner; Git Bash segfaults instead). git-subtree's `check_parents` recurses down the whole excluded pre-merge dev history from the uncached boundary commit.
   * Trailer on the import commit AND on the merge (both referencing the same core tip) -> `fatal: cache for <hash> already exists!`
   * Split without the repository parameter -> `fatal: could not rev-parse split hash <sha> from commit <merge>` - the core history is not in the monorepo repo; the parameter fetches it from the mirror (which already holds it).
   * Checkout's `http.https://github.com/.extraheader` (job token) left in place -> `remote: Repository not found` - the job token overrides the App token in the URL. The workflow unsets it in the Configure git step.

9. Test the contributor update (clones from step 4):
   * `yarg-old-recursive` (submodule initialized) - the documented update:
     ```bash
     git checkout dev
     git submodule deinit -f YARG.Core
     rm -rf .git/modules/Assets/Plugins/YARG.Core
     git pull
     ```
     It should fast-forward with no `untracked files` error. On Windows PowerShell use `Remove-Item -Recurse -Force` instead of `rm -rf`.
   * `yarg-old-plain` - the new documented flow: just `git pull`, no submodule commands at all.

10. Export a Core PR as a patch:
    ```bash
    git clone https://github.com/you/YARG.Core-dryrun.git core-fork && cd core-fork
    git checkout -b my-core-feature
    # make a small change to a source file (not README.md - dev changes that in step 8)
    echo "// dry-run test" >> Chart/ChartParser.cs
    git commit -am "test: core feature"
    git format-patch origin/master --stdout > ./p.patch
    ```

11. Test the single-PR migration:
    ```bash
    git clone -b dev https://github.com/you/YARG-dryrun.git yarg-single && cd yarg-single
    git checkout -b my-feature
    git am --directory=YARG.Core/ ../core-fork/p.patch
    git push origin my-feature
    ```
    Open and merge the PR in `you/YARG-dryrun`.

12. Test the paired-PR migration. In `yarg-old-recursive` (after step 9's pull it is on post-merge `dev`):
    ```bash
    git checkout paired-pr
    # clear the submodule worktree first: merging dev replaces the gitlink with a
    # folder, and the checked-out submodule files block the merge
    git submodule deinit -f YARG.Core
    # merge the monorepo in; resolve conflicts by keeping dev's versions
    git merge origin/dev
    # replay the Core commits keeping original author
    git am --directory=YARG.Core/ ../core-fork/p.patch
    git push origin paired-pr
    ```
    Open and merge the PR in `you/YARG-dryrun`.

13. Test as an outside tool. Run `git clone https://github.com/you/YARG.Core-dryrun.git` and confirm it contains only `YARG.Core` history and files.

14. Optional: verify the mirror is read-only. Add a second GitHub account as a collaborator (write) on `YARG.Core-dryrun` and push `master` with it - the push must be rejected by branch protection. The owner's own push succeeds (owners bypass protection), so this needs a second account.

15. Optional: test the undo path. In a dev clone (e.g. `yarg-old-plain`):
    ```bash
    git checkout dev && git pull
    # the step-8 README commit touched YARG.Core/, so revert it first - this is
    # exactly the conflict the "If you need to undo" section warns about
    git revert <README commit>
    git revert -m 1 <monorepo merge commit>
    git push origin dev
    ```
    The workflow run stays green but skips (the guard sees the gitlink again) and `YARG.Core-dryrun` `master` keeps the monorepo-era commits - the mirror does not follow the revert. Then test the recovery: force-push the original history back (`tmp-core-mirror` from step 1 still has it):
    ```bash
    git -C ./tmp-core-mirror push --force https://github.com/you/YARG.Core-dryrun.git master
    ```

16. Clean up. Delete both `*-dryrun` repos and the test App installation. Also delete local `./tmp-yarg-mirror`, `./tmp-core-mirror`, `./guard-test`, `./yarg-old-recursive`, `./yarg-old-plain`, `./yarg-fork`, `./yarg-single`, `./core-fork`, and `./p.patch` folders. If all checks pass, repeat steps 2-15 on `YARC-Official`.

</details>