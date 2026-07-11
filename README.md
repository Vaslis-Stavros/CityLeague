# CityLeague

Organize sports games and fill the pitch. **CityLeague** is a .NET MAUI (Android/iOS) app with an ASP.NET Core backend for planning football matches: an organizer creates an event, contacts join, and everyone claims positions on a live, interactive football field. Player stats are tracked across matches.

This repository contains the **Phase 1 MVP** focused on **football** (5v5 through 11v11). Padel, tennis and basketball appear as **"Coming soon"** tabs.

## Features (MVP)

- Sign in (Google / Microsoft via Azure AD B2C, or a dev sign-in for local testing)
- Pick a globally **unique handle** (`@alex_k`)
- Add **contacts** by handle search (request / accept)
- Create a **football event** (5v5–11v11) with an optional recurring **series**
- Invite contacts; every participant can invite their own contacts
- Claim positions on a **SkiaSharp football field** with **real-time** updates (SignalR)
- **Result gating**: you can't start the next match in a series until the previous result is submitted
- Submit a score → **player stats** (played / won / lost / drawn) update
- Profile with avatar (image upload or initials fallback) and per-sport stats

Deferred to Phase 2 (schema stubs already exist): leagues, team logos, team standings, multi-sport field UIs, push notifications, Apple Sign-In.

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

Auth defaults to **Dev mode**, so `POST /api/auth/exchange` accepts a simple payload (provider + email + display name) and returns an app JWT — no B2C tenant required for local development.

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

### Google Maps (location picker)

The map on **Create → Pick location** uses the Google Maps SDK on Android. Without a valid API key you will see blank beige tiles and a yellow warning banner.

1. In [Google Cloud Console](https://console.cloud.google.com/), enable **Maps SDK for Android**.
2. Create an API key restricted to your app package `com.CityLeague.app` (and optionally your debug SHA-1).
3. Copy `src/CityLeague.App/google-maps.key.example` to `google-maps.key` in the same folder and paste your key (one line, no quotes).
4. Rebuild the app. Alternatively pass `-p:GoogleMapsApiKey=YOUR_KEY` or set a `GoogleMapsApiKey` environment variable.

The key is injected at build time via `AndroidManifestPlaceholders` and is gitignored.

## Configuration reference (`appsettings.json`)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:SqlServer` | If set, the API uses Azure SQL / SQL Server and applies EF migrations. |
| `ConnectionStrings:Sqlite` | Used when `SqlServer` is empty (local dev); schema created via `EnsureCreated`. |
| `Jwt:SigningKey` | Symmetric key for signing app JWTs. **Override in production** (min 32 bytes). |
| `Jwt:Issuer` / `Jwt:Audience` | JWT issuer/audience. |
| `Auth:Mode` | `Dev` (trusts the exchange payload) or `B2C` (validates a real id_token). |
| `Auth:B2C:Authority` | B2C metadata authority (e.g. `https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/v2.0`). |
| `Auth:B2C:ClientId` | B2C application (client) id, validated as the token audience. |
| `AvatarStorage:Provider` | `Local` (disk, served from `/uploads`) or `Azure` (Blob Storage). |
| `AvatarStorage:ConnectionString` | Azure Storage connection string (when Provider = `Azure`). |
| `AvatarStorage:PublicBaseUrl` | Absolute base URL used to build avatar URLs for the `Local` provider. |

## Authentication: Dev vs Azure AD B2C

### Dev mode (default, local only)
`POST /api/auth/exchange` with `{ provider, email, displayName }` provisions/restores a user and returns app tokens. This is **not secure** and must never be enabled in production.

### Azure AD B2C (production)
1. Create a **B2C tenant** and register an application (SPA/native) with the mobile redirect URIs.
2. Add **Google** and **Microsoft** as identity providers and create a **sign-up/sign-in user flow** (or custom policy).
3. Configure the API:
   ```json
   "Auth": {
     "Mode": "B2C",
     "B2C": {
       "Authority": "https://<tenant>.b2clogin.com/<tenant>.onmicrosoft.com/<policy>/v2.0",
       "ClientId": "<app-client-id>"
     }
   }
   ```
4. The mobile app acquires a B2C `id_token` (via MSAL) and calls `POST /api/auth/exchange` with `{ idToken }`. The API validates it against the tenant's published keys, upserts the user, and returns app JWTs.

> The MAUI client ships with a pluggable auth flow. Wire MSAL (`Microsoft.Identity.Client`) into `AuthService` for B2C and configure platform redirect URIs (Android intent filter, iOS URL scheme + entitlements) before shipping.

## Azure deployment

| Resource | Use |
|----------|-----|
| Azure SQL | Primary database (set `ConnectionStrings:SqlServer`) |
| Azure Blob Storage | Avatars (`AvatarStorage:Provider = Azure`) |
| Azure SignalR Service | Scale-out for real-time position updates |
| Azure AD B2C | Identity (Google / Microsoft) |
| App Service / Container Apps | Host the API |
| Application Insights | Logging + telemetry |

Steps:
1. Provision the resources above.
2. Set app settings: `ConnectionStrings:SqlServer`, `Jwt:SigningKey`, `Auth:Mode=B2C` (+ B2C settings), `AvatarStorage:Provider=Azure` (+ connection string).
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
| POST | `/api/auth/exchange` | Identity token → app JWT |
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

## Key technical decisions

- **MVVM** with `CommunityToolkit.Mvvm`; DI configured in `MauiProgram`.
- **SkiaSharp** for the football field (flexible pitch drawing + tap-to-claim).
- **SignalR** for instant multi-user position sync.
- **Result gating** via the `EventSeries` entity: the last event in a series must be `Completed`.
- **Race-safe claiming**: positions are claimed with a conditional `ExecuteUpdate` (`WHERE UserId IS NULL`), so only one player can win a slot.
- **Provider-portable data layer**: SQL Server in production, SQLite for local dev/tests (with a `DateTimeOffset` conversion applied for SQLite).
```
