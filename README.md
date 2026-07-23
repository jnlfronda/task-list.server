# Task List Application

A full-stack personal task tracker where each user can register, log in, and manage their own tasks (create, edit, delete). Built with:

- **Frontend:** Angular 21 (standalone components, zoneless, signals)
- **Backend:** ASP.NET Core 8 Web API with Entity Framework Core
- **Database:** SQL Server (Express / LocalDB)
- **Auth:** JWT bearer tokens, BCrypt-hashed passwords

The two projects live in sibling folders:

```
task-list.client/   # Angular SPA
task-list.server/   # ASP.NET Core Web API  <-- this repo
```

---

## 1. About this repository

This is the **backend API** for the Task List application. It exposes:

- Authentication endpoints (register / login) that issue JWTs.
- Task CRUD endpoints protected by JWT bearer authorization.
- All task queries are scoped server-side to the caller — users cannot see or modify anyone else's data.

The Angular client that consumes this API lives in the sibling folder `task-list.client/`.

---

## 2. Setup, build, and run

### Prerequisites

- **.NET 8 SDK**
- **SQL Server / SQL Express / LocalDB**
- **EF Core CLI:**
  ```powershell
  dotnet tool install --global dotnet-ef
  ```

### Steps

1. Restore packages:
   ```powershell
   dotnet restore
   ```

2. Update the connection string in [appsettings.json](appsettings.json) → `ConnectionStrings:DefaultConnection` for your SQL instance. The default targets a local SQL Express instance:
   ```
   Server=DESKTOP-D1M9ECD\SQLEXPRESS;Database=task-manager;Trusted_Connection=True;TrustServerCertificate=True;
   ```

3. Configure the JWT signing key via **user-secrets** (never commit secrets):
   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "<random-string-at-least-32-characters-long>"
   ```

4. Apply migrations (creates `Users` and `Tasks` tables):
   ```powershell
   dotnet ef database update
   ```

5. Run:
   ```powershell
   dotnet run --launch-profile task-list
   ```
   The API listens on **http://localhost:4201**.

6. Build:
   ```powershell
   dotnet build
   ```

To run the frontend against this API, follow the setup steps in [`../task-list.client/README.md`](../task-list.client/README.md). Both apps must be running simultaneously (client on `4200`, server on `4201`).

---

## 3. Authentication mechanism

**Chosen mechanism: traditional username + password with JWT bearer tokens.**

- Passwords are hashed with **BCrypt** before storage; the raw password is never persisted.
- On successful register/login, the server issues a signed **JWT (HMAC-SHA256)** containing:
  - `nameid` — the user's id, used server-side to scope task queries.
  - `unique_name` — the username, displayed by the client.
- JWT validation is configured in [Program.cs](Program.cs) via `AddJwtBearer(...)`; issuer, audience, lifetime, and signing key are all checked on every request.
- Task endpoints are protected by `[Authorize]` on `TaskController`, and every query filters by `UserId == CurrentUserId` (read from the token's `nameid` claim).
- The login endpoint only accepts local (traditional) accounts — SSO users cannot log in via password (see §4).
- Token lifetime is configurable via `Jwt:ExpireMinutes` (default: 120 minutes).

**Why this over SSO:**
- Self-contained — the API can be developed and tested with zero dependency on an external identity provider.
- Simple to demo, reason about, and secure end-to-end with a single secret.
- Adequate for a personal task tracker with no cross-organization identity needs.

**SSO is not implemented yet**, but the database schema and `User` entity already support it (see §4 and §6). Adding SSO becomes an OIDC handler in `Program.cs` plus an upsert into `Users`; no schema change is needed.

---

## 4. Database schema

Two tables, both managed by EF Core migrations under [Migrations/](Migrations/).

### `Users`

| Column         | Type              | Notes                                                   |
| -------------- | ----------------- | ------------------------------------------------------- |
| `id`           | int (PK, IDENT)   |                                                         |
| `Username`     | nvarchar(100)     | **Unique index**                                        |
| `PasswordHash` | nvarchar(max)     | **Nullable** — BCrypt hash for local users, NULL for SSO |
| `Provider`     | nvarchar(50)      | `"Local"` (default) / `"Microsoft"` / `"Google"` / …    |
| `ExternalId`   | nvarchar(255)     | Nullable — the provider's stable user id (SSO only)     |

Additional constraint: filtered unique index on (`Provider`, `ExternalId`) where `ExternalId IS NOT NULL`, so each SSO identity maps to exactly one local `Users` row.

### `Tasks`

| Column        | Type                          | Notes                                    |
| ------------- | ----------------------------- | ---------------------------------------- |
| `id`          | int (PK, IDENT)               |                                          |
| `Title`       | nvarchar(100)                 | Required                                 |
| `Description` | nvarchar(max)                 | Optional                                 |
| `due_date`    | datetime2                     | **Nullable**                             |
| `Priority`    | nvarchar(max)                 | `Low` / `Medium` / `High` (required)     |
| `Category`    | nvarchar(max)                 | Nullable                                 |
| `Status`      | nvarchar(max)                 | `Pending` / `In Progress` / `Completed`  |
| `user_id`     | int (FK → Users.id, cascade)  | Scopes every task to its owner           |

### Traditional vs. SSO users

Both user types live in the **same `Users` table** and are distinguished by the `Provider` column:

| Field         | Local user (traditional)   | SSO user (future)                       |
| ------------- | -------------------------- | --------------------------------------- |
| `Username`    | chosen by the user         | usually derived from the SSO profile    |
| `PasswordHash`| BCrypt hash                | `NULL`                                  |
| `Provider`    | `"Local"`                  | `"Microsoft"` / `"Google"` / …          |
| `ExternalId`  | `NULL`                     | provider's stable subject/id            |

Because `Tasks.user_id` is a single FK to `Users.id`, task ownership queries (`WHERE user_id = @current`) work identically for every user regardless of how they signed in.

---

## 5. Accessing the API

The database ships **empty** — there are no default credentials.

### Via the Angular client (recommended)

1. Start this server (`dotnet run --launch-profile task-list`).
2. Start the client (`npm start` in `../task-list.client`) and open **http://localhost:4200/**.
3. Click **Register**, choose a username (≥3 chars) and password (≥6 chars, confirmed twice).
4. You are automatically signed in and land on `/home`, where you can add tasks. Only **Title** is required; description, due date, and category are optional.

### Direct API calls (for testing)

All routes are under `/api`. Task routes require `Authorization: Bearer <token>`.

| Method | Route                | Auth | Description                          |
| ------ | -------------------- | ---- | ------------------------------------ |
| POST   | `/api/auth/register` | No   | Create user, returns JWT + username  |
| POST   | `/api/auth/login`    | No   | Verify credentials, returns JWT      |
| GET    | `/api/tasks`         | Yes  | List the caller's tasks              |
| GET    | `/api/tasks/{id}`    | Yes  | Get one task (must be owner)         |
| POST   | `/api/tasks`         | Yes  | Create a task (owner set from token) |
| PUT    | `/api/tasks/{id}`    | Yes  | Update a task (must be owner)        |
| DELETE | `/api/tasks/{id}`    | Yes  | Delete a task (must be owner)        |

Example (PowerShell):

```powershell
# Register
$body = @{ username = 'alice'; password = 'password123' } | ConvertTo-Json
$reg  = Invoke-RestMethod -Uri http://localhost:4201/api/auth/register `
                          -Method Post -ContentType 'application/json' -Body $body

# Call a protected endpoint
Invoke-RestMethod -Uri http://localhost:4201/api/tasks `
                  -Headers @{ Authorization = "Bearer $($reg.token)" }
```

You can also import [task-list.server.http](task-list.server.http) into your editor's REST client.

---

## 6. Configuring SSO providers

**SSO is not yet wired up in code**, so no provider client IDs, secrets, or callback URLs are required to run or test the API today. The `Users` table already has the columns needed to host SSO identities (see §4).

When SSO is added, credentials should be stored via `dotnet user-secrets` (development) or environment variables (production) — **never committed to source control**. The expected shape:

```json
"Authentication": {
  "Microsoft": {
    "ClientId": "<from Entra ID app registration>",
    "ClientSecret": "<from Entra ID app registration>"
  },
  "Google": {
    "ClientId": "<from Google Cloud Console>",
    "ClientSecret": "<from Google Cloud Console>"
  }
}
```

Typical local callback URL for OIDC: **`http://localhost:4201/signin-oidc`**. This URL must also be registered on the provider side:

- **Microsoft Entra ID:** *App registration → Authentication → Redirect URIs*
- **Google:** *OAuth 2.0 Client → Authorized redirect URIs*

Server-side wiring would extend the existing `AddAuthentication(...)` in [Program.cs](Program.cs) with `.AddOpenIdConnect(...)`. On the OIDC callback, the server would upsert the SSO user into the `Users` table (with `Provider`, `ExternalId`, `PasswordHash = NULL`) and issue the same JWT format used today, so the rest of the API is unchanged.
