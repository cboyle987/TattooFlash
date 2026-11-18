# TattooFlash

A tattoo flash catalogue, giving clients a place to browse designs and the artist a way to manage them without updating the website's source code.

Visitors can browse designs, filter by tag, check prices and availability, and start a booking enquiry through Instagram. Authenticated users manage designs, images, and tags through a separate management interface.

**[Visit the website](https://www.emmamccormicktattoo.com)**

## Features

- **Public catalogue:** responsive design cards, pagination, tag filtering, prices, and availability indicators.
- **Booking enquiries:** a design details dialog links to the artist's Instagram messages. Booking arrangements are handled outside the application.
- **Design management:** create, edit, and delete designs; upload replacement images; assign multiple tags; and toggle availability.
- **Tag management:** create, rename, and delete tags, with validation and duplicate-name handling.
- **Image processing:** validate JPEG, PNG, and WebP uploads, apply image orientation, resize large images, and encode them as WebP before storing them in Cloudflare R2.
- **Operational tooling:** automated tests, container builds, staging deployments, manual production promotion, and Sentry error reporting.

Management access is intentionally available to every authenticated user. The deployed Auth0 applications have public signup disabled, and accounts are created manually for authorised users.

## Technology

| Area | Technology |
| --- | --- |
| Application | C# / .NET 10, ASP.NET Core Blazor Interactive Server |
| Interface | MudBlazor |
| Persistence | Entity Framework Core, Npgsql, PostgreSQL |
| Authentication | Auth0 |
| Images | SkiaSharp, Cloudflare R2 through its S3-compatible API |
| Testing | xUnit, Moq, bUnit, in-memory SQLite, Coverlet |
| Hosting | Docker Compose, Caddy, Linux VPS |
| Delivery and monitoring | GitHub Actions, GitHub Container Registry, Sentry |

## Architecture

The application follows a component-to-service-to-repository structure. Blazor components handle presentation and user interaction, services coordinate application operations, and repositories handle database queries and persistence.

```text
Blazor components
    -> Application services
        -> Repositories -> EF Core -> PostgreSQL
        -> Image processing -> Cloudflare R2
```

Interactive components execute on the server. Each repository operation creates and disposes its own EF Core context through `IDbContextFactory<AppDbContext>`, avoiding a database context shared across a long-lived Blazor circuit. Read-only queries use no tracking where appropriate.

Image uploads and database writes are coordinated by the design service. If a database save fails after an upload, the service attempts to remove the newly uploaded image. A replacement image is saved before the previous image is removed.

## Run locally

Run the commands below from the repository root. The direct .NET setup is independent of the NAS database used by the project's existing Docker development environment.

### Prerequisites

- .NET 10 SDK.
- A PostgreSQL database and a database user with permission to apply the application's migrations.
- An Auth0 **Regular Web Application**, with a user connection and a manually provisioned account for management access.
- A Cloudflare R2 bucket, bucket-scoped credentials with object read/write access, and a public URL for serving catalogue images.

Auth0 and R2 configuration are required at startup, including when you only intend to browse the public catalogue. Sentry configuration is optional for local development. Building and running the automated tests do not require these service credentials.

### 1. Restore dependencies

```powershell
git clone https://github.com/cboyle987/TattooFlash.git
cd TattooFlash
dotnet restore TattooFlash.slnx
```

### 2. Configure the application

Store local credentials in the web project's .NET user secrets. In Visual Studio, right-click `TattooFlash.Web` and choose **Manage User Secrets**, or set values from the CLI:

```powershell
dotnet user-secrets --project TattooFlash.Web/TattooFlash.Web.csproj set "ConnectionStrings:DefaultConnection" "<YOUR_LOCAL_CONNECTION_STRING>"
```

The example command sets one value; configure the remaining values through the same CLI or the secrets editor. The complete configuration has this shape. Replace all placeholders with values for your own development services:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=tattooflash_dev;Username=tattooflash_dev;Password=<LOCAL_DATABASE_PASSWORD>"
  },
  "Auth0": {
    "Domain": "<YOUR_AUTH0_DOMAIN>",
    "ClientId": "<YOUR_AUTH0_CLIENT_ID>",
    "ClientSecret": "<YOUR_AUTH0_CLIENT_SECRET>"
  },
  "R2": {
    "AccessKeyId": "<YOUR_R2_ACCESS_KEY_ID>",
    "SecretAccessKey": "<YOUR_R2_SECRET_ACCESS_KEY>",
    "BucketName": "<YOUR_DEVELOPMENT_BUCKET>",
    "Prefix": "designs",
    "Endpoint": "https://<YOUR_ACCOUNT_ID>.r2.cloudflarestorage.com",
    "PublicBaseUrl": "https://<YOUR_PUBLIC_IMAGE_HOST>"
  }
}
```

`Auth0:Domain` is the hostname without `https://`. `R2:PublicBaseUrl` should have no trailing slash. Override all R2 values above so your local instance uses your own bucket rather than the defaults in `appsettings.json`.

For the included HTTPS launch profile, configure these URLs in your Auth0 application:

| Auth0 setting | Local value |
| --- | --- |
| Allowed Callback URLs | `https://localhost:7077/callback` |
| Allowed Logout URLs | `https://localhost:7077/` |

Keep public signup disabled if you use the same access model as the deployed application. Use a separate Auth0 application and R2 bucket for development.

Optional settings include `Sentry:Dsn` and `Sentry:Environment`. Site branding and the booking destination are configured with `SiteName` and `Booking:InstagramUserName`; `DefaultPageSize` controls catalogue pagination.

### 3. Apply migrations to your development database

Install the EF Core CLI, matching the web project's `Microsoft.EntityFrameworkCore.Design` package version:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
```

If the tool is already installed, use `dotnet tool update` with the same version instead. After confirming that your connection string targets your own development database, apply the existing migrations:

```powershell
dotnet ef database update --project TattooFlash.Web/TattooFlash.Web.csproj --startup-project TattooFlash.Web/TattooFlash.Web.csproj
```

The design-time context factory reads the web project's user secrets and environment variables. Without a configured connection string it uses a placeholder intended for generating migrations, which cannot connect to a real database.

### 4. Build and start the app

```powershell
dotnet dev-certs https --trust
dotnet build TattooFlash.slnx
dotnet run --project TattooFlash.Web/TattooFlash.Web.csproj --launch-profile https
```

Open **https://localhost:7077**. Sign in with your provisioned Auth0 account to add designs and tags. A new database starts without catalogue content; add designs through the management interface.

For Docker and Visual Studio Compose development, see [Container development](docs/container-development.md). That setup mounts local user secrets and expects a reachable NAS PostgreSQL database; it is not a self-contained fresh-clone demo.

## Tests

```powershell
dotnet test TattooFlash.slnx
```

The test suite covers application services, Blazor components, repositories, image processing, configuration, and login redirect handling. It runs without PostgreSQL, Auth0, or Cloudflare credentials.

- **Service tests** use xUnit and Moq to exercise application behaviour and failure handling.
- **Component tests** use bUnit and MudBlazor providers to exercise rendering and interactions.
- **Repository tests** use isolated in-memory SQLite databases to exercise real relational queries. They do not verify PostgreSQL-specific behaviour.

Pull request CI builds the application, runs tests, and enforces a minimum of **85% branch coverage** using the repository's coverage settings. This is a CI threshold, not a claim about the current measured coverage.

See the [component testing guide](TattooFlash.UnitTest/Components/README.md) and [repository testing guide](TattooFlash.UnitTest/Repositories/README.md) for setup details.

## Deployment

Version tags trigger the release workflow, which runs tests, publishes a container image to GHCR, builds an EF Core migration bundle, and deploys to staging. Production promotion is a separate manually triggered workflow that checks for a successful staging release and a matching retained migration artifact.

Staging and production use separate Compose projects, databases, configuration, and persistent volumes behind a shared Caddy reverse proxy. The deployment script backs up the target database before applying migrations, then starts the application and checks its HTTP response. Deployment credentials and environment files are supplied outside source control.

- [Shared VPS setup and environment configuration](docs/shared-vps-compose.md)
- [Releases, production promotion, manual deployment, and recovery](docs/deployment.md)

## Repository layout

```text
TattooFlash.Web/
  Components/       Pages, layouts, and reusable Razor components
  Configuration/    Validated options and proxy configuration
  Data/             EF Core context and design-time factory
  Endpoints/        Login endpoint handling
  Interfaces/       Application service contracts
  Migrations/       EF Core migrations and model snapshot
  Models/           Domain models and operation results
  Repositories/     Database queries and persistence
  Services/         Application operations and image processing/storage
TattooFlash.UnitTest/  Service, component, repository, and configuration tests
.github/workflows/    Validation, release, and deployment workflows
docs/                 Local development, VPS setup, and deployment guides
scripts/              Deployment and local development helpers
```

The guides under `docs/` describe the current development and deployment setup. Start with this README for application features and direct .NET setup.

## Licence

The project is licensed under the [MIT Licence](LICENSE).
