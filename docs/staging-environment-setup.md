# One-time staging environment setup

This document records the one-time server, registry, and external-service setup
for TattooFlash staging on an IONOS Ubuntu 24.04 Linux x64 VPS.

The staging site is https://tattoo-flash-staging.connorboyle.dev. The SSH account
is `deploy`, and deployment files live in `/home/deploy/tattooflash`.

Complete this setup before following [Manual staging build and deployment](manual-staging-deployment.md).
That guide covers the first application deployment and subsequent releases.

## Server and external services

The following were completed for the first deployment:

- An SSH key was created on Windows at
  `C:\Users\boyle\.ssh\tattooflash_staging`. Its public key was installed for
  `deploy`, which has `sudo` access. Root SSH login and password-based SSH login
  were disabled, and a fresh key-based login was verified.
- Ubuntu was updated. UFW allows inbound TCP ports 22, 80, and 443 and denies other
  incoming traffic. The IONOS network firewall must also allow those ports.
- Docker Engine, Buildx, and the Compose plugin were installed using
  [Docker's official Ubuntu repository](https://docs.docker.com/engine/install/ubuntu/).
  `sudo docker run --rm hello-world` and `sudo docker compose version` succeeded.
- DNS has an A record for `tattoo-flash-staging.connorboyle.dev` pointing to the
  VPS's public IPv4 address. Any AAAA record must also point to a working IPv6
  endpoint. Caddy obtains and renews the public HTTPS certificate.
- A separate Auth0 **Regular Web Application** was created for staging, with the
  intended user connection enabled. Its allowed callback URL is
  `https://tattoo-flash-staging.connorboyle.dev/callback`; its allowed logout URL is
  `https://tattoo-flash-staging.connorboyle.dev/`.
- A separate staging R2 bucket was created, with bucket-scoped Object Read & Write
  S3 credentials. Its public development URL serves staging images. This URL is
  public and rate limited; use a custom domain for production.

Only Caddy's ports are published by the staging stack. The app and PostgreSQL use
Docker networks. Docker-published ports can bypass normal UFW filtering, so do not
add public database or app port mappings as a troubleshooting shortcut.

### Registry authentication

For manual GHCR authentication, use GitHub personal access tokens **(classic)**:

- Windows publishing token: `write:packages`.
- VPS pull token: `read:packages`.

Authenticate on Windows with Docker Desktop running:

```powershell
docker login ghcr.io --username cboyle987
```

Authenticate on the VPS:

```bash
sudo docker login ghcr.io --username cboyle987
```

Paste the appropriate token at the password prompt. Do not put tokens directly in
commands, source control, or this document. Use `sudo docker` consistently on the
VPS: its credentials are stored under `/root/.docker/`, separate from `deploy`'s
configuration. Without a credential helper, Docker warns that the stored
credentials are unencrypted. Renew expiring tokens as needed.

New GHCR packages default to private. Confirm the package's visibility and its
association with `cboyle987/TattooFlash` in GitHub's Packages settings.

### Secret configuration on the VPS

Create this once if provisioning a replacement server:

```bash
install -d -m 700 "$HOME/.config/tattooflash"
touch "$HOME/.config/tattooflash/staging.env"
chmod 600 "$HOME/.config/tattooflash/staging.env"
nano "$HOME/.config/tattooflash/staging.env"
```

File structure, using real values only on the server:

```dotenv
POSTGRES_PASSWORD='REPLACE_WITH_GENERATED_PASSWORD'
AUTH0_DOMAIN='REPLACE_WITH_AUTH0_HOSTNAME'
AUTH0_CLIENT_ID='REPLACE_WITH_STAGING_CLIENT_ID'
AUTH0_CLIENT_SECRET='REPLACE_WITH_STAGING_CLIENT_SECRET'
R2_ACCESS_KEY_ID='REPLACE_WITH_STAGING_ACCESS_KEY_ID'
R2_SECRET_ACCESS_KEY='REPLACE_WITH_STAGING_SECRET_ACCESS_KEY'
R2_BUCKET_NAME='REPLACE_WITH_STAGING_BUCKET_NAME'
R2_ENDPOINT='REPLACE_WITH_S3_API_ENDPOINT'
R2_PUBLIC_BASE_URL='REPLACE_WITH_PUBLIC_BUCKET_URL'
```

`AUTH0_DOMAIN` has no `https://` prefix. `R2_PUBLIC_BASE_URL` has no trailing
slash; `R2_ENDPOINT` is the S3 API URL, not the public image URL. Compose currently
sets the image object prefix to `designs`.

`openssl rand -hex 32` generates a database password that can be embedded safely
in the current connection-string template. Store it in a password manager.
Changing `POSTGRES_PASSWORD` later does **not** change the password of a database
user in an existing PostgreSQL volume; password rotation needs a coordinated
database change.

The env file supplies Compose's `${...}` substitutions. The app service's
`environment` block explicitly passes the required values into its container.
Keep this file outside the repository and build context.

## Deployment directory and first image downloads

On the VPS, create the directory that will receive the Compose files, Caddy
configuration, and migration bundles:

```bash
mkdir -p "$HOME/tattooflash"
```

Follow the manual deployment guide to build the first release, copy its artifacts,
and define the `dc` shell shortcut. During its validation and pull step, also run:

```bash
dc pull caddy postgres
```

These fetch the infrastructure images configured in the base Compose file. Keep
the deployment directory and Compose project name stable on subsequent releases
so the same named volumes are reused.

Continue with the deployment guide to start PostgreSQL, apply the migration
bundle, and launch and verify the site. The initial deployment completed these
steps successfully; it does not need repeating as server setup for each release.
