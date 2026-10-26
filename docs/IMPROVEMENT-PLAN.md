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

## Audit follow-up status

The audit requirements below were implemented in Step 4 and verified by the Release build and test suite. The MVC audit filter records controlled action metadata only. It does not log request bodies, query values, arbitrary route values, headers, or model contents. Audit persistence failures propagate through the global safe exception handler instead of being swallowed.

Sensitive application mutations write a `Started` event before business persistence and a terminal outcome afterward. The JSON writer uses the existing append-only audit interface, strict metadata allowlisting, same-process concurrent-write serialization, and temporary-file replacement. Malformed audit JSON is preserved and causes a visible internal failure. JSON files remain non-tamper-resistant and cannot atomically commit business and audit records across separate files; unresolved `Started` events require reconciliation. See `DESIGN.md`, ADR-006, and `AUDIT-SAMPLE.json`.

The baseline CSP is intentionally restrictive. Verify the actual views and static assets in a browser; if the application uses inline scripts/styles or third-party origins, prefer CSP nonces/hashes and explicit allowlists rather than broadly weakening the policy.

## Required follow-up before marking the assignment complete

1. Build the solution with the .NET 10 SDK and fix every compiler/analyzer warning.
2. Filter registration, audit output/failure handling, concurrent writes, metadata rejection, and cancellation are covered by tests in this step.
3. Verify security headers in the integration-test host, not only in an isolated middleware test.
4. Add/verify API-key validation, IP allowlisting, forwarded-header trust boundaries, maintenance-mode behavior, CORS restrictions, login/partner rate limits, and security exception responses.
5. Verify authentication claims and authorization at branch scope; a role-only policy is not sufficient to isolate branch data.
6. Sensitive inventory, transaction, shipment, money-order, identity-verification, staff, and branch operations emit application-boundary audit records; authorization denials are recorded at OperationalAccess.
7. Tests verify safe metadata rejection and append-only behavior through the writer contract. Tamper resistance, retention, and cross-process locking remain unsupported by JSON persistence.
8. Complete all domain/integration scenarios and verify seed-data counts, reports, ADRs, and AUDIT-SAMPLE.json against the lab rubric.
9. Review the actual C# 14 feature usage and explain the reason for each feature in the design documentation.

## Local verification commands

    dotnet --list-sdks
    dotnet restore ApexPost.sln
    dotnet build ApexPost.sln --configuration Release
    dotnet test ApexPost.sln --configuration Release

Do not claim 100% completion until the build, tests, and every rubric item have been checked against the assignment.
