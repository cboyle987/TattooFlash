# Shared VPS setup

Staging and production run as separate Docker Compose projects on a Linux x64
VPS, behind one Caddy reverse proxy. This guide covers infrastructure and protected
configuration. For routine releases, manual deployment, and recovery, see
[Deployment](deployment.md).

## Layout

| Project | Configuration | Purpose |
| --- | --- | --- |
| `tattooflash-proxy` | [docker-compose.proxy.yml](../docker-compose.proxy.yml), [Caddyfile.server](../Caddyfile.server) | Shared HTTPS entry point |
| `tattooflash-staging` | [docker-compose.deployment.yml](../docker-compose.deployment.yml), `staging.env` | Staging app and PostgreSQL |
| `tattooflash-production` | Same deployment Compose file, `production.env` | Production app and PostgreSQL |

Caddy joins both proxy networks. Each app joins its environment's proxy network
and its own database network. Only Caddy publishes host ports, 80 and 443; app and
database ports are private. PostgreSQL data, Caddy state, and application data
protection keys use named volumes.

Keep project names stable because they determine volume identities. Do not merge
these server Compose files with the local `docker-compose.yml`, development
override, or legacy `docker-compose.staging.yml`.

| Setting | Staging | Production |
| --- | --- | --- |
| `DEPLOYMENT_ENVIRONMENT` | `staging` | `production` |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |
| Proxy alias | `staging-web` | `production-web` |
| Hostname | `tattoo-flash-staging.connorboyle.dev` | `www.emmamccormicktattoo.com` |

## Provision a replacement server

These steps are for initial provisioning or rebuilding infrastructure, not routine
application releases. Preserve existing volumes and configuration during maintenance.

1. Install Docker Engine with the Compose and Buildx plugins on the Linux x64 VPS.
   The existing deployment uses Ubuntu. The deployment script also needs `curl`,
   `flock`, and `runuser`.
2. Set up the `deploy` SSH account with a personal administrative key and sudo
   access. Verify key-based access before disabling password and root SSH login.
3. Allow inbound SSH and HTTP/HTTPS through the host and provider firewalls.
   Keep PostgreSQL and app ports private.
4. Point both hostnames at the VPS. Any IPv6 records must also reach the server.
   Caddy obtains public certificates for these hostnames.
5. Configure separate Auth0 applications, R2 buckets/credentials, and Sentry
   settings for staging and production. Auth0 callback URLs are
   `https://HOSTNAME/callback`; logout URLs are `https://HOSTNAME/`. Disable public
   signup and provision management users manually.

### Registry access

The server pulls `ghcr.io/cboyle987/tattooflash:VERSION` using root's Docker
credentials. Authenticate with a token able to read that package, entering it at
the password prompt:

```bash
sudo docker login ghcr.io --username cboyle987
```

Root's Docker configuration is separate from `deploy`'s configuration. For
automated publishing, the GHCR package must grant the repository write access
under **Manage Actions access** or equivalent inherited access. CI uses its
built-in `GITHUB_TOKEN`. Keep tokens outside the repository and build context.

### Install reviewed configuration

Transfer `docker-compose.proxy.yml`, `Caddyfile.server`,
`docker-compose.deployment.yml`, and `scripts/Deploy.sh` from the reviewed revision
to `/home/deploy/tattooflash/config/` using the personal administrative SSH key.
Create that staging directory as `deploy` if needed. On the VPS, install:

```bash
sudo install -d -o root -g root -m 755 \
  /etc/tattooflash/proxy /etc/tattooflash/staging /etc/tattooflash/production
sudo install -o root -g root -m 644 \
  /home/deploy/tattooflash/config/docker-compose.proxy.yml \
  /home/deploy/tattooflash/config/Caddyfile.server /etc/tattooflash/proxy/
sudo install -o root -g root -m 644 \
  /home/deploy/tattooflash/config/docker-compose.deployment.yml /etc/tattooflash/staging/
sudo install -o root -g root -m 644 \
  /home/deploy/tattooflash/config/docker-compose.deployment.yml /etc/tattooflash/production/
sudo install -o root -g root -m 755 \
  /home/deploy/tattooflash/config/Deploy.sh /usr/local/sbin/tattooflash-deploy
```

The source for the installed executable is [scripts/Deploy.sh](../scripts/Deploy.sh).
Use LF line endings. Workflows do not update this script or the protected Compose
and Caddy files; reviewed changes require administrative installation.

### Environment files

Create `/etc/tattooflash/staging/staging.env` and
`/etc/tattooflash/production/production.env`, owned by root with mode `0600`.
Use `sudoedit` to edit them. Each contains its environment's values:

```dotenv
POSTGRES_PASSWORD='REPLACE_WITH_GENERATED_PASSWORD'
AUTH0_DOMAIN='REPLACE_WITH_AUTH0_HOSTNAME'
AUTH0_CLIENT_ID='REPLACE_WITH_CLIENT_ID'
AUTH0_CLIENT_SECRET='REPLACE_WITH_CLIENT_SECRET'
R2_ACCESS_KEY_ID='REPLACE_WITH_ACCESS_KEY_ID'
R2_SECRET_ACCESS_KEY='REPLACE_WITH_SECRET_ACCESS_KEY'
R2_BUCKET_NAME='REPLACE_WITH_BUCKET_NAME'
R2_ENDPOINT='REPLACE_WITH_S3_API_ENDPOINT'
R2_PUBLIC_BASE_URL='REPLACE_WITH_PUBLIC_IMAGE_URL'
SENTRY_DSN='REPLACE_WITH_SENTRY_DSN'
```

`AUTH0_DOMAIN` excludes `https://`. `R2_PUBLIC_BASE_URL` has no trailing slash;
`R2_ENDPOINT` is the S3 API URL. Compose sets the object prefix to `designs`.
Generate a database password suitable for the connection-string template, for
example with `openssl rand -hex 32`. Updating `POSTGRES_PASSWORD` does not change
the password in an existing PostgreSQL volume; coordinate rotation with the database.

### Deployment permissions

As `deploy`, create the incoming bundle directories:

```bash
install -d -m 700 /home/deploy/tattooflash/staging /home/deploy/tattooflash/production
```

Keep active configuration, the installed script, and their parent directories
root-owned and unwritable by `deploy`. Install the automation sudo rule through
`sudo visudo -f /etc/sudoers.d/tattooflash-deploy`:

```sudoers
deploy ALL=(root) NOPASSWD: /usr/local/sbin/tattooflash-deploy *
```

The sudoers file must be `root:root`, mode `0440`; validate with `sudo visudo -c`.
The script validates the environment and stable version arguments. This account
can deploy executable code to both environments: separate SSH keys on the same
account do not isolate staging from production access.

### Start infrastructure

Start the shared proxy first so the external app proxy networks exist:

```bash
sudo docker compose --project-name tattooflash-proxy \
  --project-directory /etc/tattooflash/proxy \
  -f /etc/tattooflash/proxy/docker-compose.proxy.yml config --quiet
sudo docker compose --project-name tattooflash-proxy \
  --project-directory /etc/tattooflash/proxy \
  -f /etc/tattooflash/proxy/docker-compose.proxy.yml up -d
```

Define the environment-specific `dc` helper in
[Deployment diagnostics](deployment.md#diagnostics), choosing an existing image
version and the environment being provisioned. For each environment, run:

```bash
dc config --quiet
dc up -d postgres
dc ps
```

Wait for PostgreSQL to become healthy before the first application deployment.
An environment with no running app can return a proxy error while the other
environment remains available. Provision GitHub environments using the
[deployment guide](deployment.md#github-environments-and-ssh).

## Deployment state

The installed script accepts `staging|production VERSION`. It supplies the Compose
project, environment selectors, and release version explicitly in a clean
environment, validates configuration, pulls the image, checks PostgreSQL, stops
the selected app, backs up the database, runs migrations, and restarts that app.
Caddy is not restarted by an application deployment.

| Path | Purpose |
| --- | --- |
| `/etc/tattooflash/ENVIRONMENT/` | Protected deployment Compose file and `ENVIRONMENT.env` |
| `/home/deploy/tattooflash/ENVIRONMENT/efbundle-VERSION` | Incoming migration bundle |
| `/var/lib/tattooflash/releases/ENVIRONMENT/VERSION/efbundle` | Root-owned executable bundle copy |
| `/var/backups/tattooflash/ENVIRONMENT/` | Private pre-migration database backups |
| `/run/tattooflash-deploy/ENVIRONMENT.lock` | Lock preventing overlapping deployments to that environment |

Backups have no automatic retention or off-server replication. Archive inspection
with `pg_restore --list` is not a restore test. Maintain protected off-server
copies and verify restoration separately. Database dumps do not cover R2 images,
Caddy state, or application data protection keys.
