# Repository guidance

## Project overview

TattooFlash is a tattoo flash catalogue with design management functionality.
It uses .NET 10, Blazor Interactive Server, MudBlazor, EF Core, and PostgreSQL
through Npgsql. The solution is `TattooFlash.slnx`; the application project is
`TattooFlash.Web/TattooFlash.Web.csproj`.

## Learning-first collaboration

This project has two goals: produce useful software and help the owner learn.
Prioritise understanding and participation over completing implementation on the
owner's behalf.

- Explain the reasoning behind recommendations, relevant concepts, and tradeoffs
  at a level that helps the owner understand what they are implementing.
- When asked for help with an issue, investigate and explain the root cause first,
  then guide the owner towards a fix. Distinguish confirmed findings from hypotheses.
  Do not immediately edit application code to fix the issue yourself.
- Start with a small, focused hint that helps the owner work out the next step.
  Offer progressively more guidance as needed, but provide full implementation
  details or complete solutions only when explicitly asked.
- For questions such as "How do I do X?", start with a conceptual hint. Use small,
  generic examples when helpful without revealing the complete solution. Let the
  owner write the project-specific implementation.
- Break work into manageable steps, explain each step's purpose, and give the owner
  opportunities to implement it and ask questions before moving on.
- When reviewing the owner's code, explain what works, identify problems and why
  they matter, and offer hints towards improvements before supplying replacement code.
- Implement changes directly when the owner explicitly asks you to do so. Explain
  what changed and why so the implementation still supports learning.
- Read-only investigation is welcome when it helps provide accurate guidance.
  Do not interpret a request for help or guidance as permission to implement the solution.

## Code organisation

- `TattooFlash.Web/Components/`: Razor components, pages, layouts, and code-behind files.
- `TattooFlash.Web/Services/`: application operations and image URL resolution.
- `TattooFlash.Web/Repositories/`: database queries and persistence.
- `TattooFlash.Web/Models/`: domain models.
- `TattooFlash.Web/Data/`: EF Core database context.
- `TattooFlash.Web/Migrations/`: EF Core migrations and model snapshot.
- `TattooFlash.Web/wwwroot/samples/`: legacy sample image assets; catalogue uploads use Cloudflare R2.
- `docs/`: current local development, VPS setup, and deployment guides.
- `scripts/`: development helpers, including trusting Caddy's local CA on Windows.
  `Deploy.sh` is the source for the separately installed, root-owned VPS
  deployment command; workflow runs do not update that installed copy.

## Implementation conventions

- Follow the conventions in the surrounding code; avoid unrelated formatting or refactoring.
- Use existing MudBlazor components and UI patterns when extending the interface.
- Follow the existing component-to-service-to-repository structure. Keep database
  access out of Razor components and application rules out of presentation code.
- Use the existing interfaces and register new dependencies in `Program.cs`.
- In repositories, create and dispose a context per operation through
  `IDbContextFactory<AppDbContext>`. Keep database operations asynchronous and use
  `AsNoTracking()` for read-only queries where appropriate.
- Preserve existing entity identities. Design IDs currently use `Guid`.
- Follow existing `.razor.cs` code-behind patterns when extending components that use them.
- Before introducing dependencies, architectural changes, or database schema changes,
  explain the options and recommend an approach. Agree on the direction with the
  owner before implementing unless that direction has already been explicitly authorised.

## Build and verification

Run commands from the repository root:

```powershell
dotnet build TattooFlash.slnx
dotnet run --project TattooFlash.Web/TattooFlash.Web.csproj
```

Running the application requires a configured `ConnectionStrings:DefaultConnection`
and access to PostgreSQL. Use .NET user secrets or environment variables for credentials.

- Build after changing application code.
- Explain how to verify a change and why the suggested checks matter. Guide the
  owner through writing tests when they add value, following the hint-first approach.
- Service unit tests live in `TattooFlash.UnitTest/Services/` and use xUnit and Moq.
  Blazor component tests live in `TattooFlash.UnitTest/Components/` and use bUnit.
  Test folders and namespaces mirror the corresponding application folders, such
  as `Components/Designs/`, `Components/Tags/`, and `Components/Dialogs/`.
  Tag management page tests live in `Components/Pages/Management/Tags/`.
  Shared component test
  infrastructure remains in the test project's `Components/` root.
  Create a fresh `ComponentTestContext` per test with `await using`, register mocks
  before rendering, and use `RenderWithProviders<TComponent>` for MudBlazor providers.
  See `TattooFlash.UnitTest/Components/README.md` for authentication and JS interop setup.
  Repository tests live in `TattooFlash.UnitTest/Repositories/` and use isolated
  in-memory SQLite databases with a mocked context factory. These exercise real
  relational queries but do not verify PostgreSQL-specific behaviour.
  Run `dotnet test TattooFlash.slnx`. Tests use Arrange, Act, Assert and require no
  database or Cloudflare credentials.
  Configuration tests live in `TattooFlash.UnitTest/Configuration/` and verify
  options validation, including rejection of invalid values during host startup.
- For behaviour changes, perform the
  relevant manual checks when the application and database are available.
- For UI changes, check the affected page, loading and error states, and mobile layout.
- Report what was verified and any checks blocked by missing configuration or tooling.
  Do not claim a successful build proves runtime behaviour.

## Database and configuration

- Keep credentials and connection string secrets out of source control and output.
- For EF model changes, include an appropriate migration and updated snapshot.
  Inspect generated migrations for unintended changes.
- Do not rewrite existing migrations that may have been applied without establishing
  that doing so is appropriate for the task.
- Apply database migrations or seed scripts only when the task authorises it and
  the target database is known.
- Avoid dependency upgrades unrelated to the requested change.

## Reference documents

- `README.md`: project overview, implemented features, architecture, and direct
  .NET development setup.
- `docs/shared-vps-compose.md`: shared-VPS Compose layout, infrastructure
  provisioning, protected configuration, and backup locations.
- `docs/container-development.md`: current container development setup and usage of
  `scripts/Trust-CaddyLocalCertificate.ps1` for Windows certificate trust.
- `docs/deployment.md`: version-tag CI/CD, GitHub environments and SSH,
  production promotion, manual staging fallback, diagnostics, and recovery.

Tags, catalogue filtering, and Cloudflare R2 image uploads are implemented.
Use current source code for their behaviour; completed feature plans and generic
reference notes have been removed from `docs/`.

## Scope and handoff

- Keep changes focused on the requested task and preserve unrelated user changes.
- Flag unrelated issues separately, explaining their impact without fixing them
  automatically or expanding the task.
- Explain the resulting behaviour, relevant verification, and any remaining limitations.
- Keep relevant documentation accurate when behaviour changes and clearly distinguish
  planned features from implemented ones. Follow the learning-first approach when
  documentation updates are part of an implementation the owner is carrying out.
- Update this file when project structure, commands, or agreed conventions change.
