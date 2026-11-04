# Container development

The base Compose configuration defines the app, Caddy, and PostgreSQL. The local
`docker-compose.override.yml` starts the app and Caddy, using the shared NAS
PostgreSQL database instead of a local database container. It removes the app's
local PostgreSQL dependency and places that service behind the optional
`local-postgres` profile. Visual Studio also explicitly marks PostgreSQL as
`DoNotStart` in the Docker Compose launch profile.

For staging releases, see [GitHub Actions staging deployment](github-actions-staging-deployment.md).
The [manual deployment guide](manual-staging-deployment.md) remains available as a fallback.
Initial provisioning is covered in [One-time staging environment setup](staging-environment-setup.md).
The staging deployment does not use the development override and retains its own PostgreSQL service.

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

### 3. Supply the NAS connection and apply existing migrations

The design-time `AppDbContextFactory` reads
`ConnectionStrings__DefaultConnection` from the environment. It does **not** load
the app's user secrets automatically. Without this variable it uses placeholder
credentials for generating migrations and bundles, which cannot update a real
database. Setting only `ASPNETCORE_ENVIRONMENT=Development` does not change that.

In the same PowerShell session at the repository root, run this block. Enter the
NAS development database password at the credential prompt. The connection-string
builder handles passwords containing connection-string punctuation without
printing the password or putting it in command history or process arguments.

```powershell
$localDbCredential = Get-Credential -UserName 'tattooflash_user' -Message 'NAS development database password'
$localDbConnection = New-Object System.Data.Common.DbConnectionStringBuilder
$localDbConnection['Host'] = Read-Host 'NAS development database hostname or IP'
$localDbConnection['Port'] = Read-Host 'NAS PostgreSQL published port'
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

Connect your database client to the same NAS development database.

Run:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

The initial migrations are
`20261021192717_InitialCreate` and `20261031190214_AddDesignTimeStamps`; include
any newer IDs present in the repository. Then check the affected feature in the
local app. A successful history query does not prove application behaviour.

### Migration troubleshooting

- **Connection refused:** check the NAS PostgreSQL container is running, its port
  is published, and the NAS firewall allows connections from your development machine.
- **Password authentication failed:** use the NAS database credentials. The local
  Compose `.env` does not set the NAS password. Do not print connection strings.
- **Placeholder credentials or `tattooflash_design_time` appears:** the EF
  process did not receive the environment variable. Run the connection setup
  and migration command in the same PowerShell session.
- **Database is up to date but the app still reports missing tables:** confirm
  EF and the app target the same NAS host, port, and database. EF uses the
  temporary environment variable; the app uses its mounted user secrets.
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
