# CityLeague

Organize sports games and fill the pitch. **CityLeague** is a .NET MAUI (Android/iOS) app with an ASP.NET Core backend for planning football matches: an organizer creates an event, contacts join, and everyone claims positions on a live, interactive football field. Player stats are tracked across matches.

This repository contains the **Phase 1 MVP** focused on **football** (5v5 through 11v11). Padel, tennis and basketball appear as **"Coming soon"** tabs.

## Features (MVP)

- Sign in with **Google**, **Microsoft**, **Apple**, a username + password, or a dev sign-in for local testing
- Pick a globally **unique handle** (`@alex_k`)
- Add **contacts** by handle search (request / accept)
- Create a **football event** (5v5–11v11) with an optional recurring **series**
- Invite contacts; every participant can invite their own contacts
- Claim positions on a **SkiaSharp football field** with **real-time** updates (SignalR)
- **Result gating**: you can't start the next match in a series until the previous result is submitted
- Submit a score → **player stats** (played / won / lost / drawn) update
- Profile with avatar (image upload or initials fallback) and per-sport stats

Deferred to Phase 2 (schema stubs already exist): leagues, team logos, team standings, multi-sport field UIs, push notifications.

## Solution layout

```
CityLeague.sln
├── src/
│   ├── CityLeague.Core/            # Entities, enums, DTOs, formation templates, validation
│   ├── CityLeague.Infrastructure/  # EF Core DbContext + migrations, JWT, avatar storage
│   ├── CityLeague.Api/             # ASP.NET Core Web API + SignalR hub
│   └── CityLeague.App/             # .NET MAUI app (net10.0-android; net10.0-ios)
└── tests/
    ├── CityLeague.Core.Tests/      # Formation + handle validation unit tests
    └── CityLeague.Api.Tests/       # Integration tests (gating, claim conflict, stats)
```

## Prerequisites

- .NET SDK 10.0+
- MAUI workloads: `dotnet workload install maui`
- Android: Android SDK + an emulator (or a device). iOS: a paired Mac.
- (Optional) `dotnet-ef` for migrations: `dotnet tool install --global dotnet-ef`

## Run the API locally

The API runs against **SQLite** out of the box (no database to install). The schema is created automatically on first run and reference data (sports + football formats) is seeded.

```bash
cd src/CityLeague.Api
dotnet run
```

- API: `http://localhost:5066`
- Swagger UI (Development): `http://localhost:5066/swagger`
- SignalR hub: `http://localhost:5066/hubs/events`

Auth defaults to **Dev mode**, so `POST /api/auth/exchange` accepts a simple payload (provider + email + display name) and returns an app JWT — no provider registration required for local development. Configure real Google / Microsoft / Apple sign-in when you want it; see [Authentication](#authentication).

> **Android emulator:** `localhost` inside the emulator is not your PC. The DEBUG app rewrites it to `http://10.0.2.2:5066` automatically. On a physical device, set `ApiSettings.BaseUrl` to your PC's LAN IP (e.g. `http://192.168.1.20:5066`) and allow the port through the firewall.

### Quick smoke test (PowerShell)

```powershell
$base = "http://localhost:5066"
$auth = Invoke-RestMethod "$base/api/auth/exchange" -Method Post -ContentType application/json `
  -Body (@{ provider="google"; email="alex@example.com"; displayName="Alex K" } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($auth.accessToken)" }
Invoke-RestMethod "$base/api/me/handle" -Method Post -Headers $h -ContentType application/json -Body (@{handle="alex_k"} | ConvertTo-Json)
Invoke-RestMethod "$base/api/sports" -Headers $h
```

## Run the MAUI app

The app reads its API base URL from `ApiSettings`:

- **Android emulator** → `http://10.0.2.2:5066` (loopback alias to the host machine)
- **iOS simulator** → `http://localhost:5066`

Cleartext HTTP is enabled for local development (Android `usesCleartextTraffic`, iOS ATS override). Use HTTPS in production.

```bash
# Android (from Windows/macOS/Linux with an emulator running)
dotnet build src/CityLeague.App/CityLeague.App.csproj -f net10.0-android -t:Run

# iOS (requires a Mac)
dotnet build src/CityLeague.App/CityLeague.App.csproj -f net10.0-ios -t:Run
```

On first launch, use the dev sign-in (any email). You'll be asked to pick a handle, then land on Home.

### Location picker (OpenStreetMap)

**Create → Map** uses a Leaflet map over **OpenStreetMap** tiles inside a WebView — no Google API key.

Football pitches tagged in OSM (`leisure=pitch|stadium|sports_centre` + `sport=soccer|football`) are highlighted as green markers when the map opens. Coverage in Greece is solid in cities (Athens, Thessaloniki, etc.) but incomplete in smaller towns — unnamed pitches show as “Football pitch”.

Typing in **Where** on Create Match filters those nearby city pitches into a dropdown. Data comes from the public Overpass + Nominatim APIs.

## Configuration reference (`appsettings.json`)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:SqlServer` | If set, the API uses Azure SQL / SQL Server and applies EF migrations. |
| `ConnectionStrings:Sqlite` | Used when `SqlServer` is empty (local dev); schema created via `EnsureCreated`. |
| `Jwt:SigningKey` | Symmetric key for signing app JWTs. **Override in production** (min 32 bytes). |
| `Jwt:Issuer` / `Jwt:Audience` | JWT issuer/audience. |
| `Auth:Mode` | `Dev` also accepts the unverified exchange payload (local only). Anything else (`Production`) requires a real provider token. |
| `Auth:PublicBaseUrl` | Public https base URL of the API. Required for Apple (and Google web clients), which can only redirect to https. |
| `Auth:MobileRedirectUri` | Custom scheme the app listens on. Defaults to `cityleague://auth/callback`. |
| `Auth:Google:*` / `Auth:Microsoft:*` / `Auth:Apple:*` | Social sign-in providers — see below. A provider with no `ClientId` is simply disabled. |
| `Auth:B2C:Authority` | B2C metadata authority (e.g. `https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/v2.0`). |
| `Auth:B2C:ClientId` | B2C application (client) id, validated as the token audience. |
| `AvatarStorage:Provider` | `Local` (disk, served from `/uploads`) or `Azure` (Blob Storage). |
| `AvatarStorage:ConnectionString` | Azure Storage connection string (when Provider = `Azure`). |
| `AvatarStorage:PublicBaseUrl` | Optional absolute base for avatar URLs. Leave empty to use the host the client called (recommended for emulators). |

## Authentication

Four ways in, and they can be combined:

- **Username + password** (`/api/auth/register`, `/api/auth/login`) — always available.
- **Google / Microsoft / Apple** — real OpenID Connect, enabled per provider by configuration.
- **Azure AD B2C** — if you prefer to federate through a B2C tenant.
- **Dev sign-in** — `Auth:Mode=Dev` only, trusts `{ provider, email, displayName }` without a token. Never enable in production.

`GET /api/auth/providers` returns whatever is configured, and the app builds its login screen from it: buttons for providers the server knows nothing about are hidden.

A filled-in example lives at `src/CityLeague.Api/appsettings.Social.example.json` — copy the values into user secrets or your host's app settings rather than committing them.

### How the social flow works

1. The app asks the API which providers are configured and where to send the user.
2. It opens the provider's authorize page (`WebAuthenticator`, or the native Sign in with Apple sheet on iOS) with PKCE, `state` and `nonce`.
3. The provider redirects back with an authorization code. Providers that only accept https redirects come back through `/api/auth/callback/{provider}`, which forwards to the app's custom scheme.
4. The app posts the code to `POST /api/auth/exchange`. The API redeems it at the provider's token endpoint, verifies the `id_token` against the provider's published signing keys, provisions or links the user, and returns app JWTs.

Client secrets stay on the server; the app only ever holds the client id it was told to use.

### Google

Chrome Custom Tabs generally **cannot** hop from an https bridge page back into a custom scheme, so prefer a public client that redirects straight into the app:

1. In [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services → Credentials**, create an OAuth client that allows the redirect URI `cityleague://auth/callback` (or set `Auth:Google:RedirectUri` to your reverse-DNS / `com.googleusercontent.apps.…` URI).
2. Configure the API **without** a client secret (PKCE only):
   ```json
   "Auth": {
     "Google": {
       "ClientId": "<id>.apps.googleusercontent.com",
       "RedirectUri": "cityleague://auth/callback"
     }
   }
   ```

If you must use a **Web application** client (has a client secret), also set `Auth:PublicBaseUrl` and register `https://<your-api>/api/auth/callback/google`. The API will 302/bridge back to the app — this only works when that https URL is reachable from the device.

Using several platform client ids? List them in `Auth:Google:AdditionalAudiences` so every id_token audience is accepted.

### Microsoft

1. In the [Entra portal](https://entra.microsoft.com/) → **App registrations**, register an app.
2. Under **Authentication**, add a **Mobile and desktop applications** platform with the redirect URI `cityleague://auth/callback`.
3. Set `Auth:Microsoft:ClientId`. No secret is needed — it is a public client using PKCE.

Multi-tenant sign-in is allowed by default (`Auth:Microsoft:Authority` defaults to the `common` endpoint). Emails are only trusted for account linking when Entra marks the domain as verified (`xms_edov`) or the account is a consumer Microsoft account; set `Auth:Microsoft:Authority` to your single tenant if you want to avoid that nuance entirely.

### Apple

1. In the [Apple Developer portal](https://developer.apple.com/account/resources/identifiers/list), enable **Sign in with Apple** on the app's App ID (`com.CityLeague.app`).
2. Create a **Services ID** (e.g. `com.cityleague.service`), enable Sign in with Apple on it, and register the return URL `https://<your-api>/api/auth/callback/apple`. Apple rejects http and custom schemes.
3. Create a **Sign in with Apple key** and download the `.p8` file.
4. Configure the API:
   ```json
   "Auth": {
     "PublicBaseUrl": "https://<your-api>",
     "Apple": {
       "ClientId": "com.cityleague.service",
       "TeamId": "<team-id>",
       "KeyId": "<key-id>",
       "PrivateKeyPath": "/secrets/AuthKey_<key-id>.p8",
       "BundleId": "com.CityLeague.app"
     }
   }
   ```

The API mints Apple's short-lived ES256 client secret from the key itself. `BundleId` matters because the native iOS sheet issues tokens for the bundle id rather than the services id — without it, iOS sign-ins are rejected while Android ones succeed.

### Azure AD B2C (alternative)

1. Create a **B2C tenant** and register an application (SPA/native) with the mobile redirect URIs.
2. Add **Google** and **Microsoft** as identity providers and create a **sign-up/sign-in user flow** (or custom policy).
3. Configure the API:
   ```json
   "Auth": {
     "B2C": {
       "Authority": "https://<tenant>.b2clogin.com/<tenant>.onmicrosoft.com/<policy>/v2.0",
       "ClientId": "<app-client-id>"
     }
   }
   ```
4. Acquire a B2C `id_token` in the client (MSAL ships with the app) and post it to `/api/auth/exchange` as `{ idToken }`.

### Account linking

External identities are stored in `UserExternalLogins`, so one person can sign in with several providers and keep one account. A second provider links to an existing account **only** when the provider asserts a verified email that matches; otherwise the sign-in is refused with `409` rather than silently merging accounts.

### Mobile platform setup

Already wired up, but worth knowing if you change the scheme:

- **Android**: `WebAuthenticatorCallbackActivity` declares the `cityleague://auth` intent filter, and the manifest queries for a Custom Tabs browser (required on Android 11+).
- **iOS**: `Info.plist` registers the `cityleague` URL scheme and `Platforms/iOS/Entitlements.plist` carries `com.apple.developer.applesignin`. The same capability must be enabled on the App ID or signing fails.

Change `Auth:MobileRedirectUri` and you must update both platforms to match.

## Azure deployment

| Resource | Use |
|----------|-----|
| Azure SQL | Primary database (set `ConnectionStrings:SqlServer`) |
| Azure Blob Storage | Avatars (`AvatarStorage:Provider = Azure`) |
| Azure SignalR Service | Scale-out for real-time position updates |
| Azure AD B2C | Optional, if you federate identity through B2C instead of the providers directly |
| App Service / Container Apps | Host the API |
| Application Insights | Logging + telemetry |

Steps:
1. Provision the resources above.
2. Set app settings: `ConnectionStrings:SqlServer`, `Jwt:SigningKey`, `Auth:Mode=Production`, `Auth:PublicBaseUrl`, the provider settings from [Authentication](#authentication), and `AvatarStorage:Provider=Azure` (+ connection string).
3. (Optional) Bind Azure SignalR by adding `builder.Services.AddSignalR().AddAzureSignalR(<connStr>)` in `Program.cs`.
4. Deploy the API (e.g. `az webapp up` or container image). On startup with a SQL Server connection string, **EF migrations are applied automatically**.

### EF Core migrations

Migrations target **SQL Server** (the production database). A design-time factory is included.

```bash
dotnet ef migrations add <Name> \
  --project src/CityLeague.Infrastructure \
  --startup-project src/CityLeague.Infrastructure \
  --output-dir Data/Migrations
```

Local SQLite dev does not use migrations; the schema is created from the model via `EnsureCreated`.

## API surface

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/auth/providers` | Sign-in options this deployment is configured for |
| POST | `/api/auth/register` / `/api/auth/login` | Username + password |
| POST | `/api/auth/exchange` | Provider code or id_token → app JWT |
| GET/POST | `/api/auth/callback/{provider}` | Forwards an https-only provider redirect to the app |
| POST | `/api/auth/refresh` | Refresh tokens |
| GET/PATCH | `/api/me` | Profile |
| POST | `/api/me/handle` | Set unique handle (once) |
| GET | `/api/me/handle/available` | Handle availability check |
| POST | `/api/me/avatar` | Upload avatar (multipart) |
| GET | `/api/users/search?q=` | Find users by handle prefix |
| GET/POST | `/api/contacts` | List / request contacts |
| POST | `/api/contacts/{userId}/accept` | Accept a request |
| GET | `/api/sports` | Sports + formats |
| GET/POST | `/api/series` | Event series |
| GET/POST | `/api/events` | List / create events |
| GET | `/api/events/{id}` | Event detail (positions, participants, result) |
| POST | `/api/events/{id}/invite` | Invite contacts |
| POST | `/api/events/{id}/positions/{slotId}/claim` | Claim a position |
| POST | `/api/events/{id}/positions/{slotId}/release` | Release a position |
| POST | `/api/events/{id}/result` | Submit result → update stats |
| GET | `/api/stats/me` | Per-sport stats |

Real-time: SignalR hub at `/hubs/events` broadcasts `PositionChanged`, `ParticipantJoined`, and `EventCompleted` to clients in an event group.

## Tests

```bash
dotnet test tests/CityLeague.Core.Tests/CityLeague.Core.Tests.csproj
dotnet test tests/CityLeague.Api.Tests/CityLeague.Api.Tests.csproj
```

- **Core**: formation templates (slot counts, uniqueness, mirroring), handle validation.
- **API** (in-memory host + isolated SQLite): result gating returns `409`, claiming an occupied slot returns `409` (single winner), and submitting a result updates player stats.
- **Social sign-in**: provider `id_token`s are validated through the real code path against a stubbed JWKS, covering code redemption, wrong audience/issuer/nonce/expiry, verified-email linking, account-takeover protection and Apple's generated client secret.

## Key technical decisions

- **MVVM** with `CommunityToolkit.Mvvm`; DI configured in `MauiProgram`.
- **SkiaSharp** for the football field (flexible pitch drawing + tap-to-claim).
- **SignalR** for instant multi-user position sync.
- **Result gating** via the `EventSeries` entity: the last event in a series must be `Completed`.
- **Race-safe claiming**: positions are claimed with a conditional `ExecuteUpdate` (`WHERE UserId IS NULL`), so only one player can win a slot.
- **Provider-portable data layer**: SQL Server in production, SQLite for local dev/tests (with a `DateTimeOffset` conversion applied for SQLite).
```
