# Container development

The base Compose configuration defines the app, Caddy, and PostgreSQL. The local
`docker-compose.override.yml` starts the app and Caddy, using the shared NAS
PostgreSQL database instead of a local database container. It removes the app's
local PostgreSQL dependency and places that service behind the optional
`local-postgres` profile. Visual Studio also explicitly marks PostgreSQL as
`DoNotStart` in the Docker Compose launch profile.

For releases and the manual fallback, see [Deployment](deployment.md).
Infrastructure provisioning is covered in [Shared VPS setup](shared-vps-compose.md).
Server deployments do not use the development override and have separate
PostgreSQL services for staging and production.

## Configure local development with the NAS database

On both the laptop and desktop, use Visual Studio's **Manage User Secrets** for
`TattooFlash.Web` to set `ConnectionStrings:DefaultConnection` to the NAS database.
Use its LAN hostname/IP, published PostgreSQL port, database name, and credentials.
Do not use `Host=postgres` or `localhost`: the app container must reach the NAS.
Keep credentials out of tracked files. User secrets are machine-specific and are
not synced through Git; the development override mounts them into the app container.
Ensure the NAS endpoint is reachable from Docker Desktop on each machine.

Start the Docker Compose profile in Visual Studio as before, or run:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d
```

Use a current Docker Compose v2 release supporting the `!reset` override tag.
Do not enable `local-postgres` or explicitly start `postgres` for normal NAS development.
The base file still requires `POSTGRES_PASSWORD` during interpolation, even when
the local service is inactive. Retain its existing value in your ignored `.env`;
this value does not configure the NAS connection or change its password.

If an old local PostgreSQL container is still running, stop it once in the same
Compose project (set `COMPOSE_PROJECT_NAME` for the VS project if necessary):

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml stop postgres
```

This leaves the existing local database volume intact. The app will use the NAS
once its user-secret connection string is updated and the app restarted.

## Data protection keys in Docker and Windows

The Dockerfile sets `DataProtection__KeysPath=/var/lib/tattooflash/keys` in its
base stage, covering Visual Studio fast mode and published images. Compose mounts
the existing `dataprotection_keys` volume there, preserving keys across container
replacements. The directory permissions and application name (`TattooFlash`)
remain unchanged.

When running `TattooFlash.Web` directly on Windows with the `https` or `http`
launch profile, leave `DataProtection:KeysPath` unset. ASP.NET Core uses its
default user-profile key storage, normally
`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`, and encrypts keys with Windows DPAPI.
The NAS connection string and other development user secrets are still required.
For HTTPS, trust the ASP.NET development certificate with
`dotnet dev-certs https --trust`; the Caddy certificate is only used through Docker.

You can override the storage directory using `DataProtection:KeysPath` in
configuration or `DataProtection__KeysPath` as an environment variable. An explicit
directory disables ASP.NET Core's automatic encryption at rest; Windows development
should normally keep the default. Windows and Docker use separate key rings, so
switching between them can require signing in again.

To verify persistence, sign in, restart the app in the same mode, and confirm that
the existing authentication cookie still works. For Docker, also recreate the app
container while retaining its named volume and repeat the check.

## Apply EF migrations to the shared NAS development database

Both development machines now share schema and data. Coordinate schema changes
and back up data before applying migrations. Verify the endpoint is the NAS
development database before running updates; do not use staging credentials.

### 1. Identify the NAS database

Use the NAS hostname/IP and published port from your development connection string.
The same endpoint is used by EF on Windows and the app inside Docker, provided
that hostname resolves from both. No local PostgreSQL container or published
Windows database port is needed.

### 2. Check the SDK and EF tool

Install the .NET 10 SDK on Windows and check:

```powershell
dotnet ef --version
```

Match the web project's `Microsoft.EntityFrameworkCore.Design` version,
currently `10.0.13`. If the global tool is absent:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
```

To align an existing global installation, use
`dotnet tool update --global dotnet-ef --version 10.0.13`.

### 3. Confirm configuration and apply existing migrations

The design-time `AppDbContextFactory` reads the web project's user secrets and
then environment variables. An existing `ConnectionStrings__DefaultConnection`
environment variable overrides the user-secret connection string. Without either,
it uses placeholder credentials for generating migrations and bundles; these
cannot update a real database.

Confirm your user secrets target the NAS development database and that no stale
environment override points elsewhere. From the repository root:

```powershell
dotnet ef database update --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj
```

Expect the existing migrations to be applied, or a message that the database is
already up to date. This applies migrations in the repository; it does not generate
one from model changes. Create and inspect migrations separately, and do not edit
previously applied migrations.

### 4. Verify the result

Connect your database client to the same development database and inspect:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

Compare the IDs with the migrations in `TattooFlash.Web/Migrations/`, then check
the affected feature in the app. Migration history alone does not prove runtime
behaviour.

### Migration troubleshooting

- **Connection refused:** check the NAS database is running, its port is published,
  and its firewall allows the development machine.
- **Password authentication failed:** use the NAS credentials. The local Compose
  `.env` does not set that database's password.
- **Placeholder database appears:** configure the web project's user secrets or
  the environment variable for the EF process.
- **App and EF disagree about the schema:** check both connection sources and
  remove stale environment overrides. The app container uses mounted user secrets.
- **Missing Kerberos library message:** for password-based development connections,
  `GSS Encryption Mode=Disable` suppresses Npgsql's optional Kerberos probe. Judge
  migration success from the exit status and migration history.

## Trust Caddy's local HTTPS certificate on Windows

The development `Caddyfile` serves `https://localhost` and forwards requests to
`tattooflash.web:8080`. Caddy generates its own local certificate authority (CA),
which is separate from the ASP.NET development certificate. Your Windows browser
needs to trust this CA to accept the site's certificate.

On each Windows development machine:

1. Install/start Docker Desktop with Linux containers and ensure `docker compose`
   is available in PowerShell.
2. Start the repository's Docker Compose project in Visual Studio, including
   Caddy. Ensure Caddy has started successfully and visit `https://localhost`.
3. From the repository root, run in PowerShell as your normal Windows user:

   ```powershell
   .\scripts\Trust-CaddyLocalCertificate.ps1
   ```

4. Accept the Windows certificate trust prompt if shown. Fully close and reopen
   your browser, then visit `https://localhost` and check that it no longer shows
   a certificate warning.

The helper copies only the public root certificate from the Caddy container to a
temporary file, imports it into `Cert:\CurrentUser\Root`, and removes the temporary
file. It does not copy the CA's private key or require a machine-wide certificate
installation. Re-running it detects an already trusted certificate.

Trusting this CA allows certificates it issues to be accepted by your Windows
user. Run this helper against your own development Caddy instance. Browsers using
a separate certificate store may need the root certificate imported separately.

The `caddy_data` volume preserves the CA across container replacements. If that
volume is deleted, Caddy creates a new CA and you must run the helper again.
Another machine with its own Caddy volume will also have its own CA.

### Troubleshooting

- **Docker not found:** use a terminal with Docker on `PATH`; reopen the terminal
  after installing Docker Desktop.
- **Certificate copy fails:** check that the Caddy service is running and inspect
  its logs. The helper uses the repository's base Compose file to find the service.
  If you launched with a custom Compose project name, set
  `$env:COMPOSE_PROJECT_NAME = 'your-project-name'` in the same terminal first.
- **Script execution is blocked:** follow your machine's PowerShell execution
  policy. On a personal development machine, if permitted, you can enable local
  scripts for your user with `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.
- **Warning persists:** verify you opened `https://localhost` through Caddy,
  restarted the browser, and ran the helper as the same Windows user as the browser.

This helper is for local Windows development. The VPS deployment uses a public
domain and Caddy-managed publicly trusted certificates; visitors won't need this step.

References: [Caddy in Docker](https://caddyserver.com/docs/running#docker-compose)
and [Windows Import-Certificate](https://learn.microsoft.com/en-us/powershell/module/pki/import-certificate).
