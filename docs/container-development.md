# Container development

The current Compose configuration runs the app, Caddy, and PostgreSQL. Visual Studio's
`docker-compose.override.yml` supplies development settings and local secret mounts.
For staging releases, see [GitHub Actions staging deployment](github-actions-staging-deployment.md).
The [manual deployment guide](manual-staging-deployment.md) remains available as a fallback.
Initial provisioning is covered in [One-time staging environment setup](staging-environment-setup.md).

## Apply EF migrations to local container PostgreSQL

Run EF tooling on Windows against the local container's published database port.
The development override maps `127.0.0.1:5433` on Windows to port `5432` inside
PostgreSQL. From Windows, use **`Host=127.0.0.1;Port=5433`**, not the Docker
service name `postgres`. App containers use `postgres:5432` instead.

These steps change the local development database only. Do not use staging
credentials or the staging Compose override. Back up any local data you need
before applying schema changes.

### 1. Start or identify the local database

Start Docker Desktop with Linux containers. From the repository root in
PowerShell, use the base and development Compose files explicitly:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d postgres
docker compose -f docker-compose.yml -f docker-compose.override.yml ps postgres
docker compose -f docker-compose.yml -f docker-compose.override.yml port postgres 5432
```

Wait for `healthy` and confirm the published endpoint is `127.0.0.1:5433`. Compose
requires the local `POSTGRES_PASSWORD`, normally supplied by the ignored root
`.env` file or your shell environment. Keep its value private.

If Visual Studio already started the stack, target that same Compose project
rather than creating another database volume. Check `docker compose ls` and set
`$env:COMPOSE_PROJECT_NAME = 'the-existing-project-name'` before the commands above
if necessary. If another instance already occupies port 5433, identify it before
starting another stack; do not delete volumes to resolve the conflict.

The database is `tattooflash`, with user `tattooflash_user`. Use the password that
initialised this local database volume. Editing `POSTGRES_PASSWORD` in `.env`
does not update an existing PostgreSQL user's password.

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

### 3. Supply the local connection and apply existing migrations

The design-time `AppDbContextFactory` reads
`ConnectionStrings__DefaultConnection` from the environment. It does **not** load
the app's user secrets automatically. Without this variable it uses placeholder
credentials for generating migrations and bundles, which cannot update a real
database. Setting only `ASPNETCORE_ENVIRONMENT=Development` does not change that.

In the same PowerShell session at the repository root, run this block. Enter the
existing local database password at the credential prompt. The connection-string
builder handles passwords containing connection-string punctuation without
printing the password or putting it in command history or process arguments.

```powershell
$localDbCredential = Get-Credential -UserName 'tattooflash_user' -Message 'Local PostgreSQL container password'
$localDbConnection = New-Object System.Data.Common.DbConnectionStringBuilder
$localDbConnection['Host'] = '127.0.0.1'
$localDbConnection['Port'] = 5433
$localDbConnection['Database'] = 'tattooflash'
$localDbConnection['Username'] = $localDbCredential.UserName
$localDbConnection['Password'] = $localDbCredential.GetNetworkCredential().Password
$localDbConnection['GSS Encryption Mode'] = 'Disable'

$previousDbConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')
try {
    $env:ConnectionStrings__DefaultConnection = $localDbConnection.ConnectionString
    dotnet ef database update --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj
    if ($LASTEXITCODE -ne 0) {
        throw 'Applying local EF migrations failed; inspect the error above.'
    }
}
finally {
    if ($null -eq $previousDbConnection) {
        Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    }
    else {
        $env:ConnectionStrings__DefaultConnection = $previousDbConnection
    }
    Remove-Variable localDbCredential, localDbConnection, previousDbConnection
}
```

The explicit project and startup-project arguments select the web project rather
than relying on solution or Visual Studio startup-project selection. The factory
is discovered automatically; no `Program.cs` changes are needed.

Expect existing migration IDs to be applied followed by `Done.`, or a message
that the database is already up to date. This applies migrations already in the
repository; it does not generate a migration from uncommitted model changes.
After changing the model, create and review an appropriate migration separately
before updating the database. Do not edit previously applied migrations.

### 4. Verify the result

Inspect the history table inside the same local database container:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml exec postgres psql -U tattooflash_user -d tattooflash
```

At the `psql` prompt, run:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

Use `\q` to exit. The initial migrations are
`20261021192717_InitialCreate` and `20261031190214_AddDesignTimeStamps`; include
any newer IDs present in the repository. Then check the affected feature in the
local app. A successful history query does not prove application behaviour.

### Migration troubleshooting

- **Connection refused:** check the container is healthy and port 5433 is
  published by the development override. Windows cannot resolve Docker's
  `postgres` service hostname.
- **Password authentication failed:** use the password for the existing volume,
  rather than assuming a newly edited `.env` changed it. Do not print resolved
  connection strings while investigating.
- **Placeholder credentials or `tattooflash_design_time` appears:** the EF
  process did not receive the environment variable. Run the connection setup
  and migration command in the same PowerShell session.
- **Database is up to date but the app still reports missing tables:** confirm
  EF and the app target the same database and Compose project. EF on Windows
  uses `127.0.0.1:5433`; the containerised app uses `postgres:5432`.
- **Fresh-database history query or missing Kerberos library message:** an
  initial history-table query may fail before EF creates it. Judge the final
  exit status and history rows. `GSS Encryption Mode=Disable` in the example
  suppresses Npgsql's optional Kerberos probe for this password-based setup.

See [EF CLI commands](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
and [design-time context creation](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation).

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
