# EF Core setup cheatsheet

A reusable reference for .NET applications using a relational database. Examples use a generic `Product` entity and SQLite; substitute your database provider as needed.

## 1. Choose compatible versions and a provider

- Check the project's `<TargetFramework>` and the EF Core release's supported .NET versions.
- Keep Microsoft EF Core packages on the same version. Match `dotnet-ef` to that version where possible, and at least to the same major version.
- Check third-party provider compatibility before choosing an EF Core version.

| Database | NuGet provider | Configuration method |
| --- | --- | --- |
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | `UseSqlite(connectionString)` |
| SQL Server / Azure SQL | `Microsoft.EntityFrameworkCore.SqlServer` | `UseSqlServer(connectionString)` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | `UseNpgsql(connectionString)` |

Install one provider for the walkthrough. Provider packages bring in the EF Core runtime dependencies.

See [provider compatibility](https://learn.microsoft.com/en-us/ef/core/providers/).

## 2. Install packages and tools

Run package commands in the directory containing the `.csproj`. Replace `<EF_VERSION>` with a compatible, explicit version; angle-bracket values throughout this document are placeholders.

```powershell
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version <EF_VERSION>
dotnet add package Microsoft.EntityFrameworkCore.Design --version <EF_VERSION>
```

For a reproducible, repository-local CLI tool, run these at the repository root:

```powershell
# Create the manifest only if the repository does not already have one.
dotnet new tool-manifest
dotnet tool install dotnet-ef --version <EF_VERSION>
dotnet ef --version
```

Commit `.config/dotnet-tools.json`. After cloning, run `dotnet tool restore`.
Update an existing local tool with `dotnet tool update dotnet-ef --version <EF_VERSION>`.

Alternative: install globally with `dotnet tool install --global dotnet-ef --version <EF_VERSION>`.
The `Microsoft.EntityFrameworkCore.Tools` package is for Visual Studio Package Manager Console; it is not required for `dotnet ef`.

See the [CLI reference](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## 3. Define an entity and context

`Product.cs`:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

`AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}
```

Conventions recognize `Id` as the primary key. With nullable reference types enabled, `string` is required and `string?` is optional. Database enforcement of length and precision depends on the provider; SQLite does not enforce these facets like SQL Server does. Validate input in the application too.

## 4. Configure the connection and dependency injection

For a local SQLite database, add this section to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

A relative SQLite path is resolved from the process working directory. Use a consistent working directory or an absolute path if tools and the app appear to use different databases.

In an ASP.NET Core application's `Program.cs`, before `builder.Build()`:

```csharp
using Microsoft.EntityFrameworkCore;

// builder is created with WebApplication.CreateBuilder(args).
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
```

For credentials in local development, use user secrets instead of committed configuration:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<CONNECTION_STRING>"
```

ASP.NET Core loads user secrets by default in Development. For deployment, supply configuration through the hosting platform or secret store; the environment variable key is `ConnectionStrings__DefaultConnection`.

### Blazor Server, interactive server components, and long-lived services

Use a factory when a DI scope outlives one database operation. Register this **instead of** the `AddDbContext` example:

```csharp
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));
```

Inject `IDbContextFactory<AppDbContext>` into the component or service, then create and dispose a context per operation:

```csharp
await using var db = await factory.CreateDbContextAsync();
var products = await db.Products.AsNoTracking().ToListAsync();
```

For ordinary HTTP request handlers, injecting the scoped `AppDbContext` is a common default. Contexts are not thread-safe: await each operation and never share one across concurrent tasks. Browser-based clients access a server API rather than connecting directly to the database.

See [context configuration and lifetimes](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/).

## 5. Create and apply the first migration

Run from the application project directory:

```powershell
dotnet build
dotnet ef migrations add InitialCreate
# Review the generated migration before applying it.
dotnet ef database update
```

The migration describes schema changes; `database update` applies them. Commit the generated migrations and model snapshot. Do not combine `EnsureCreated()` with a migrations-based workflow.

## 6. Everyday database operations

These snippets assume an available `AppDbContext db` and `using Microsoft.EntityFrameworkCore;`.

```csharp
// Create
var product = new Product { Name = "Notebook", Price = 4.50m };
db.Products.Add(product);
await db.SaveChangesAsync();

// Read: no tracking is useful when results will not be edited.
var products = await db.Products
    .AsNoTracking()
    .Where(p => p.Name.StartsWith("Note"))
    .OrderBy(p => p.Name)
    .Take(20)
    .ToListAsync();

// Update: queried entities are tracked by default.
var existing = await db.Products.SingleAsync(p => p.Id == product.Id);
existing.Name = "Large notebook";
await db.SaveChangesAsync();

// Delete
db.Products.Remove(existing);
await db.SaveChangesAsync();
```

`SingleAsync` throws if no row matches; use `SingleOrDefaultAsync` and handle `null` when absence is expected. Use `Select` to fetch only required columns and `Include` when you need related entities. Adding, editing, or removing tracked entities does not persist changes until `SaveChangesAsync()`.

## 7. Migration command reference

| Task | Command |
| --- | --- |
| Add a schema change | `dotnet ef migrations add AddProductDescription` |
| Apply pending migrations locally | `dotnet ef database update` |
| List migrations | `dotnet ef migrations list` |
| Remove the last unapplied migration | `dotnet ef migrations remove` |
| Move database to a named migration | `dotnet ef database update <MigrationName>` |
| Generate SQL | `dotnet ef migrations script --output migrations.sql` |
| Generate a migration bundle | `dotnet ef migrations bundle` |
| List discovered contexts | `dotnet ef dbcontext list` |

For every model change: edit the model → add a migration → inspect it → apply it. Generated migrations compare the model against the snapshot, not the live database. Inspect rename operations carefully to avoid accidental drop-and-recreate changes.

Rolling back can lose data. Only remove an applied migration after rolling back a disposable/local database; for shared or deployed databases, create a corrective migration. Test production changes and deploy using a reviewed SQL script or migration bundle. Providers that support idempotent scripts can use `migrations script --idempotent`; SQLite does not support this option.

See [migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) and [deployment options](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## 8. Separate data and startup projects

Use explicit project paths from the solution root:

```powershell
dotnet ef migrations add InitialCreate --project src/Example.Data --startup-project src/Example.Web --context AppDbContext
dotnet ef database update --project src/Example.Data --startup-project src/Example.Web --context AppDbContext
```

- `--project`: contains the context/migrations and receives generated files.
- `--startup-project`: builds and runs to supply configuration and services; it must reference the data project and have the Design package available.
- `--context`: selects a context when more than one exists.

## 9. Troubleshooting

| Symptom | Check |
| --- | --- |
| `dotnet ef` not found | Restore local tools, or install the tool; check the global tool PATH if applicable. |
| Tool/runtime version warning | Align tool and EF packages; verify provider compatibility. |
| Unable to create a context | Build first; check the public options constructor, registration, startup project, and configuration. Use `IDesignTimeDbContextFactory<TContext>` when normal startup cannot construct it. |
| Missing connection string during tooling | Set the intended environment, e.g. `dotnet ef database update -- --environment Development`, and check configuration in the startup project. |
| No table / wrong database | Apply migrations and verify the connection string, environment, and SQLite path. |
| A second operation started on this context | Await database calls; use a separate context per concurrent operation. |
| Changes do not persist | Call `SaveChangesAsync`; use tracked entities for edits. |
| Existing tables conflict with first migration | Do not apply an initial create migration blindly to an existing schema; use a deliberate baseline or reverse-engineering workflow. |

For an existing database, see [reverse engineering](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/) before choosing who owns schema changes.

## 10. Seed initial data

Seeding inserts starting records, such as lookup values or development examples. Choose one approach for each dataset:

| Approach | Use for |
| --- | --- |
| `UseSeeding` / `UseAsyncSeeding` (EF Core 9+) | Initialization that checks existing database contents |
| `HasData` | Small, fixed lookup datasets owned by migrations |

### General-purpose seeding (EF Core 9+)

Replace the earlier context registration with this example in `Program.cs`. It uses the existing `Product` entity and SQLite connection string; substitute your provider as needed. The same options configuration works with `AddDbContextFactory`.

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString)
        .UseSeeding((context, _) =>
        {
            if (!context.Set<Product>().Any(p => p.Name == "Notebook"))
            {
                context.Set<Product>().Add(new Product
                {
                    Name = "Notebook",
                    Price = 4.50m
                });
                context.SaveChanges();
            }
        })
        .UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            if (!await context.Set<Product>().AnyAsync(
                p => p.Name == "Notebook", cancellationToken))
            {
                context.Set<Product>().Add(new Product
                {
                    Name = "Notebook",
                    Price = 4.50m
                });
                await context.SaveChangesAsync(cancellationToken);
            }
        }));
```

Both callbacks should perform equivalent work: tooling uses the synchronous callback. The existence check makes repeated runs safe for this example; for real data, identify records by a stable key and enforce uniqueness in the database.

Run after creating the schema migrations:

```powershell
dotnet ef database update
```

Callbacks run during EF initialization/migration operations, including when no migrations are pending. Merely starting the application or calling `SaveChanges` does not invoke them. Changing callback code alone needs no new migration. Generated SQL scripts do not execute these callbacks; use tooling, a migration bundle, or a dedicated initializer. Account for older schemas if your deployment supports migration downgrades.

### Fixed lookup data with `HasData`

For migration-owned values, define an entity such as:

```csharp
public class ProductStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

Add this inside the existing context's `OnModelCreating` method:

```csharp
modelBuilder.Entity<ProductStatus>().HasData(
    new ProductStatus { Id = 1, Name = "Active" },
    new ProductStatus { Id = 2, Name = "Discontinued" });
```

Create, review, and apply a migration:

```powershell
dotnet ef migrations add SeedProductStatuses
dotnet ef database update
```

Keep explicit primary keys and values deterministic: avoid `DateTime.Now` or newly generated GUIDs. Supply foreign key values for related records. Changing `HasData` requires another migration; editing these records outside migrations can conflict with later updates. Keep sample data out of production by explicitly gating its registration to the intended environment.

See the [official data seeding guide](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) for lifecycle details and limitations.
