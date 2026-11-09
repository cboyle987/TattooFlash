#!/usr/bin/env bash
set -euo pipefail

export PATH=/usr/sbin:/usr/bin:/sbin:/bin

if [[ "$EUID" -ne 0 ]]; then
  printf 'Run this script using sudo\n' >&2
  exit 1
fi


command -v curl >/dev/null

# Validate supplied release version argument
if [[ "$#" -ne 2 ]]; then
  printf 'Usage: tattooflash-deploy staging|production VERSION\n' >&2
  exit 1
fi

deployment_environment="$1"
case "$deployment_environment" in
  staging)
    app_environment=Staging
    site_url="https://tattoo-flash-staging.connorboyle.dev"
    ;;
  production)
    app_environment=Production
    site_url="https://www.emmamccormicktattoo.com"
    ;;
  *)
    printf 'Expected environment staging or production\n' >&2
    exit 1
    ;;
esac

release_version="$2"
if [[ ! "$release_version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  printf 'Expected a release version such as 0.1.5\n' >&2
  exit 1
fi


# Acquire a lock to prevent concurrent deployments
configuration_directory="/etc/tattooflash/$deployment_environment"
cd "$configuration_directory"
install -d -o root -g root -m 700 /run/tattooflash-deploy
exec 9> "/run/tattooflash-deploy/$deployment_environment.lock"

if ! flock -n 9; then
  printf 'Another %s deployment is already running\n' "$deployment_environment" >&2
  exit 1
fi


# Define a helper function to run docker compose commands with the correct environment
dc() {
  env -i PATH="$PATH" HOME=/root RELEASE_VERSION="$release_version" \
    DEPLOYMENT_ENVIRONMENT="$deployment_environment" ASPNETCORE_ENVIRONMENT="$app_environment" \
    docker compose \
    --project-name "tattooflash-$deployment_environment" \
    --project-directory "$configuration_directory" \
    --env-file "$configuration_directory/$deployment_environment.env" \
    -f "$configuration_directory/docker-compose.deployment.yml" \
    "$@"
}

# Validate the existence of the efbundle file for the specified release version
incoming_bundle="/home/deploy/tattooflash/$deployment_environment/efbundle-$release_version"

if ! runuser -u deploy -- test -f "$incoming_bundle"; then
  printf 'Migration bundle not found: %s\n' "$incoming_bundle" >&2
  exit 1
fi

release_directory="/var/lib/tattooflash/releases/$deployment_environment/$release_version"
install -d -o root -g root -m 755 "$release_directory"

bundle_path="$release_directory/efbundle"

runuser -u deploy -- cat -- "$incoming_bundle" > "$bundle_path"
chmod 755 "$bundle_path"

# Verify that the Docker Compose configuration is valid and pull the latest image for the web service
dc config --quiet
dc config --images 

dc pull tattooflash.web


# Check if the PostgreSQL database is ready to accept connections
if ! dc exec -T postgres \
  pg_isready -U tattooflash_user -d tattooflash; then
  printf 'PostgreSQL is not ready; deployment stopped\n' >&2
  exit 1
fi

# Create a backup of the selected database before applying migrations
backup_directory="/var/backups/tattooflash/$deployment_environment"
install -d -m 700 "$backup_directory"
umask 077

backup_file="$backup_directory/pre-$release_version-$(date -u +%Y%m%dT%H%M%SZ).dump"

dc stop tattooflash.web

printf 'Backing up %s database\n' "$deployment_environment"
dc exec -T postgres \
  pg_dump -U tattooflash_user -d tattooflash -Fc \
  > "$backup_file"

dc exec -T postgres pg_restore --list < "$backup_file" > /dev/null
printf 'Backup archive created and inspected: %s\n' "$backup_file"

# Apply migrations and start the selected application
printf 'Applying %s migrations\n' "$deployment_environment"
dc run --rm --no-deps \
  --volume "$bundle_path:/migration/efbundle:ro" \
  --entrypoint /migration/efbundle \
  tattooflash.web

printf 'Starting %s application\n' "$deployment_environment"
dc up -d --no-build --no-deps tattooflash.web
dc ps

# Smoke test deployment 
site_ready=false

printf 'Checking %s HTTP response\n' "$deployment_environment"

for attempt in {1..12}; do
  if status_code=$(curl --silent --show-error \
    --connect-timeout 3 \
    --max-time 5 \
    --output /dev/null \
    --write-out '%{http_code}' \
    "$site_url"); then
    if [[ "$status_code" == "200" ]]; then
      site_ready=true
      break
    fi
  fi

  printf 'Site not ready; attempt %s of 12\n' "$attempt"
  sleep 5
done

if [[ "$site_ready" != true ]]; then
  printf '%s HTTP check failed; inspect application and shared Caddy logs\n' "$deployment_environment" >&2
  exit 1
fi

printf 'Deployment of %s to %s completed; site returned HTTP 200\n' "$release_version" "$deployment_environment"
