# Container development

The current Compose configuration runs the app and Caddy. PostgreSQL and the
remaining production configuration are still to be added. Visual Studio's
`docker-compose.override.yml` supplies development settings and local secret mounts.

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

This helper is for local Windows development. The VPS deployment will use a public
domain and Caddy-managed publicly trusted certificates; visitors won't need this step.

References: [Caddy in Docker](https://caddyserver.com/docs/running#docker-compose)
and [Windows Import-Certificate](https://learn.microsoft.com/en-us/powershell/module/pki/import-certificate).
