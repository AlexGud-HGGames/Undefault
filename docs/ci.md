## Continuous Integration

UndefaultIt uses GitHub Actions for automated build and test on every pull
request targeting `main` and every push to `main`. The workflow can also be
re-run manually via `workflow_dispatch`.

### Workflow

- File: [`.github/workflows/ci.yml`](../.github/workflows/ci.yml)
- Runner: `windows-latest`. Several tests exercise Windows-only paths
  (CS2 cfg detection in `Cs2SetupTestCollection`, `WindowsHotkeyService`
  registration, `WindowsProtectedSpotifySecretStore` DPAPI usage); a Linux
  runner would either skip or fail those tests, and mirroring the product
  platform also keeps the CI outcome aligned with a tester machine.
- SDK: a single `8.0.x` channel installed via `actions/setup-dotnet@v4`,
  per [`docs/dotnet-tfm-decision.md`](dotnet-tfm-decision.md) (UND-30 + UND-46,
  commit `09c245d`). The solution is uniformly on `net8.0`; no `9.0.x`
  channel is installed.
- Steps: `actions/checkout@v4` → `actions/setup-dotnet@v4` → `dotnet restore`
  → `dotnet build --no-restore -c Release` → `dotnet test --no-build -c Release`
  with the `trx` logger writing to `TestResults/`.
- Caching: `setup-dotnet`'s `cache: true` is not enabled because the repo
  has no `packages.lock.json`, which is the file the action keys against
  by default. Restore cost is small enough today that adding lockfiles
  purely to enable caching is not worth the maintenance.

### Failure semantics

Any non-zero exit code from `restore`, `build`, or `test` fails the run.
The workflow does not use `continue-on-error`.

### Test logs

The full set of `*.trx` files written under `TestResults/` is uploaded as a
workflow artifact (`test-results-<run-id>`) on every run, including failed
runs (`if: always()`). The artifact is for debugging only — open the failed
run on GitHub Actions, scroll to the **Artifacts** section, and download the
`.trx` files. Retention is 14 days.

### Secrets and artifacts

The workflow runs with `permissions: contents: read` and requires no
repository secrets. A fresh fork can open a PR and the same workflow runs
without any setup.

CI does **not** publish a user-facing artifact (no `dotnet publish`, no zip,
no GitHub Release, no NuGet push, no Docker build). The artifact-policy
boundary is set by [`docs/release-pipeline-design.md`](release-pipeline-design.md)
(UND-31); the tester-drop publish workflow is a separately filed
implementation issue.
