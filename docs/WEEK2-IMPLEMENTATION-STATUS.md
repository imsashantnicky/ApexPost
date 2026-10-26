# Week 2 compliance implementation status

This document distinguishes implemented changes from work that still requires code, testing, or confirmation against the trainer's exact rubric.

## Changes in this branch

- Validate inbound `X-Correlation-ID` values. Only short ASCII letters, digits, hyphens, underscores, and dots are accepted; invalid or oversized values are replaced.
- Add tests for safe, invalid, and oversized correlation IDs.
- Add role and a validated branch identifier to the MVC action audit metadata when those claims exist. Request bodies, query values, arbitrary headers, and model values remain excluded.
- Run the GitHub Actions build/test workflow for both `master` and `main`, since this repository's default branch is `master`.

## Verification required on a Mac

Run from the repository root:

```bash
dotnet --list-sdks
dotnet restore ApexPost.sln
dotnet build ApexPost.sln --configuration Release
dotnet test ApexPost.sln --configuration Release
```

Do not mark this branch complete until all commands pass and failures have been fixed. The GitHub connector can commit changes, but it does not execute the .NET build in the user's Mac environment.

## Remaining requirement checklist

- [ ] Confirm exact seed-data counts against the lab handout.
- [ ] Add integration tests that exercise the actual application pipeline for security headers, correlation IDs, protected pages, authorization failures, and rate limiting.
- [ ] Confirm and implement API-key validation for the specific partner/API routes that require it.
- [ ] Confirm and implement trusted IP allowlisting and maintenance mode if required by the rubric.
- [ ] Configure CORS with explicit allowed origins only if cross-origin clients are required.
- [ ] Verify branch-scoped authorization in every data-access workflow; adding a branch claim to audit metadata does not itself enforce branch isolation.
- [ ] Add domain-level audit events for sensitive business operations (inventory adjustment, counter transaction, shipment lifecycle, money-order verification/completion, void/refund, and staff/branch changes).
- [ ] Verify audit persistence under concurrent writes, process interruption, permissions, retention, and backup/restore. JSON storage is not tamper-proof and is not a transactional database.
- [ ] Add tests for overselling stock, shipment weight/service limits, money-order verification rules, transaction totals/tax, and role restrictions.
- [ ] Check the required C# 14 examples and explain why each is appropriate rather than adding language features just for appearance.
- [ ] Build with warnings reviewed; do not silently disable analyzer warnings.
- [ ] Compare every deliverable and seed-data count to the original assignment rubric before submission.

## Important

These commits are a focused improvement pass, not a guarantee of 100% marks or production security. No build or test execution is claimed here.
