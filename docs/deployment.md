# Deployment and recovery

The normal release path builds a versioned image, deploys staging, then manually
promotes the tested release to production. See [Shared VPS setup](shared-vps-compose.md)
for infrastructure, protected configuration, and the installed deployment script.
Local development is covered in [Container development](container-development.md).

## Release to staging

Use a committed revision with reviewed migrations and an unused stable
`MAJOR.MINOR.PATCH` version. Replace the example `0.2.0` with an unused version;
never move published tags or overwrite published images.

From the repository root in PowerShell:

```powershell
git status --short
git rev-parse HEAD
git push origin HEAD
git tag -a v0.2.0 -m "Release 0.2.0"
git push origin v0.2.0
```

The [release workflow](../.github/workflows/deploy.yml) runs three jobs:

1. **Test:** restore, build, and run tests without deployment credentials.
2. **Build and publish:** publish `ghcr.io/cboyle987/tattooflash:VERSION` for
   `linux/amd64` and create a self-contained `linux-x64` migration bundle named
   `efbundle-VERSION`. The `migrations-VERSION` artifact is retained for seven days.
3. **Deploy staging:** download the bundle from the same run, configure SSH with
   strict ED25519 host-key verification, check sudo access, upload the bundle to
   `/home/deploy/tattooflash/staging/`, and invoke
   `sudo -n /usr/local/sbin/tattooflash-deploy staging VERSION`.

Manually dispatching this release workflow runs tests only. Publication and staging
deployment require a tag push. The staging deployment concurrency group does not
cancel an in-progress deployment; the server also holds an environment-specific
lock. Neither mechanism prevents deploying an older version.

After success, verify the staging catalogue, tag filtering, login/logout, and an
authenticated operation. Check uploads, error states, and mobile layout when
affected. The script's HTTP 200 check does not verify these behaviours.

## Promote to production

1. Confirm the version's staging workflow succeeded and manually check staging.
2. Open **Actions > Deploy production > Run workflow** on the default branch.
3. Enter the tested stable version without `v` and review it before dispatching.

The [production workflow](../.github/workflows/deploy-production.yml) resolves the
tag and finds a successful release push run for that exact tag and commit. It
requires a successful `deploy-staging` job and an unexpired matching migration
artifact, then deploys the existing image and bundle without rebuilding them.

Promote within the seven-day bundle retention window. An expired artifact requires
a reviewed recovery path; do not overwrite a release to recreate it. The check
proves historical staging success, not that the release is still running there.
Manual dispatch authorises a real production deployment, including migrations.
The server script itself does not enforce GitHub staging checks or reviewer
approval; environment protection rules are configured separately in GitHub.

After success, verify production's homepage, authentication, catalogue, and an
authenticated operation, then check that staging remains available.

## GitHub environments and SSH

Configure repository environments named `staging` and `production`:

| Type | Staging name | Production name | Value |
| --- | --- | --- | --- |
| Secret | `STAGING_SSH_PRIVATE_KEY` | `PRODUCTION_SSH_PRIVATE_KEY` | Complete automation private key |
| Variable | `STAGING_HOST` | `PRODUCTION_HOST` | VPS address |
| Variable | `STAGING_USER` | `PRODUCTION_USER` | `deploy` |
| Variable | `STAGING_KNOWN_HOSTS` | `PRODUCTION_KNOWN_HOSTS` | Complete verified ED25519 known-host entry |

Use a dedicated ED25519 automation key for unattended SSH, separate from the
personal administrative key. Install its public half in
`/home/deploy/.ssh/authorized_keys`, for example:

```text
restrict ssh-ed25519 PUBLIC_KEY_DATA TattooFlash GitHub Actions
```

Keep `.ssh` at mode `0700` and `authorized_keys` at `0600`, owned by `deploy`.
`restrict` disables forwarding and PTY allocation but still allows commands and
file transfers. For rotation, install and test the replacement, update the GitHub
secret, verify a deployment, then remove the old authorised public key.

Obtain the host-key entry through the trusted VPS console, using the exact host
value configured in GitHub:

```bash
awk -v host='YOUR_SERVER_IP' '{print host " " $1 " " $2}' /etc/ssh/ssh_host_ed25519_key.pub
```

Store the full `HOST ssh-ed25519 PUBLIC_KEY_DATA` output. A fingerprint alone is
not a known-host entry. Verify legitimate key changes through the console before
updating the variable; keep strict verification enabled.

## Manual staging fallback

Use this when Actions deployment is unavailable. It invokes the same installed
script and requires the infrastructure from the shared VPS guide.

### Use an existing release

Download `migrations-VERSION` from its successful release run and extract
`efbundle-VERSION` into local `artifacts/`. Confirm the matching GHCR image exists.
Keep the image, source revision, and bundle matched. Skip the local build below.

### Build a new release locally

Use a committed revision and an unused stable version. With Docker Desktop running
Linux containers, run from the repository root in PowerShell:

```powershell
$releaseVersion = '0.2.0'
$releaseImage = "ghcr.io/cboyle987/tattooflash:$releaseVersion"
docker login ghcr.io --username cboyle987
dotnet build TattooFlash.slnx
dotnet test TattooFlash.slnx
```

Enter a token with package publishing access at the password prompt. Stop if build
or tests fail. Match the global `dotnet-ef` version to the web project's EF Design
reference (currently `10.0.13`), then build both artifacts from the same revision:

```powershell
docker build --platform linux/amd64 --file TattooFlash.Web/Dockerfile --tag $releaseImage --label "org.opencontainers.image.source=https://github.com/cboyle987/TattooFlash" .
docker push $releaseImage
dotnet ef migrations bundle --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj --configuration Release --self-contained --target-runtime linux-x64 --output "artifacts/efbundle-$releaseVersion"
```

Record the source commit and image digest. Bundle generation uses the design-time
factory and does not apply migrations. The factory reads user secrets and
environment variables, with a placeholder fallback. At deployment, the real
database connection is supplied by the protected Compose environment.

### Transfer and deploy

Set the version even when using existing artifacts. Substitute your server and
personal administrative key path:

```powershell
$releaseVersion = '0.2.0'
$stagingServerIp = 'YOUR_SERVER_IP'
$adminSshKey = "$env:USERPROFILE\.ssh\tattooflash_staging"
scp -i $adminSshKey "artifacts/efbundle-$releaseVersion" "deploy@${stagingServerIp}:/home/deploy/tattooflash/staging/efbundle-$releaseVersion.upload"
ssh -i $adminSshKey "deploy@$stagingServerIp"
```

In the VPS Bash session, finalise the transfer and deploy:

```bash
release_version='0.2.0'
mv "/home/deploy/tattooflash/staging/efbundle-$release_version.upload" "/home/deploy/tattooflash/staging/efbundle-$release_version"
sudo /usr/local/sbin/tattooflash-deploy staging "$release_version"
```

This stops the staging app for backup and migration. Use the production workflow
for routine production promotion; a manual staging deployment does not satisfy
its requirement for a successful staging Actions job.

## Diagnostics

In a personal administrative Bash session on the VPS, select the environment and
version being investigated. Set `app_environment` to `Staging` or `Production`
to match `deployment_environment`:

```bash
deployment_environment='staging'
app_environment='Staging'
release_version='0.2.0'
configuration_directory="/etc/tattooflash/$deployment_environment"
dc() {
  sudo env -i PATH=/usr/sbin:/usr/bin:/sbin:/bin HOME=/root \
    RELEASE_VERSION="$release_version" DEPLOYMENT_ENVIRONMENT="$deployment_environment" \
    ASPNETCORE_ENVIRONMENT="$app_environment" \
    docker compose --project-name "tattooflash-$deployment_environment" \
    --project-directory "$configuration_directory" \
    --env-file "$configuration_directory/$deployment_environment.env" \
    -f "$configuration_directory/docker-compose.deployment.yml" "$@"
}
dc ps
dc logs --tail 100 tattooflash.web postgres
dc exec -T postgres psql -U tattooflash_user -d tattooflash \
  -c 'TABLE "__EFMigrationsHistory";'
```

For shared proxy logs:

```bash
sudo docker compose --project-name tattooflash-proxy \
  --project-directory /etc/tattooflash/proxy \
  -f /etc/tattooflash/proxy/docker-compose.proxy.yml logs --tail 100 caddy
```

Use `dc config --quiet` to validate configuration without printing resolved secrets.
Check image versions, migration history, and logs together. Remove sensitive data
before sharing logs.

## Recovery

| Failure | Response |
| --- | --- |
| Build, publication, artifact download, or SSH preflight fails | Inspect the failed job; it has not reached app shutdown |
| GHCR access denied | Check package access for CI and root's registry login on the VPS |
| Host key rejected | Verify the VPS key through the trusted console and correct the complete known-host entry |
| Sudo denied | Check the installed script and sudo rule ownership/permissions; run `sudo visudo -c` |
| Backup or migration fails | The selected app may remain stopped; inspect backup and migration state before restarting |
| HTTP check fails | Containers remain running; inspect app/proxy logs, DNS, and certificates |
| SSH drops during deployment | Inspect the remote process, lock, containers, and migration history before retrying |

After fixing external configuration, rerun only the failed deployment job in the
original Actions run while its artifact remains available. Confirm this will not
downgrade a newer deployment. Workflow code fixes require a new commit and release;
avoid rerunning successful publication jobs for an existing version.

There is no automatic rollback. An older image does not reverse database migrations;
check schema compatibility before reverting, or plan a forward fix or reviewed
restore. Never delete named volumes or automatically apply Down migrations as a
recovery step. Backups are under `/var/backups/tattooflash/ENVIRONMENT/`; a failed
dump can leave a partial file. See the shared VPS guide for backup limitations.
