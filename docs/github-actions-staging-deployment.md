# GitHub Actions staging deployment

> The server instructions below describe the previous staging-only setup.
> The workflow now invokes `scripts/Deploy.sh` as `staging VERSION` and uploads
> bundles to `/home/deploy/tattooflash/staging/`. Complete the server transition
> in [Shared VPS layout](shared-vps-compose.md) before running it. That guide also
> documents the separate manual production promotion workflow.

TattooFlash builds, tests, publishes, and deploys staging when a version tag is
pushed. This process has completed successfully against the IONOS VPS. The site
is https://tattoo-flash-staging.connorboyle.dev.

Implementation:

- [Workflow](../.github/workflows/deploy.yml)
- [Server deployment script source](../scripts/Deploy.sh)
- [EF design-time context factory](../TattooFlash.Web/Data/AppDbContextFactory.cs)
- [Staging Compose configuration](../docker-compose.staging.yml)

For initial infrastructure, see [Staging environment setup](staging-environment-setup.md).
For local builds and deployment outside Actions, see [Manual deployment](manual-staging-deployment.md).

## Release a version

Use a committed source revision containing the intended code and reviewed EF
migrations. Select an unused stable SemVer version, such as `0.2.0`, and run from
the repository root in PowerShell:

```powershell
git status --short
git rev-parse HEAD
git push origin HEAD
git tag -a v0.2.0 -m "Release 0.2.0"
git push origin v0.2.0
```

Review the status and commit before tagging. The tag identifies the commit that
will be tested and built. Pushing the tag automatically deploys staging and causes
a brief maintenance window; no approval step is defined in the workflow itself.
Any configured GitHub environment protection rules also apply.

Open the repository's Actions tab and select **Build and Push Docker Image**.
Check all three jobs and then verify the site and login in a browser.

| Trigger | Behaviour |
| --- | --- |
| Push a tag matching `v*.*.*` | Test, publish image and bundle, deploy staging |
| Run workflow manually | Build and test only; publishing is skipped |
| Ordinary branch push or pull request | No automatic run is currently configured |

The tag filter is a glob, not a SemVer validator. Metadata derives image version
`0.2.0` from Git tag `v0.2.0`. The deployment preflight and server script accept
only stable `MAJOR.MINOR.PATCH` versions without leading zeroes. Prerelease tags
may build and publish, but deployment currently rejects them. Do not use
prereleases until those checks have been deliberately extended.

Treat published version tags as immutable. Do not move Git release tags or
overwrite their image tags. No `latest` image tag is generated.

## Pipeline jobs and artifacts

### Test

The `test` job checks out the tagged commit, installs .NET 10, restores the
solution, builds the web project in Release, and runs
`TattooFlash.UnitTest/TattooFlash.UnitTest.csproj`. These tests need no staging
database, Auth0, Cloudflare, or Sentry credentials.

### Build and publish

`build-and-push` requires successful tests and runs only for pushed version tags.
It has its own runner, so it installs the SDK and EF tooling and restores the web
project again. Files produced by the test job are not shared automatically.

The job generates and uploads the migration bundle before building and publishing
the image. Both come from the same checkout:

| Output | Example |
| --- | --- |
| Private image | `ghcr.io/cboyle987/tattooflash:0.2.0` |
| Workflow artifact | `migrations-0.2.0` |
| Bundle inside artifact | `efbundle-0.2.0` |
| Target platform | Linux x64 / `linux/amd64` |
| Artifact retention | Seven days |

The bundle is self-contained and generated with explicit `--project` and
`--startup-project` arguments pointing to the web project. EF tooling is currently
pinned to `10.0.13`; keep it aligned with the project's EF Design package when
deliberately updating dependencies.

`AppDbContextFactory` lets tooling construct the PostgreSQL model without running
the application's startup configuration. In CI it uses a non-secret placeholder
connection string. Generating the bundle does not connect to staging. At execution
time, the factory reads `ConnectionStrings__DefaultConnection` supplied by Compose.
It does not automatically read local user secrets.

The Docker build uses the repository root context and
`TattooFlash.Web/Dockerfile`. `.dockerignore` excludes `artifacts/`, keeping the
bundle separate from the image. Image metadata includes source and revision
labels. Publishing authenticates through the job's `GITHUB_TOKEN` with
`packages: write`, not a personal publishing token.

### Deploy staging

`deploy-staging` requires successful publication. It uses the GitHub `staging`
environment and downloads the named bundle from the same workflow run. It does
not check out the repository or install an updated server script.

The job:

1. Writes the dedicated private key and verified known-host entry into the runner's
   SSH configuration, with strict ED25519 host-key verification and batch mode.
2. Validates the stable version, checks the downloaded file, and uses `sudo -n -l`
   to check permission for the installed deployment command without executing it.
3. Uploads the bundle to `/home/deploy/tattooflash/efbundle-VERSION.upload` and
   renames it to `efbundle-VERSION` after transfer succeeds.
4. Invokes `sudo -n /usr/local/sbin/tattooflash-deploy VERSION` through SSH.
5. Removes the runner's SSH files in an `always()` cleanup step.

The `staging-deploy` concurrency group prevents deployment jobs from overlapping,
with `cancel-in-progress: false`. The server script also holds a filesystem lock,
covering manual invocations. This does not enforce chronological version order;
check for a newer deployment before rerunning an older release.

## Server-side deployment

| Path | Purpose |
| --- | --- |
| `/usr/local/sbin/tattooflash-deploy` | Installed, root-owned deployment executable |
| `/etc/tattooflash/staging/` | Root-owned Compose files, Caddyfile, and `staging.env` |
| `/home/deploy/tattooflash/` | Incoming migration bundles |
| `/var/lib/tattooflash/releases/VERSION/efbundle` | Root-owned bundle copy used for migration |
| `/var/backups/tattooflash/` | Database dump archives |
| `/run/tattooflash-deploy/deployment.lock` | Root-controlled deployment lock |
| `/etc/sudoers.d/tattooflash-deploy` | Passwordless permission for the installed script |

The script runs as root, accepts exactly one stable version argument, and:

1. Acquires a non-blocking lock; a concurrent invocation fails immediately.
2. Reads the incoming bundle as `deploy`, copies it to a root-owned release
   directory, and sets executable permissions.
3. Validates Compose configuration without printing resolved secrets and pulls
   the requested image from GHCR.
4. Checks that the existing PostgreSQL service is accepting connections.
5. Stops the web app, takes a custom-format `pg_dump`, and inspects the archive
   with `pg_restore --list`.
6. Runs the bundle in a temporary app-image container on the private database
   network, with staging configuration. No database port is published.
7. Starts the stack with `up -d --no-build` and lists containers.
8. Checks the public HTTPS homepage for HTTP 200, up to 12 attempts, with a
   five-second delay between failures and bounded connection/request timeouts.

The Compose project name remains `tattooflash`, preserving container and volume
identities. Relative mounts resolve against `/etc/tattooflash/staging`. The
script passes `RELEASE_VERSION` explicitly in a clean environment and uses root's
GHCR login. PostgreSQL must already be provisioned and running.

Stopping the app begins the maintenance window. Blazor circuits disconnect, and
Caddy may return a gateway error until startup completes. This is not a zero
downtime deployment.

## GitHub environment and SSH configuration

Create a repository environment named `staging` with:

| Type | Name | Value |
| --- | --- | --- |
| Secret | `STAGING_SSH_PRIVATE_KEY` | Complete dedicated OpenSSH private key, including BEGIN/END lines |
| Variable | `STAGING_HOST` | VPS IP/hostname used by SSH |
| Variable | `STAGING_USER` | `deploy` |
| Variable | `STAGING_KNOWN_HOSTS` | Complete `HOST ssh-ed25519 PUBLIC_KEY_DATA` line |

The dedicated key was created on Windows as
`C:\Users\boyle\.ssh\tattooflash_staging_actions`, without a passphrase for
unattended execution. To create a replacement using a new filename:

```powershell
ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\tattooflash_staging_actions_new" -C "TattooFlash staging GitHub Actions"
```

Leave the passphrase empty for this automation key. Install and test the new
public key, update the GitHub secret, verify access, and then remove the old
public key from `authorized_keys`. Do not overwrite the personal administrative
key. Only the automation key's public half belongs in the server's
`/home/deploy/.ssh/authorized_keys`, on a separate line from the personal key:

```text
restrict ssh-ed25519 PUBLIC_KEY_DATA TattooFlash staging GitHub Actions
```

The `restrict` option disables PTY allocation, forwarding, and SSH user startup
scripts. It still allows non-interactive commands and file transfers as `deploy`;
it is not a forced-command restriction. Protect and rotate this automation key.
Keep the directory at mode `0700` and `authorized_keys` at `0600`, owned by `deploy`.

Obtain the server's public host-key entry through the trusted IONOS console,
replacing the placeholder with exactly the value of `STAGING_HOST`:

```bash
awk -v host='YOUR_SERVER_IP' '{print host " " $1 " " $2}' /etc/ssh/ssh_host_ed25519_key.pub
```

Store the full output in `STAGING_KNOWN_HOSTS`, without surrounding quotes. A
`SHA256:...` fingerprint or a public key lacking the hostname field is not a valid
replacement. Verify a legitimate server/key change through the console before
updating the variable; keep strict checking enabled.

GHCR package settings must grant the `cboyle987/TattooFlash` repository **Write**
access under **Manage Actions access**, or provide equivalent inherited access.
The workflow's permission block alone cannot grant access to an existing package.
The VPS separately authenticates as root using a classic PAT with `read:packages`.

## Install and update privileged server files

Changes to `scripts/DeployStaging.sh`, Compose files, and Caddy configuration are
not uploaded by the pipeline. Review them and transfer them using the personal
administrative key. Use LF line endings for the shell script.

For example, from the repository root in Windows PowerShell:

```powershell
$stagingServerIp = 'YOUR_SERVER_IP'
scp -i "$env:USERPROFILE\.ssh\tattooflash_staging" scripts/DeployStaging.sh docker-compose.yml docker-compose.staging.yml Caddyfile.staging "deploy@${stagingServerIp}:~/tattooflash/"
```

On the VPS, install the reviewed script:

```bash
sudo install -o root -g root -m 755 \
  /home/deploy/tattooflash/DeployStaging.sh \
  /usr/local/sbin/tattooflash-deploy
```

For reviewed configuration updates staged in `/home/deploy/tattooflash`:

```bash
sudo install -o root -g root -m 644 \
  /home/deploy/tattooflash/docker-compose.yml \
  /home/deploy/tattooflash/docker-compose.staging.yml \
  /home/deploy/tattooflash/Caddyfile.staging \
  /etc/tattooflash/staging/
```

Keep the env file root-owned at mode `0600`. Editing it directly with
`sudoedit /etc/tattooflash/staging/staging.env` avoids putting credentials into
repository files or shell history. Add required settings before deploying code
that needs them. The image reference must continue to use
`${RELEASE_VERSION:?Set RELEASE_VERSION}` rather than a hardcoded tag.

The sudo rule is installed using
`sudo visudo -f /etc/sudoers.d/tattooflash-deploy`:

```sudoers
deploy ALL=(root) NOPASSWD: /usr/local/sbin/tattooflash-deploy *
```

The file must be owned by `root:root` at mode `0440`; validate with
`sudo visudo -c`. Only the protected script is granted passwordless execution,
and the script validates arguments. Deploying executable application code through
Docker is still a privileged operation; this is not a sandbox for untrusted code.

Test without performing a deployment:

```bash
sudo -k
sudo -n /usr/local/sbin/tattooflash-deploy invalid
```

Expect the version-validation message and a nonzero exit, without a password
prompt. For a complete test, invoke the script with an existing valid image and
matching incoming bundle; that performs a real maintenance-window deployment.

## Verification and recovery

A successful workflow confirms tests, artifact generation, SSH execution,
database backup/archive inspection, migration completion, container startup, and
the HTTPS homepage smoke check. It does not prove login, image uploads, all
database queries, mobile layout, or backup restoration. Check affected features
in the browser after a release.

For diagnostics, connect with the personal administrative key and define:

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
```

Use the version being investigated. Logs may contain sensitive data; remove it
before sharing. Never print resolved Compose configuration into Actions logs.

| Failure | Response |
| --- | --- |
| Test, bundle generation, publication, or SSH preflight fails | Deployment has not reached the app-stop step; inspect that job's logs |
| `NETSDK1004`, missing `project.assets.json` | Restore the web project in the publishing job; test-job restore is on another runner |
| GHCR `permission_denied: read_package` | Check package Manage Actions access; `packages: write` already includes reading |
| No ED25519 host key is known | Correct the full matching `STAGING_KNOWN_HOSTS` entry and rerun the failed job |
| Sudo file has bad permissions | Restore `root:root`, mode `0440`, and run `sudo visudo -c` |
| Backup or migration fails after app stop | App may remain stopped; investigate before restarting against a possibly changed schema |
| HTTP smoke check fails | Containers remain running; inspect app/Caddy logs, DNS, and certificate configuration |
| SSH connection drops during deployment | Check the remote process, lock, containers, and migration history before retrying; a disconnected runner is not proof the remote work stopped |

For configuration or permission fixes outside the workflow, rerun only the failed
deployment job in the original Actions run. Confirm it will not unintentionally
downgrade a newer release. Reruns use the original commit/ref; workflow code fixes
require a new commit and new version tag. Avoid rerunning successful publishing
jobs or overwriting an already published version. Artifact downloads depend on
the seven-day retention; retain required bundles separately for longer recovery.

The script has no automatic rollback. Image rollback does not reverse migrations.
Only return to an older app after checking schema compatibility; otherwise plan
a forward fix or a reviewed database restore. Do not run an older migration
bundle casually: it contains only that release's migration set. Never use
`docker compose down -v` as a deployment or recovery step.

Backups live under `/var/backups/tattooflash` and currently have no automatic
retention or off-server replication. A failed dump can leave a partial file.
`pg_restore --list` checks readability, not restorability. Establish off-server
copies and test restoration separately. Database dumps do not cover R2 objects,
Caddy certificates/configuration, or ASP.NET data protection keys.

References: [GitHub container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry),
[workflow reruns](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs),
[EF design-time factories](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation),
and [OpenSSH authorized keys](https://man.openbsd.org/sshd.8#AUTHORIZED_KEYS_FILE_FORMAT).
