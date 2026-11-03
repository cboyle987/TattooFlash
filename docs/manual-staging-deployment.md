# Manual staging build and deployment

This guide documents the working manual deployment process for TattooFlash.
GitHub Actions CI/CD is planned; it is not required for these steps.

| Item | Current value |
| --- | --- |
| Site | https://tattoo-flash-staging.connorboyle.dev |
| Server | IONOS VPS, Ubuntu 24.04, Linux x64 |
| Registry | Private GitHub Container Registry (GHCR) package |
| Image | `ghcr.io/cboyle987/tattooflash` |
| First deployed version | `0.1.0` |
| VPS account | `deploy` |
| Deployment directory | `/home/deploy/tattooflash` |
| Secret settings | `/home/deploy/.config/tattooflash/staging.env` |

The computer builds and publishes the app image and an EF migration bundle from
the same source revision. The VPS receives configuration files and the bundle,
pulls the image, applies migrations explicitly, and starts the app. It needs
neither the source repository nor the .NET SDK.

## Prerequisites

Complete the [one-time staging environment setup](staging-environment-setup.md) first.
This release guide assumes SSH access, Docker, registry authentication, staging
services, the protected env file, and the deployment directory are already configured.

## Each release: build on Windows

Run these commands in PowerShell from the repository root. Replace the example
version with the release being deployed; replace `YOUR_SERVER_IP` with the VPS IP.

### 1. Choose a version and source revision

Use SemVer, for example `0.1.1` for a fix following `0.1.0`. The image version
identifies the build, independently of the environment. Do not overwrite a
published version tag. Prerelease versions such as `0.2.0-rc.1` are also valid.

```powershell
$releaseVersion = '0.1.1'
$stagingServerIp = 'YOUR_SERVER_IP'
$releaseImage = "ghcr.io/cboyle987/tattooflash:$releaseVersion"
git status --short
git rev-parse HEAD
```

Review and commit the intended release changes, and record the source commit.
Build the image and migration bundle from that same revision without editing
application code between builds. Inspect any new migrations for unintended schema
or data changes before deployment.

Check that appsettings files contain no credentials: they are published into the
image. `.dockerignore` excludes `.env`, but that does not exclude arbitrary secret
files with other names.

### 2. Build and test

```powershell
dotnet build TattooFlash.slnx
dotnet test TattooFlash.slnx
```

Stop if either fails. Passing tests do not prove staging runtime behaviour; the
browser checks below are still required.

### 3. Build and push the image

```powershell
docker build --platform linux/amd64 --file TattooFlash.Web/Dockerfile --tag $releaseImage --label "org.opencontainers.image.source=https://github.com/cboyle987/TattooFlash" .
docker push $releaseImage
```

The final `.` supplies the repository root as the build context. The Dockerfile
builds a Release app and publishes into a runtime image. The source label links
the package to the repository. `linux/amd64` matches the VPS (`uname -m` returns
`x86_64`). Record the digest printed after a successful push.

### 4. Build the migration bundle

```powershell
dotnet ef --version
```

Match the EF tool version to the project's
`Microsoft.EntityFrameworkCore.Design` reference, currently `10.0.13`. If absent:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
```

To align an existing global installation, use
`dotnet tool update --global dotnet-ef --version 10.0.13`.

```powershell
dotnet ef migrations bundle --project TattooFlash.Web/TattooFlash.Web.csproj --configuration Release --self-contained --target-runtime linux-x64 --output "artifacts/efbundle-$releaseVersion"
```

This packages existing migrations; it does not create a new migration or apply
one to staging. The self-contained Linux executable runs in a temporary app-image
container. If design-time context creation fails, check local development
configuration; do not embed staging secrets into the bundle.

### 5. Select the release and copy deployment artifacts

Update the app image in the local `docker-compose.staging.yml`:

```yaml
services:
  tattooflash.web:
    image: ghcr.io/cboyle987/tattooflash:0.1.1
```

Keep its existing environment settings and restart policy. Use the same version
as the image and bundle above. Commit the deployment version change so the
selected version is recorded.

In Windows PowerShell, transfer only these artifacts:

```powershell
scp -i "$env:USERPROFILE\.ssh\tattooflash_staging" docker-compose.yml docker-compose.staging.yml Caddyfile.staging "artifacts/efbundle-$releaseVersion" "deploy@${stagingServerIp}:~/tattooflash/"
```

Do not copy the development override or local secret files.

## Each release: deploy on the VPS

### 6. Validate and pull before interrupting the running app

Connect through Windows Terminal using the usual SSH key. In the VPS Bash shell:

```bash
cd "$HOME/tattooflash"
release_version='0.1.1'

dc() {
  sudo docker compose \
    --env-file "$HOME/.config/tattooflash/staging.env" \
    -f docker-compose.yml \
    -f docker-compose.staging.yml \
    "$@"
}

dc config --quiet
dc pull tattooflash.web
```

The `dc` function is a shell shortcut valid for this session. Recreate it after
reconnecting. Its explicit files exclude `docker-compose.override.yml`, whose
development settings mount Windows user secrets. Keep the directory and Compose
project name stable so the existing volumes are reused.

Stop on any error. `config --quiet` validates without printing resolved secrets.
Confirm the pull digest matches the published release and that the staging YAML
selects the intended version. For the first deployment, also complete the
[initial infrastructure image downloads](staging-environment-setup.md#deployment-directory-and-first-image-downloads).

### 7. Prepare PostgreSQL and take a database backup

```bash
dc up -d postgres
dc ps
```

Wait until PostgreSQL is healthy. For a subsequent release, stop the old app
before migrating so it cannot write against a changing schema:

```bash
dc stop tattooflash.web
```

This introduces a maintenance window; the deployment is not zero downtime.
Caddy may return a gateway error until the app starts again.

Before changing an existing schema, take a database backup:

```bash
install -d -m 700 "$HOME/tattooflash-backups"
backup_file="$HOME/tattooflash-backups/pre-$release_version-$(date -u +%Y%m%dT%H%M%SZ).dump"
umask 077
dc exec -T postgres pg_dump -U tattooflash_user -d tattooflash -Fc > "$backup_file"
```

Only proceed if `pg_dump` exits successfully. Check that the archive can be read:

```bash
dc exec -T postgres pg_restore --list < "$backup_file" > /dev/null
```

Archive inspection is not a restore test. Keep a protected off-server copy and
establish a tested restore procedure before relying on backups for recovery.
This dump covers PostgreSQL, not R2 objects, Caddy data, or data protection keys.
Do not delete or recreate the database volume for a routine deployment.

### 8. Apply migrations explicitly

```bash
chmod 755 "efbundle-$release_version"
dc run --rm --no-deps \
  --volume "$PWD/efbundle-$release_version:/migration/efbundle:ro" \
  --entrypoint /migration/efbundle \
  tattooflash.web
```

The temporary container inherits the staging app's environment and database
network. The image supplies appsettings files in its `/app` working directory.
The entrypoint runs the bundle instead of starting the web app. The container is
removed when it exits.

Expect `Done.` and a successful exit. Applied migrations are tracked, so rerunning
the bundle applies only outstanding migrations. Stop on failure and inspect it
before launching the new app. Do not pass migration `0`, which requests a full
schema rollback.

Verify the recorded migrations:

```bash
dc exec postgres psql -U tattooflash_user -d tattooflash \
  -c 'TABLE "__EFMigrationsHistory";'
```

The initial `0.1.0` deployment recorded:

- `20261021192717_InitialCreate`
- `20261031190214_AddDesignTimeStamps`

Subsequent releases should include any additional expected migration IDs.

### 9. Start and verify

```bash
dc up -d --no-build
dc ps
dc logs --tail 100 tattooflash.web caddy
```

`--no-build` is essential because the shared base Compose file still includes a
`build` section. Staging runs the image pulled from GHCR.

Open https://tattoo-flash-staging.connorboyle.dev and check:

- HTTPS has a trusted certificate and HTTP redirects to HTTPS.
- The page and catalogue load, including a narrow/mobile viewport.
- Login returns to staging and logout succeeds.
- Uploading a disposable design displays its image from the staging R2 bucket.
- Editing and deleting the test design work as expected.
- Logs show no unexpected application or proxy failures.

To verify persistence, restart the app with `dc restart tattooflash.web`, then
confirm a retained test design and its image still load. A restart disconnects
active Interactive Server sessions, so reload the browser if necessary. Test a
full VPS reboot separately when appropriate to verify restart policies.

First deployment verification confirmed page access, login/logout, and catalogue
loading. Upload, persistence, backup restore, and reboot checks must be verified
separately; do not assume them from a successful container start.

## Troubleshooting and recovery

### GHCR push ends with `denied`

Uploaded layers alone do not mean publication succeeded. Check the classic
token's account, expiry, and `write:packages` permission, then refresh credentials
in the terminal/environment doing the push:

```powershell
docker logout ghcr.io
docker login ghcr.io --username cboyle987
docker push $releaseImage
```

The first deployment succeeded after reauthentication; the precise original
cause was not established. A successful push prints a digest. Existing layers
are reused; no rebuild is needed for an authentication retry.

### Compose cannot find the env file

The directory is `tattooflash`, not `tatttooflash`. Check the path and permissions
without printing secret contents:

```bash
ls -l "$HOME/.config/tattooflash/staging.env"
```

### Connection string fails to parse

Use mapping syntax in the staging `environment` block:

```yaml
environment:
  ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=tattooflash;Username=tattooflash_user;Password=${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD}"
```

Avoid list syntax like `- KEY="value"`: those embedded quotes become part of the
environment value. The original deployment's connection string failed this way;
Auth0 and R2 values would also have been affected. Copy the corrected Compose
file to the VPS, validate, and retry; no image rebuild is necessary.

### Migration output contains errors but finishes with `Done.`

On a fresh database, the initial query of `__EFMigrationsHistory` can log a failed
command before the table is created. Verify the final outcome and history table.

Npgsql 10 can also log `Cannot load library libgssapi_krb5.so.2` when probing for
Kerberos/GSSAPI, then successfully fall back. This is separate from the history
table query. Since this deployment does not use Kerberos, appending
`;GSS Encryption Mode=Disable` to the connection string suppresses that probe.
Keep such configuration changes in the local staging YAML and copy it to the
server. See [Npgsql security documentation](https://www.npgsql.org/doc/security.html).

### HTTPS or app startup fails

Check `dc ps` and `dc logs --tail 100 tattooflash.web caddy`. Verify DNS points to
the VPS, both firewalls allow TCP 80/443, and the file is named exactly
`Caddyfile.staging` (without a space). Caddy may take a short time to obtain the
first certificate. Check Auth0 callback/logout allowlists and required R2 settings
if the app starts but those features fail. Remove credentials before sharing logs.

### Returning to a previous app version

Retain the previous image version, configuration, and database backup. If the new
schema remains compatible with the previous app, select its image tag in the
staging YAML, transfer the file, then run `dc config --quiet`,
`dc pull tattooflash.web`, and `dc up -d --no-build`.

Changing the app image does not undo database migrations. If compatibility is
uncertain, leave the app stopped and plan a forward fix or a reviewed restore.
Do not automatically run Down migrations or restore over newer data. Never use
`docker compose down -v` for a release: it deletes the stack's named volumes.

## Future CI/CD

A future GitHub Actions workflow should reproduce the same boundaries: validate
the source, build and publish a uniquely versioned image and matching migration
artifact, deploy configuration, back up and migrate the known staging database,
then start and verify the app. GHCR publishing can use the workflow's
`GITHUB_TOKEN`; the VPS still needs permission to pull private images. Runtime
secrets remain outside the image. No automatic pipeline is implemented by this
guide.

References: [GHCR authentication and image publishing](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry),
[EF migration bundles](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#bundles),
and [Caddy automatic HTTPS](https://caddyserver.com/docs/automatic-https).
