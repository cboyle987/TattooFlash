# Repository tests

`DesignRepositoryTests` uses xUnit, Moq and EF Core SQLite. Each test opens its own
in-memory database and disposes it afterwards. The mocked context factory returns
a fresh real context for every repository call; queries and persistence are not
mocked. Seed and verification contexts are separate from repository contexts.

Run all tests from the repository root:

```powershell
dotnet test TattooFlash.slnx
```

Run only repository tests with coverage:

```powershell
dotnet test TattooFlash.UnitTest/TattooFlash.UnitTest.csproj --filter FullyQualifiedName~TattooFlash.UnitTest.Repositories --collect:"XPlat Code Coverage" --results-directory TattooFlash.UnitTest/TestResults
```

The generated `coverage.cobertura.xml` includes per-class `line-rate` and
`branch-rate` values. Inspect `TattooFlash.Web.Repositories.DesignRepository`
and its nested async state-machine entries, rather than the overall application
percentage. The repository coverage target is at least 90%; the initial suite
measured 100% for both lines and branches. Coverage is measured, not automatically
enforced as a threshold.

The test project automatically uses `../coverage.runsettings` (relative to this
folder). Coverage includes the web application and excludes test assemblies,
EF migrations and the model snapshot, generated source files, and code marked
with `GeneratedCodeAttribute` or `ExcludeFromCodeCoverageAttribute`. Async and
iterator implementations remain included; `CompilerGeneratedAttribute` is
deliberately not excluded. Application services, repositories, models and
component code-behind remain in scope even when they have no tests yet.

These are repository integration tests using SQLite, hosted in the existing unit
test project. They require no external database or credentials. PostgreSQL
collation, UUID ordering, migrations and concurrent transaction behaviour need
separate tests against PostgreSQL.

Known issue outside this change: `DefaultPageSize = 0` is currently accepted,
although the repository's validation message requires a value greater than zero.
The tests cover missing, negative, malformed and overflowing configuration values;
they do not establish zero as valid configuration.
