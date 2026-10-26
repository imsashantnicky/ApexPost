# Authentication and first-user setup

ApexPost uses ASP.NET Core cookie authentication and the framework password hasher. Passwords are stored only as password hashes in `accounts.json`; no default account or password is seeded.

On a new installation, open `/Account/Bootstrap` before any account exists and create the first administrator with a password of at least 12 characters. The bootstrap page becomes unavailable after the first account is created. Administrators and Branch Managers can access management workflows; Counter Clerks cannot.

Only an Administrator can open `/Account/Provision` to create additional local accounts. User names are 3--80 characters and may contain only letters, digits, `.`, `_`, or `-`; roles are `Administrator`, `BranchManager`, `CounterStaff`, `InventoryStaff`, and `Auditor`. Provisioning links each account to an active staff record, and the selected role must match that staff member's operational role where applicable. The staff ID and current branch claim are resolved from server-side records; posted branch IDs are never accepted as authorization evidence. The browser never receives a password hash.

At sign-in, the cookie contains the account role plus the linked staff ID and assigned branch resolved from the staff repository. Operational use cases re-load the staff record and branch for every operation, so suspended, terminated, missing, or transferred staff lose operational access even while an older cookie remains valid. Counter and inventory roles stay within their assigned branch. Administrator access to another branch is allowed explicitly and writes an audit event. The initial bootstrap administrator has no staff link; it can perform setup and provisioning but cannot perform operational writes until an active staff-linked account is provisioned.

Cookies are HTTP-only, secure-only, same-site Lax, and expire after eight hours with sliding renewal. Login posts are limited to five attempts per minute per server rate-limiter partition. Run behind HTTPS in every deployed environment.

## Organization sign-in

The current deployment uses local accounts because no organization identity-provider metadata has been supplied. To use an organization login, configure an OpenID Connect handler with the provider's authority/metadata address, client ID, client secret or certificate, callback path, and signed-out callback path. Map the provider's stable subject claim to the local user identity and map only an approved groups/roles claim to the three ApexPost roles. Keep the client secret outside source control (for example, in a secret store), require HTTPS callback URLs, and retain the local bootstrap administrator as a recovery path until the external login has been proven in the target environment. Do not enable an external handler merely by accepting arbitrary role claims.
