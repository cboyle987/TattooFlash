<#
.SYNOPSIS
Trusts the running development Caddy container's root CA for the current Windows user.
.EXAMPLE
.\scripts\Trust-CaddyLocalCertificate.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'This helper supports Windows only.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker was not found on PATH. Start Docker Desktop and use a terminal where docker is available.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
$certificatePath = Join-Path ([System.IO.Path]::GetTempPath()) ("TattooFlash-Caddy-{0}.crt" -f [guid]::NewGuid())

Push-Location $repositoryRoot
try {
    # An explicit base file also works with containers started by Visual Studio's overrides.
    & docker compose -f $composeFile cp 'caddy:/data/caddy/pki/authorities/local/root.crt' $certificatePath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $certificatePath)) {
        throw 'Could not copy the Caddy root certificate. Start this repository''s Compose project with Caddy, then retry. If using a custom Compose project name, set COMPOSE_PROJECT_NAME to that name.'
    }

    $certificate = Get-PfxCertificate -FilePath $certificatePath
    $trustedCertificatePath = 'Cert:\CurrentUser\Root\{0}' -f $certificate.Thumbprint
    if (Test-Path -LiteralPath $trustedCertificatePath) {
        Write-Host 'This Caddy root certificate is already trusted by your Windows user.'
    }
    else {
        Write-Host ("Trusting Caddy root CA: {0} ({1})" -f $certificate.Subject, $certificate.Thumbprint)
        Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
        if (-not (Test-Path -LiteralPath $trustedCertificatePath)) {
            throw 'The certificate was not added to the current user''s trusted root store.'
        }
        Write-Host 'Caddy root certificate trusted for your Windows user.'
    }

    Write-Host 'Restart your browser, then open https://localhost.'
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $certificatePath) {
        Remove-Item -LiteralPath $certificatePath
    }
}
