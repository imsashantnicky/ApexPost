# ApexPost

ApexPost is a secure ASP.NET Core MVC application for branch operations, inventory, money orders, and reporting. The solution is designed around a layered architecture: Domain for business invariants, Application for use cases and abstractions, Infrastructure for JSON persistence and adapters, and Web for MVC endpoints, authentication, and browser assets.

## Prerequisites

- .NET SDK 10.0.x
- A supported operating system for .NET 10 (Windows/macOS/Linux)
- A local writable directory for application data files
- A browser for local verification
- Optional: GitHub Actions CI runner or Azure/App Service host for deployment

## Quick start

From the repository root:

```bash
dotnet restore ApexPost.sln
dotnet build ApexPost.sln --configuration Release
dotnet test ApexPost.sln --configuration Release
```

To run the app locally:

```bash
dotnet run --project src/ApexPost.Web/ApexPost.Web.csproj --urls http://localhost:5100
```

The app is configured for HTTPS via ASP.NET Core middleware and secure cookie settings. In a local development environment, the app will still redirect HTTP traffic to HTTPS if the environment is configured for HTTPS.

## Configuration

The application reads JSON persistence settings from `ApexPost:JsonData` and seed settings from `ApexPost:JsonSeed`.

Example configuration in `src/ApexPost.Web/appsettings.json`:

```json
{
  "ApexPost": {
    "JsonData": {
      "DataDirectory": "App_Data"
    },
    "JsonSeed": {
      "Enabled": false
    }
  }
}
```

For local development or testing, override with environment variables or an appsettings file such as `appsettings.Development.json`:

```bash
set ApexPost__JsonData__DataDirectory=C:\ApexPost\data
set ApexPost__JsonSeed__Enabled=true
```

On Linux/macOS:

```bash
export ApexPost__JsonData__DataDirectory="$HOME/ApexPost/data"
export ApexPost__JsonSeed__Enabled=true
```

A sample production-style configuration file is provided at `appsettings.Production.example.json`.

## JSON data directory and backup/recovery guidance

ApexPost stores files in a configurable directory rather than embedding runtime state in source control. The default is `App_Data` under the Web project, but production deployments should use a dedicated writable directory with strict filesystem permissions.

Recommended practices:

- Store the application data directory outside the source tree in non-production environments.
- Back up the directory regularly and keep snapshots before any operator action that alters branch, payment, shipment, or inventory data.
- Restore only the exact backup set that matches the same app version and JSON schema.
- Keep the data directory separate from static web content and secret stores.
- Ensure the directory is writable only by the application identity.

Critical note: the repository uses JSON-backed persistence, not a relational database. Backup/recovery is a file-level operational process and should be treated as such until a database migration is implemented.

## Seed-data setup

Seed data is controlled by `ApexPost:JsonSeed:Enabled`.

- Set to `true` only for development or demonstration environments.
- The initializer writes deterministic branch, staff, catalogue, pricing, and inventory data when the target directory is empty.
- Seed data intentionally does not include accounts, passwords, card data, or provider credentials.
- Do not enable seed mode against a production data directory or a directory that contains real operational data.
- If the directory contains unsupported or malformed JSON files, the initializer stops and raises a clear persistence error instead of silently replacing data.

To seed an empty local directory:

```bash
set ApexPost__JsonData__DataDirectory=C:\ApexPost\data
set ApexPost__JsonSeed__Enabled=true
dotnet run --project src/ApexPost.Web/ApexPost.Web.csproj
```

## First-user provisioning

The application does not ship with a default production account or embedded password.

On a clean system, before any local account exists, open:

```text
https://localhost:5100/Account/Bootstrap
```

Create the first administrator account with a strong password (12+ characters, unique to that environment). The bootstrap page is only available while no accounts exist.

After the first login:

- Create additional users from `/Account/Provision`.
- Use the least privileged role needed for each user.
- Roles are limited to `Administrator`, `BranchManager`, and `CounterClerk`.
- Use a dedicated admin account rather than a shared or personal account.

## Authentication and authorization

- ASP.NET Core cookie authentication is used for MVC sign-in.
- Passwords are never stored in plaintext; the app persists only PasswordHasher output.
- Login is protected by rate limiting and antiforgery tokens.
- Cookies are `HttpOnly`, secure-only, and configured for HTTPS use.
- Access control uses role policies for management and administrator-only actions.
- Unauthorized users are redirected to `/Account/AccessDenied`.

### Supported roles and permissions

ApexPost supports five application roles:

| Role | Purpose | Typical access |
| --- | --- | --- |
| `Administrator` | Full administration and provisioning | Full management access, user provisioning, and report access |
| `BranchManager` | Branch operations lead | Branch and staff management, inventory, operational workflows |
| `CounterStaff` | Counter sales and service desk | Transactions, payments, shipments, counter operations |
| `InventoryStaff` | Warehouse and stock handling | Inventory movement, receiving, issuing, stock actions |
| `Auditor` | Read-only monitoring and review | Reports, audit records, non-mutating review only |

The `Auditor` role is intentionally read-only and cannot access state-changing controllers or actions.

### Development accounts and secure provisioning

The project does not ship a default username or password. Each environment must be provisioned explicitly.

For local development, enable development role accounts through configuration or user secrets and set one username/password pair per role. The app only creates the configured accounts when `ApexPost:DevelopmentAccounts:Enabled=true` and the environment is `Development`.

Example PowerShell values:

```powershell
# One-time local values in user secrets or environment variables
$env:ApexPost__DevelopmentAccounts__Enabled = "true"
$env:ApexPost__DevelopmentAccounts__Administrator__UserName = "admin.dev"
$env:ApexPost__DevelopmentAccounts__Administrator__Password = "ChangeThis-StrongDevPass-123!"
$env:ApexPost__DevelopmentAccounts__BranchManager__UserName = "branch.dev"
$env:ApexPost__DevelopmentAccounts__BranchManager__Password = "ChangeThis-StrongDevPass-456!"
$env:ApexPost__DevelopmentAccounts__CounterStaff__UserName = "counter.dev"
$env:ApexPost__DevelopmentAccounts__CounterStaff__Password = "ChangeThis-StrongDevPass-789!"
$env:ApexPost__DevelopmentAccounts__InventoryStaff__UserName = "inventory.dev"
$env:ApexPost__DevelopmentAccounts__InventoryStaff__Password = "ChangeThis-StrongDevPass-101!"
$env:ApexPost__DevelopmentAccounts__Auditor__UserName = "auditor.dev"
$env:ApexPost__DevelopmentAccounts__Auditor__Password = "ChangeThis-StrongDevPass-202!"
```

Equivalent user-secrets configuration:

```powershell
dotnet user-secrets set "ApexPost:DevelopmentAccounts:Enabled" "true"
dotnet user-secrets set "ApexPost:DevelopmentAccounts:Administrator:UserName" "admin.dev"
dotnet user-secrets set "ApexPost:DevelopmentAccounts:Administrator:Password" "ChangeThis-StrongDevPass-123!"
# Repeat for BranchManager, CounterStaff, InventoryStaff, and Auditor.
```

The app will create each configured account once and never overwrite an existing account or password. If a role is configured without both `UserName` and `Password`, startup fails with a clear error. In production, startup skips development account initialization entirely.

### Portal login

- URL: `https://localhost:5100/Account/Login` (or the configured local URL)
- Sign in with a valid user name and password for the role you created.
- After successful login, the app redirects to a role-specific dashboard route such as `/dashboard/admin`, `/dashboard/branch`, `/dashboard/counter`, `/dashboard/inventory`, or `/dashboard/auditor`.
- New accounts are provisioned by an Administrator from `/Account/Provision`.
- Only Administrators can assign roles or create users.

## Browser-side IndexedDB and offline behavior

ApexPost uses browser-side IndexedDB only for non-sensitive offline assistance. This is intentionally limited to minimal local queue metadata and drafts. The implementation rejects payloads that resemble credential, token, session, document, payment, or identity data.

Key rules:

- Local queue entries are explicitly marked as `pending` and are never presented as confirmed server transactions.
- Browser storage is not used for passwords, authentication cookies, identity documents, or payment secrets.
- The UI clearly separates offline queue state from server records.

Manual verification checklist:

1. Open the dashboard in a browser and confirm the offline banner appears only when the browser is offline or local queue activity exists.
2. Inspect DevTools and verify `window.ApexPostBrowserStorage` exposes the expected API.
3. Validate that a harmless draft is saved successfully, while a payload containing `password`, `token`, or similar sensitive fields is rejected.
4. Confirm local queue entries remain in a `pending` state until server synchronization is confirmed.
5. Confirm no credentials or auth tokens appear in IndexedDB storage.

## GitHub Actions workflow

The repository includes a CI workflow at `.github/workflows/ci.yml` that:

- runs on pushes to `main` and pull requests to `main`
- sets up .NET 10
- restores dependencies
- builds the solution in Release mode
- runs the entire test suite in Release mode

This workflow is suitable as a baseline for a production delivery pipeline and can be extended with artifact publication, code coverage, or deployment steps later.

## Secret handling and repository hygiene

- Do not commit passwords, API keys, client secrets, local certificate files, or appsettings files containing live values.
- Keep secrets in a secret store or environment variables, not in source control.
- The repository ignores common local secret and generated-file patterns via `.gitignore`.
- Review Git status before sharing a branch or preparing a handoff.

## Testing

From the root:

```bash
dotnet test ApexPost.sln --configuration Release
```

For an explicit build/test flow:

```bash
dotnet build ApexPost.sln --configuration Release
dotnet test ApexPost.sln --configuration Release --no-build
```

## Troubleshooting

### App data directory is not writable

- Confirm the configured directory exists or is creatable by the current user.
- Ensure the application identity has write access.
- Check the configured `ApexPost:JsonData:DataDirectory` value.

### Bootstrap page is not available

- This means at least one account already exists in the configured data directory.
- Remove the configured data directory only if you intentionally want a clean reset and you have a verified backup.

### Seed data does not initialize

- Ensure `ApexPost__JsonSeed__Enabled=true` or the matching appsettings value is set.
- The app only seeds an empty directory; it will not overwrite existing valid JSON data.

### Malformed JSON error

- Stop the app and inspect the JSON file in the configured directory.
- Restore from a known-good backup or delete the invalid file only if the data is intentional to discard.
- Confirm that the JSON settings point to the intended data directory.

### App fails to start

- Verify `dotnet --info` shows .NET 10 SDK.
- Confirm `global.json` matches the installed SDK feature band.
- Check the logs for startup exceptions and verify HTTPS configuration is valid.

## Known limitations and future migration plan

This repository currently uses JSON file persistence for operational data. That is appropriate for local development and controlled internal deployment, but it is not a long-term production database strategy.

Current limitations:

- JSON persistence is file-based and process-scoped; it does not provide transactional multi-table integrity.
- There is no distributed locking or multi-instance coordination for production deployments.
- Operational audit and business records are stored in the same local file model; a future database migration should isolate these concerns.
- Browser storage is deliberately limited to safe offline workflows, but it still remains local client-side state.

Forward-looking migration plan:

1. Move core operational data to a managed relational database or document store.
2. Add explicit SQL or NoSQL schemas and indexes for branches, inventory, shipments, money orders, and audit records.
3. Replace ad-hoc file locking with durable transactional patterns and connection pooling.
4. Add database-backed migration tooling and automated backups.
5. Add environment-specific deployment secrets and secret rotation workflows.
6. Keep the current MVC and authentication model while replacing persistence only behind the existing application ports.

## Security review status

This solution has been reviewed for the main MVC security controls: authentication, authorization, antiforgery, validation, JSON persistence, browser storage, direct file access, and configuration hygiene. The codebase uses ASP.NET Core cookie authentication, secure cookie settings, role policies, rate limiting, and hash-only account storage.

This is a focused engineering review rather than a comprehensive external security audit. It does not replace a full threat model, live production penetration test, or dependency-vulnerability scan.

## Documentation map

- `README.md` – setup, configuration, run, and test instructions
- `DESIGN.md` – architecture, data flow, key decisions, and limitations
- `docs/architecture/*.md` – architecture conventions and layering guidance
- `docs/operations/*.md` – operational notes such as authentication and seed data
- `.github/workflows/ci.yml` – automated build and test pipeline
