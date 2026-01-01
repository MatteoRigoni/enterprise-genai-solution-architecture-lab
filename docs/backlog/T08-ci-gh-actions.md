# T08 - CI with GitHub Actions

## Goal
Automate build + tests + basic eval smoke.

## Scope
- ci.yml:
  - dotnet restore/build
  - dotnet test
  - run EvalRunner smoke on small dataset (10 qs)
  - upload eval report artifact
- Add guidance for branch protection.

## Acceptance Criteria
- CI runs on PR and main.
- Fail build if tests fail or eval smoke below threshold.
- Artifacts stored.

## Files / Areas
- .github/workflows/ci.yml
- eval/README.md: thresholds explanation
- docs/quality.md: mention CI gates

## DoD
- CI green on main
- Clear failure messages

## Demo
Open PR with breaking change -> CI fails

## Sources (passive)
- GitHub Actions official docs (workflow syntax)
- YouTube: “CI for .NET GitHub Actions”
- Blog: quality gates for AI systems
