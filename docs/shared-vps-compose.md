# Shared VPS Compose layout

The new Compose files prepare staging and production to share a single VPS.
`scripts/Deploy.sh` supports this layout. The workflow deploys staging using the
new script; production deployment automation is deferred until staging has been
tested. The existing staging setup guides describe the older server layout.

## Files

- `docker-compose.deployment.yml`: standalone app and PostgreSQL stack, used once
  per environment with separate project names and protected env files.
- `docker-compose.proxy.yml`: standalone `tattooflash-proxy` project containing
  the single Caddy service and creating both proxy networks.
- `Caddyfile.server`: staging and production hostname routes.

Do not combine these files with `docker-compose.yml` or the local override.
Local development and the existing staging files remain unchanged.

## Environment selection

Each protected env file needs the existing database, Auth0, R2, and Sentry settings,
plus these values:

| Setting | Staging | Production |
| --- | --- | --- |
| Compose project (`-p`) | `tattooflash-staging` | `tattooflash-production` |
| `DEPLOYMENT_ENVIRONMENT` | `staging` | `production` |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |

Supply `RELEASE_VERSION` for the image being deployed. Keep project names stable:
they determine the database and data protection volume identities. The new staging
project uses fresh volumes; it does not migrate data from the old project.

Caddy joins both proxy networks. Each app joins only its environment's proxy
network and its own database network. Neither app nor PostgreSQL publishes host
ports. Separate aliases `staging-web` and `production-web` identify Caddy's targets.

## Transition prerequisites

Before starting this layout on the VPS:

- Install the reviewed `Deploy.sh` and new server configuration before triggering
  the updated workflow. It no longer supports the old installed script.
- Retire the old staging Caddy and proxy network during a planned maintenance
  window. Its ports and `172.30.10.0/24` subnet conflict with the new proxy stack.
- Point both hostnames at the VPS and configure production's external services.
- Start the proxy project before provisioning the application stacks, so their
  external proxy networks exist. Do not tear down those networks while apps use them.

The new Caddy project also uses fresh certificate/configuration volumes. It must
obtain certificates for both hostnames. An environment without a running app will
return a proxy error without preventing the other environment from working.

## Deployment command

The script source is now `scripts/Deploy.sh` (formerly `DeployStaging.sh`). Install
the reviewed file as the root-owned `/usr/local/sbin/tattooflash-deploy`, mode
`0755`. Install it alongside the new server configuration before running the
updated staging workflow. The former single-version invocation is rejected.

```bash
sudo /usr/local/sbin/tattooflash-deploy staging 0.2.0
sudo /usr/local/sbin/tattooflash-deploy production 0.2.0
```

These are separate real deployments, each with a maintenance window. Production
approval will be enforced by the GitHub environment, not by this server script.

| Path | Purpose |
| --- | --- |
| `/etc/tattooflash/ENVIRONMENT/docker-compose.deployment.yml` | Root-owned app Compose file |
| `/etc/tattooflash/ENVIRONMENT/ENVIRONMENT.env` | Root-owned secrets, mode `0600` |
| `/home/deploy/tattooflash/ENVIRONMENT/efbundle-VERSION` | Incoming bundle readable by `deploy` |
| `/var/lib/tattooflash/releases/ENVIRONMENT/VERSION/efbundle` | Root-owned executable bundle copy |
| `/var/backups/tattooflash/ENVIRONMENT/` | Private pre-migration database backups |
| `/run/tattooflash-deploy/ENVIRONMENT.lock` | Prevents overlapping deployments to this environment |

Replace `ENVIRONMENT` with `staging` or `production`. Create the incoming directories
as `deploy`; keep active configuration and privileged script directories owned
by root and unwritable by `deploy`. The script supplies the project name,
`DEPLOYMENT_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT`, and `RELEASE_VERSION` explicitly
in a clean environment, overriding any conflicting values in the env file.

The existing sudo rule for `/usr/local/sbin/tattooflash-deploy *` supports both
environments. It grants deployment access to production too; staging SSH
credentials using that account are therefore not isolated from production access.

Provision PostgreSQL separately before the first deployment using the appropriate
project and env file with `docker compose ... up -d postgres`. The script requires
a running database: it validates Compose, pulls the app image, checks PostgreSQL,
stops only the selected app, backs up and inspects the database archive, runs the
matching migration bundle, restarts only that app, and checks its HTTPS homepage.
Shared Caddy is neither restarted nor reconfigured by the deployment command.

Backup or migration failures after app shutdown leave it stopped for investigation;
there is no automatic rollback. A successful homepage check does not verify login
or database features. Check those separately after deployment. Never use an older
image or bundle without reviewing schema compatibility.

## Staging workflow

The existing tag-triggered test and publishing jobs are unchanged. The staging
job retains its GitHub environment, SSH settings, and concurrency group. It now
checks sudo access for `tattooflash-deploy staging VERSION`, creates the incoming
staging directory as `deploy`, uploads the bundle with an `.upload` suffix, renames
it after transfer, and invokes that same command. There is no production job.

The workflow does not install scripts, Compose files, or Caddy configuration.
Before pushing a new version tag, complete the server transition above and ensure
the new staging PostgreSQL service is healthy. A successful run should produce a
backup under `/var/backups/tattooflash/staging/` and pass the staging HTTPS check.
Then check login and app features manually; also verify shared Caddy remains up.
Manual workflow dispatch still runs tests only and does not deploy.

## Validation

`docker compose ... config --quiet` validates interpolation and Compose structure
without printing resolved secrets. Validate the deployment file separately for
each environment and validate the proxy file separately. This does not verify
DNS, certificates, app startup, proxy routing, or database connectivity; those
require runtime checks after the remaining deployment changes are complete.
