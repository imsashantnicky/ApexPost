# ApexPost improvement plan: security, auditing, C# 14, and MVC filters

## Scope of this change

This branch adds an initial, reviewable improvement set based on the trainer feedback:

- MVC AuditActionFilter, ValidationActionFilter, and PerformanceActionFilter.
- Global DI registration of the three filters.
- A baseline SecurityHeadersMiddleware.
- Production-only secure-cookie policy (SecurePolicy.Always outside Development).
- A C# 14 extension-member example for non-blank string checks.
- A C# 14 field-backed property in MoneyOrderAmount.
- Focused tests for the security headers and extension member.

## Important limitations

These changes are not a claim that the full lab is complete or that the app has passed its build/tests. The repository still needs a full local build and test run.

The MVC audit filter records controlled action metadata only. It deliberately does not log request bodies, query-string values, arbitrary route values, headers, or model contents. Audit persistence is attempted without replacing an action's original result if the audit store itself fails. That fallback should be paired with operational alerting because an audit write failure is still a security/operations incident.

The current JSON audit writer should be reviewed for append-only guarantees, concurrent writes, file permissions, retention, tamper evidence, and crash recovery. JSON files alone do not provide tamper-proof audit storage or atomic multi-file business transactions.

The baseline CSP is intentionally restrictive. Verify the actual views and static assets in a browser; if the application uses inline scripts/styles or third-party origins, prefer CSP nonces/hashes and explicit allowlists rather than broadly weakening the policy.

## Required follow-up before marking the assignment complete

1. Build the solution with the .NET 10 SDK and fix every compiler/analyzer warning.
2. Run all test projects and add tests for filter registration, audit output, failures, and request cancellation.
3. Verify security headers in the integration-test host, not only in an isolated middleware test.
4. Add/verify API-key validation, IP allowlisting, forwarded-header trust boundaries, maintenance-mode behavior, CORS restrictions, login/partner rate limits, and security exception responses.
5. Verify authentication claims and authorization at branch scope; a role-only policy is not sufficient to isolate branch data.
6. Ensure every sensitive operation (inventory adjustments, transactions, shipping, money orders, voids, refunds, staff/branch changes) emits a domain audit event with actor, branch, outcome, timestamp, and correlation ID.
7. Test audit records for secret/PII leakage and confirm the store is append-only.
8. Complete all domain/integration scenarios and verify seed-data counts, reports, ADRs, and AUDIT-SAMPLE.json against the lab rubric.
9. Review the actual C# 14 feature usage and explain the reason for each feature in the design documentation.

## Local verification commands

    dotnet --list-sdks
    dotnet restore ApexPost.sln
    dotnet build ApexPost.sln --configuration Release
    dotnet test ApexPost.sln --configuration Release

Do not claim 100% completion until the build, tests, and every rubric item have been checked against the assignment.
