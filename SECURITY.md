# Security design – ApexPost API (Week 4 Part D)

This document explains how callers prove who they are to `ApexPost.Api`, what each of them may do, and what has to
change before the API meets a real identity provider. All names, keys and hosts in this repository are fictional.

## 1. Who calls the API

| Caller | Credential | Header | Checked by | Can do |
| --- | --- | --- | --- | --- |
| Courier app (MAUI) | JWT access token, role `Courier`, claim `courierId` | `Authorization: Bearer <jwt>` | JWT bearer handler + role attributes + `ShipmentTrackingService` | Read shipments, upload **its own** scans, move a status forward |
| Back office | JWT access token, role `Admin` | `Authorization: Bearer <jwt>` | JWT bearer handler + role attributes | Read, create, replace and delete shipments |
| Partner system | API key per partner | `X-Api-Key: <key>` | `PartnerApiKeyFilter` | Read one shipment at `GET /api/v2/partner/shipments/{trackingNumber}` (status, city, latest scan; no recipient details) |

Every other caller gets 401. A valid caller with the wrong role gets 403. Both answers are RFC 9457 ProblemDetails
with a `traceId`, so a client can always read the reason.

| Route (v1, v1 alias `/api/shipments`, v2) | Rule |
| --- | --- |
| `GET` shipments, one shipment, scans | any valid JWT |
| `POST .../scans` | role `Courier`, and every scan's `courierId` equals the token's `courierId` claim |
| `PATCH .../status` | role `Courier` |
| `POST`, `PUT`, `DELETE` shipment | role `Admin` |
| `GET /api/v2/partner/shipments/{trackingNumber}` | a known partner key; a JWT does not open it |
| `POST /dev/token` | Development only (404 elsewhere); anonymous; fictional couriers CR-101 and CR-102 |

### Where each check lives

- **Authentication** (`Composition/ApiSecurityRegistration.cs`): `AddAuthentication().AddJwtBearer()`. Issuer,
  audience, lifetime (30 s clock skew, expiry required) and signing key are all validated; a missing setting refuses
  every token instead of skipping the check. Inbound claim mapping is off, so `role`, `courierId` and `sub` keep
  their short names.
- **Authorization**: `[Authorize]` on every shipment controller and `[Authorize(Roles = ...)]` on the actions above.
  There is no fallback policy, so an unknown route still answers 404 instead of 401.
- **Ownership**: the courierId rule is a business rule, so it lives in `ShipmentTrackingService` behind the
  `ICurrentCourier` port, not in a controller. A batch with a scan of another courier is refused as a whole (403)
  before anything is read or stored, and the 403 comes before a 404, so a courier cannot probe which tracking
  numbers exist.
- **401/403 bodies**: `Security/BearerProblemEvents.cs` (`OnChallenge`, `OnForbidden`) and
  `Security/PartnerApiKeyFilter.cs`. Neither ever echoes or logs a token or key; the filter logs the partner
  *name* only.

## 2. The OAuth 2.0 flows we would use with a real identity provider

Today the tokens come from `dotnet user-jwts` and the Development-only `/dev/token`. In production the API would
only **validate** tokens; an identity provider (IdP) such as Microsoft Entra ID, Auth0, Okta or Keycloak **issues**
them.

### Courier app: Authorization Code flow with PKCE

1. The app creates a random `code_verifier` and sends its SHA-256 hash (`code_challenge`) with the user to the IdP
   sign-in page in the system browser.
2. The courier signs in at the IdP (password, MFA). The app never sees the password.
3. The IdP redirects back to the app with a short-lived **authorization code**.
4. The app exchanges the code **plus the original `code_verifier`** for tokens. A stolen code is useless without the
   verifier, which is why PKCE replaces a client secret: a mobile app cannot keep a secret.
5. The app stores the tokens in `SecureStorage` (Keychain / Android Keystore) and sends the access token as
   `Authorization: Bearer`.

### Partner systems: Client Credentials flow

A partner is a server, not a person. It authenticates to the IdP with its own client id and secret (or, better, a
certificate / signed JWT assertion) and receives an access token for the scope `tracking.read`. The partner route
would then check that scope instead of an API key.

### The three artefacts

| Term | What it is | Lifetime | Where it is kept |
| --- | --- | --- | --- |
| Identity provider | The only service that knows passwords and signing keys; it issues tokens and publishes its public keys | – | Hosted service |
| Access token (JWT) | A signed, self-contained statement: issuer, audience, expiry, subject, roles, `courierId`. The API checks it without calling the IdP | Short: 5–60 minutes | Memory / SecureStorage on the device |
| Refresh token | An opaque credential that gets a new access token without signing in again; the IdP can revoke it and rotates it on use | Long: days | SecureStorage only, never sent to the API |

## 3. Why API keys are weaker than OAuth tokens

- **Long-lived and static.** A key works until someone rotates it by hand; an access token expires in minutes.
- **Bearer secret on every request.** The same value travels with every call, so one leak (a log line, a proxy, a
  screenshot) gives full access until rotation.
- **No user, no scopes.** A key identifies a partner system, not a person, and carries no fine-grained permissions
  or expiry inside it; the server must look everything up.
- **Shared secret.** The server must hold the key itself (or a hash) and compare it; a JWT is checked with a public
  key that cannot create new tokens.
- **No standard revocation or rotation protocol.** Rotation means coordinating with each partner.

The API reduces the risk: keys live only in configuration (`PartnerKeys:<Partner>` in user-secrets or environment
variables, never in source), they are compared after SHA-256 hashing with `CryptographicOperations.FixedTimeEquals`
against every configured key (no timing hint about content, length or partner), an empty value disables a partner,
the route is read-only and returns no recipient details, and the key is never logged. It is still the weakest
credential in the system, which is why it is limited to one read-only endpoint.

## 4. What changes for a real identity provider

| Today (development) | With a real IdP |
| --- | --- |
| Signing key created by `dotnet user-jwts`, kept in user-secrets (`Authentication:Schemes:Bearer:SigningKeys`) | **No signing key in the API at all.** Set `Authentication:Schemes:Bearer:Authority` (for example `https://login.apexpost.example/`); the handler downloads `/.well-known/openid-configuration` and the JWKS public keys and refreshes them automatically |
| Symmetric HS256 key shared by issuer and API | Asymmetric RS256/ES256: the IdP signs with a private key, the API only holds public keys |
| `ValidIssuer` `dotnet-user-jwts`, audiences = localhost URLs | `ValidIssuer` = the IdP issuer, `ValidAudiences` = the API's registered app id URI (`appsettings.json` already holds fictional production values) |
| `POST /dev/token` signs tokens for CR-101/CR-102 | Not available: the controller is only registered in Development (404 elsewhere), and it would be deleted once an IdP exists. Couriers sign in at the IdP (Authorization Code + PKCE) |
| Roles and `courierId` added by hand on the command line | The IdP adds them from its directory (app roles, a custom `courierId` claim) |
| Key rotation = run `dotnet user-jwts` again | **Key rotation** is handled by the IdP: it publishes the next key in the JWKS before it starts signing with it, the API picks it up from the metadata, and old tokens stay valid until they expire |
| `RequireHttpsMetadata` irrelevant | Keep `RequireHttpsMetadata = true` (the default) so the metadata and keys are only fetched over HTTPS |
| Partner API keys | Client Credentials tokens with a `tracking.read` scope; until then, rotate keys by adding the new key under a second name, letting the partner switch, then deleting the old entry |

Nothing else in the API changes: controllers, role attributes, `ICurrentCourier` and the tests that check them stay
as they are, because they depend only on validated claims.

## 5. Transport security: HTTPS, HSTS and why credentials never go over plain HTTP

A bearer token or API key is a password that works for anyone who holds it. Over plain HTTP every router, Wi-Fi
hotspot and proxy on the way can read and replay it, and can also change the response. TLS encrypts and
authenticates the connection, so the credential is only readable by the API.

- `app.UseHttpsRedirection()` runs in every environment: a plain HTTP request is redirected (307) to HTTPS once the
  HTTPS port is known (the `https` launch profile listens on `https://localhost:7310`).
- **A redirect does not protect the first request.** The client has already sent its `Authorization` or `X-Api-Key`
  header in clear text before it sees the redirect. Clients must therefore be configured with an `https://` base
  URL; the redirect only helps browsers and humans who typed `http://`.
- `app.UseHsts()` runs outside Development and sends `Strict-Transport-Security: max-age=31536000;
  includeSubDomains`. A browser that has seen it uses HTTPS for the host for a year without trying HTTP first,
  which also blocks SSL-stripping attacks. It is off in Development because pinning `localhost` to HTTPS breaks every
  other local project, and ASP.NET Core never sends it for `localhost` anyway.
- Behind a reverse proxy or load balancer that terminates TLS, the API must also use `UseForwardedHeaders` with the
  proxy's address in `KnownProxies`, so it sees the original scheme; otherwise it would redirect forever or skip HSTS.
- Tests: `HttpsTests` checks HSTS in Production, no HSTS in Development, and the HTTP → HTTPS redirect in both.

## 6. The MAUI app and the development certificate

On a developer machine the API uses the ASP.NET Core development certificate (`dotnet dev-certs https --trust`).
Windows and macOS trust it after that command, but an Android emulator or iOS simulator does not, so an HTTPS call
from the app to `https://10.0.2.2:7310` (Android's alias for the host machine) fails certificate validation.

The courier app (a later Part D step) handles this with one narrow exception:

- The bypass is compiled **only in `#if DEBUG`**, so a Release build never contains it.
- It accepts an untrusted certificate **only** when the request host is `localhost` or `10.0.2.2`; every other host
  is validated normally, also in Debug.
- Release builds always validate certificates and talk to the real HTTPS host. Disabling validation in Release, or
  for any other host, is forbidden by the project rules.
- Tokens are kept in `SecureStorage`, never in preferences or plain files, and are never logged.

## 7. Secrets and configuration

| Setting | Committed? | Where it lives |
| --- | --- | --- |
| `Authentication:Schemes:Bearer:ValidIssuer`, `ValidAudiences` | yes | `appsettings.json`, `appsettings.Development.json` |
| `Authentication:Schemes:Bearer:SigningKeys` | **no** | user-secrets, written by `dotnet user-jwts` |
| `PartnerKeys:<Partner>` | **no** | user-secrets / environment variables |
| `ConnectionStrings:ApexPostDb` | **no** | user-secrets / environment variables |

Set a partner key for local testing (any long random value; the name is the partner):

```bash
dotnet user-secrets set "PartnerKeys:ParcelTrackDemo" "$(openssl rand -hex 24)" --project src/ApexPost.Api
dotnet user-secrets list --project src/ApexPost.Api
```

Tests never use these secrets: `ApiFactory` creates a signing key per test run and uses fictional partner keys in
memory (`tests/ApexPost.Api.Tests/Support/TestTokens.cs`, `TestPartnerKeys.cs`).

## 8. Evidence in the tests

| Test class | Proves |
| --- | --- |
| `Security/AuthenticationTests` | 401 for a missing, expired, wrongly signed, foreign-issuer, foreign-audience or malformed token; any role may read |
| `Security/AuthorizationTests` | 403 for the wrong role; 403 for a scan of another courier or a token without `courierId`; success per role |
| `Security/DevTokenTests`, `Security/ProductionSecurityTests` | `/dev/token` issues verifiable Courier tokens in Development and does not exist in Production |
| `Security/PartnerApiKeyTests`, `Security/PartnerKeyStoreTests` | 200 with a valid key; 401 for a missing, wrong, case-changed or truncated key; a JWT does not open the partner route; empty keys are disabled |
| `Security/HttpsTests` | HSTS outside Development only; HTTP is redirected to HTTPS |
| `Documentation/SecuritySchemeTests` | Both documents declare Bearer; v2 also declares ApiKey and the partner operation requires only ApiKey |
