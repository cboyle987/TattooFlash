# Manual staging build and deployment

[GitHub Actions](github-actions-staging-deployment.md) is the normal release path.
This guide is the fallback for building locally or deploying an existing CI image
and bundle manually. It uses the same installed server script as the pipeline.

Complete [Staging environment setup](staging-environment-setup.md) and the
[automation server setup](github-actions-staging-deployment.md#install-and-update-privileged-server-files)
first. PostgreSQL must already be running and initialised.

## Select the source and version

Use a committed revision and an unused stable `MAJOR.MINOR.PATCH` version. The
server script currently rejects prerelease suffixes and leading zeroes. Never
move a published Git release tag or overwrite an existing image version.

Run local commands in Windows PowerShell from the repository root:

```powershell
$releaseVersion = '0.2.0'
$stagingServerIp = 'YOUR_SERVER_IP'
$releaseImage = "ghcr.io/cboyle987/tattooflash:$releaseVersion"
git status --short
git rev-parse HEAD
```

Confirm that the source is the revision intended for deployment. Build the image
and migration bundle from the same revision. Review new migrations before
applying them to staging. Credentials must not be in appsettings files, source
control, or the build context.

## Option A: use artifacts already built by CI

Locate the successful version-tag run in GitHub Actions. Download and extract
`migrations-VERSION`; its executable is `efbundle-VERSION`. Place the extracted
file under local `artifacts/`. The corresponding image must already be published
at `ghcr.io/cboyle987/tattooflash:VERSION`.

Skip the local build section below. Do not rebuild or republish an existing
release solely to perform a manual deployment. Artifacts expire after seven days
unless retained elsewhere.

## Option B: build both artifacts locally

Docker Desktop must be running with Linux containers. Authenticate using a
classic GitHub PAT with `write:packages` at the password prompt:

```powershell
docker login ghcr.io --username cboyle987
dotnet build TattooFlash.slnx
dotnet test TattooFlash.slnx
```

Stop on build or test failure. Build and push the image:

```powershell
docker build --platform linux/amd64 --file TattooFlash.Web/Dockerfile --tag $releaseImage --label "org.opencontainers.image.source=https://github.com/cboyle987/TattooFlash" .
docker push $releaseImage
```

Record the successful push digest and source commit. `linux/amd64` matches the
current VPS (`uname -m` returns `x86_64`). New GHCR packages default to private;
confirm visibility and repository association in package settings.

Check `dotnet ef --version`. Match the project's EF Design reference, currently
`10.0.13`. If absent, install it:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
```

For an existing global installation, use
`dotnet tool update --global dotnet-ef --version 10.0.13`.

```powershell
dotnet ef migrations bundle --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj --configuration Release --self-contained --target-runtime linux-x64 --output "artifacts/efbundle-$releaseVersion"
```

The design-time factory constructs the PostgreSQL context without application
startup or development user secrets. Generating a bundle does not connect to the
staging database. During execution, the real connection comes from Compose's
`ConnectionStrings__DefaultConnection` environment variable. For direct local
`dotnet ef database update`, provide a real connection explicitly; the factory's
fallback credentials are only placeholders.

## Transfer and deploy

Use your personal administrative SSH key to upload the bundle:

```powershell
scp -i "$env:USERPROFILE\.ssh\tattooflash_staging" "artifacts/efbundle-$releaseVersion" "deploy@${stagingServerIp}:~/tattooflash/efbundle-$releaseVersion.upload"
ssh -i "$env:USERPROFILE\.ssh\tattooflash_staging" "deploy@$stagingServerIp"
```

In the VPS Bash shell, set the same version, finalise the upload, and deploy:

```bash
release_version='0.2.0'
mv "/home/deploy/tattooflash/efbundle-$release_version.upload" "/home/deploy/tattooflash/efbundle-$release_version"
sudo /usr/local/sbin/tattooflash-deploy "$release_version"
```

This is a real deployment with a brief maintenance window. The installed script
validates the version, locks deployment, copies the bundle to a protected path,
validates configuration, pulls the image, checks PostgreSQL, stops the app, takes
and inspects a database dump, runs migrations, starts the app, and checks the
public HTTPS homepage. A failed backup or migration leaves the app stopped for
investigation. A failed HTTP check leaves containers running. No automatic
rollback is performed.

Do not edit the image version in Compose for each release. The protected staging
file uses `${RELEASE_VERSION:?Set RELEASE_VERSION}`, supplied by the script.
Configuration, secrets, and script updates are administrative operations; see
[installing protected files](github-actions-staging-deployment.md#install-and-update-privileged-server-files).

## Verify and diagnose

Expect the script to finish with `Deployment of VERSION completed; staging
returned HTTP 200`. Open https://tattoo-flash-staging.connorboyle.dev and check the
catalogue, login/logout, and any changed features. Exercise disposable image
uploads, editing/deletion, and mobile layout when relevant. HTTP 200 alone does
not verify those behaviours or prove the running image's version.

For read-only diagnostics from your administrative SSH session:

```bash
release_version='0.2.0'
dc() {
  sudo env RELEASE_VERSION="$release_version" docker compose \
    --project-name tattooflash \
    --project-directory /etc/tattooflash/staging \
    --env-file /etc/tattooflash/staging/staging.env \
    -f /etc/tattooflash/staging/docker-compose.yml \
    -f /etc/tattooflash/staging/docker-compose.staging.yml \
    "$@"
}
dc ps
dc logs --tail 100 tattooflash.web caddy
dc exec postgres psql -U tattooflash_user -d tattooflash \
  -c 'TABLE "__EFMigrationsHistory";'
```

Confirm the app container uses the expected version. The original migrations were
`20261021192717_InitialCreate` and `20261031190214_AddDesignTimeStamps`; newer
releases may include more. Existing migrations are not reapplied.

Npgsql may emit a missing `libgssapi_krb5.so.2` message and fall back successfully.
For this non-Kerberos deployment, appending `;GSS Encryption Mode=Disable` to the
protected connection-string template suppresses that probe. On a fresh database,
a failed history-table query can precede successful initial migration creation.
Judge completion using the exit status and recorded migrations, not an isolated
log line.

Backups are now under `/var/backups/tattooflash`, not the original
`/home/deploy/tattooflash-backups` location. Old backups have not been moved by this
guide. Archive inspection is not a restore test; maintain protected off-server
copies and verify restoration separately.

For host-key errors, GHCR permissions, failed migrations, reruns, and rollback
considerations, see [CI/CD recovery](github-actions-staging-deployment.md#verification-and-recovery).
Never delete named volumes or run Down migrations as an automatic recovery step.
# Transition notice

This guide describes the previous staging-only setup. The replacement deployment
script is `scripts/Deploy.sh` and requires `ENVIRONMENT VERSION`. See
[Shared VPS layout](shared-vps-compose.md) before using these historical commands.

