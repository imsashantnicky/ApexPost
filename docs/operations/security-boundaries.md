# Web security configuration and trust boundaries

## Integration money-order endpoint

`POST /api/integrations/money-orders` requires the `X-API-Key` header and authenticates as one configured active Counter Clerk. The key grants no branch by itself: the handler reloads `ApexPost:Integration:StaffId` from JSON staff data and issues claims from that staff member's current role-compatible assignment. Application authorization checks the requested branch against that trusted assignment. An absent key or staff identity makes integration authentication fail with 401. An empty integration IP allowlist denies the route with 403.

Store the key outside source control, for example:

```sh
dotnet user-secrets init --project src/ApexPost.Web/ApexPost.Web.csproj
dotnet user-secrets set "ApexPost:Integration:ApiKey" "<random-secret-at-least-32-characters>" --project src/ApexPost.Web/ApexPost.Web.csproj
dotnet user-secrets set "ApexPost:Integration:StaffId" "<active-counter-staff-guid>" --project src/ApexPost.Web/ApexPost.Web.csproj
```

Configure exact source IP addresses in `ApexPost:Security:IntegrationAllowedIPs`. The endpoint compares keys in constant time and does not write key material to logs or responses.

## Forwarded headers and IP allowlisting

Forwarded `X-Forwarded-For` and `X-Forwarded-Proto` values are accepted only when the immediate TCP peer matches an address in `ApexPost:Security:TrustedProxies`. The application limits processing to one proxy hop. Configure the actual reverse proxy's fixed egress address; do not add public client networks or trust all proxies. With no trusted proxy configured, forwarded headers do not change the client's effective IP or request scheme.

The integration allowlist uses the effective remote IP after trusted-proxy processing. The configured entries are exact IP addresses, not CIDR ranges. An empty list fails closed for the integration route. Health endpoints are not subject to this allowlist.

## Other controls

- `ApexPost:Maintenance:Enabled` enables 503 responses with the configured `Retry-After`; health routes remain available.
- `ApexPost:Security:ApprovedCorsOrigins` lists exact browser origins. An empty list disables cross-origin access.
- Login and integration rate limits are partitioned by the effective remote IP and configured under `ApexPost:Security`.
- Production uses HTTPS redirection, HSTS, and secure authentication/antiforgery cookies. Development cookies permit HTTP for local launch profiles.
- MVC state-changing actions retain global antiforgery validation. The API-key-only integration endpoint is exempt because it does not authenticate through browser cookies.

Do not place passwords, API keys, identity-document numbers or payment-sensitive values in logs. Do not forward client-supplied IP headers through a proxy unless that proxy overwrites them and is explicitly trusted here.
