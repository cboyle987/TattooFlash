# Branch coverage improvement plan

## Current checkpoint: target reached, 4 November 2026

The full coverage run after adding factory, component, management-page, error-page,
and layout tests passed all **186 tests**, with no failures or skips.
Measured overall branch coverage is **161/178 (90.45%)** and line coverage is
**754/858 (87.88%)**, using the original coverage settings and exclusions.
The denominator changed from the baseline because redundant service guards were removed.

Report: `TestResults/error-layout-coverage/402d9204-03eb-4b85-a959-6370e80e267a/coverage.cobertura.xml`.

`Error.razor` has 100% branch and line coverage. `MainLayout.razor` has 50% branch
coverage and 93.55% line coverage: its dark-mode toggle is private and unused by
the UI, so the alternate icon branch is not reachable through user interaction.
Tests cover the default theme, authenticated/unauthenticated navigation, page body,
and drawer toggle without changing application code or using reflection.

Remaining uncovered branch outcomes are startup (10), image processing (4),
defensive Add/Edit result guards (2), and the layout's unused dark-mode path (1).
The margin above 90% is small; a CI threshold remains a proposed next step.
The historical baseline and implementation plan below are retained for context.

## Baseline: 4 November 2026

Command run from the repository root:

```powershell
dotnet test TattooFlash.slnx --collect:"XPlat Code Coverage" --settings TattooFlash.UnitTest/coverage.runsettings --results-directory TestResults/coverage-baseline
```

All **97 tests passed**, with no failures or skips. The run built both projects successfully.

| Area | Covered branches | Total branches | Branch coverage |
| --- | ---: | ---: | ---: |
| Services | 49 | 57 | 85.96% |
| Repositories | 24 | 24 | 100% |
| Helpers | 5 | 5 | 100% |
| Components, including Razor markup | 3 | 88 | 3.41% |
| Design-time context factory | 0 | 2 | 0% |
| Application startup (`Program.cs`) | 0 | 10 | 0% |
| **Overall** | **81** | **186** | **43.55%** |

Line coverage is **378/862 (43.85%)**. Branch coverage measures the outcomes of decisions exercised, rather than simply whether a line ran. A compound condition can contribute several outcomes.

The report is `TestResults/coverage-baseline/324bf2fc-89b2-4436-84ef-c060cbd8ae07/coverage.cobertura.xml`. Test results are ignored by Git. The existing settings include the Web assembly, exclude migrations and generated files, and preserve async application logic. Keep these settings as the baseline; do not change exclusions to reach the target.

At the current denominator, 90% requires **168/186** covered branches: **87 additional outcomes**, leaving at most 18 uncovered. Recalculate after each batch because source changes can change the denominator. Aim above 90% to allow a small margin.

## Work through small batches

For each batch, first write one representative test together, then expand the scenarios and rerun coverage. Use Arrange, Act, Assert and verify observable behaviour: rendered output, service arguments, callbacks, navigation, and error messages. A passing test must assert the intended result, not merely execute code.

### 1. Lists and management interactions

These components account for **38 currently uncovered outcomes**:

| Component | Current branches | Scenarios |
| --- | ---: | --- |
| `DesignsList` | 0/12 | Pending load shows skeletons; populated and empty results; failure displays error; pagination absent for zero/one page and present for multiple pages; selecting a page forwards its number; successful retry clears the error. |
| `DesignManagement` | 0/16 | Same load/pagination scenarios; deletion updates count and removes the matching item; deleting the last item reloads; availability callback updates only the matching design; unknown ID leaves availability unchanged. |
| `DesignManagementItem` | 0/10 | Edit navigation; confirmed, canceled, and null dialog result; successful deletion and already-deleted response; service failures show snackbar; availability success/failure; pending operation disables actions and prevents duplicate operations; actions recover afterward. |

Use controllable pending tasks to observe loading/busy states before completing operations. Start with `DesignsList`: a pending load followed by success is a focused introduction to asynchronous bUnit tests.

### 2. Form and save flows

These components have **33 uncovered outcomes** across 36 total:

| Component | Current branches | Scenarios |
| --- | ---: | --- |
| `DesignInput` | 2/10 | Create/update labels; required name/image validation; existing image permits submitting without replacement; selected file and model reach callback; pending callback shows saving state and prevents repeat submission; callback failure resets the form state. |
| `AddDesign` | 0/8 code-behind, 1/2 markup | Missing file; valid upload and redirect; processing failure displays mapped error without redirect; stream read/service exception displays generic error; retry clears previous error. |
| `EditDesign` | 0/12 code-behind, 0/4 markup | Pending load, loaded design, failed/missing lookup; null submission/model guards; update with and without replacement image; processing failure; read/service failure; successful navigation; parameter change reloads the requested design. |

Use browser-file test doubles and mocked services; do not call R2 or require PostgreSQL. Exercise the form through events and parent callbacks. Avoid reflection solely to reach private guards or construct impossible result objects.

### 3. Remaining component decisions

These account for **14 uncovered outcomes**:

- `DesignCard` (0/2): available/unavailable rendering, booking button state, dialog receives the selected design.
- `BookingDialog` (0/2): configured booking link and missing-configuration error.
- `Error` (0/8): Activity ID takes precedence, HTTP trace ID fallback, absent context/ID, and request-ID visibility. Restore ambient Activity state after each test.
- `MainLayout` (0/2): both theme states and the toggle's visible result. Render without duplicate MudBlazor providers.

Test delete-dialog confirm/cancel behaviour too, although the report attributes no branches to that component. Branch count does not represent all useful behaviour.

### 4. Design-time factory and startup

- `AppDbContextFactory` (0/2): configured environment variable and fallback. Inspect the returned context configuration without connecting to a database. Restore the environment variable and isolate tests that mutate process-wide state.
- `Program.cs` (0/10): missing required configuration and development/production pipeline paths. Startup needs host-level tests, rather than ordinary mocked service tests. First agree on a minimal host-testing approach and any package or application-entry-point changes. Use dummy configuration and replace external dependencies; do not use real credentials or services.

Startup matters to the arithmetic: even perfect coverage of every currently missing component branch alone would produce only **166/186 (89.25%)**. Covering the factory as well would just reach **168/186 (90.32%)**, but some component guards are not naturally reachable. Plan for startup coverage rather than relying on that theoretical maximum.

### 5. Review remaining service gaps and repeat measurement

- `DesignService`: 14/18 branches; four uncovered outcomes are defensive null-result guards at lines 22, 27, 65, and 69. Current result factories and `IsSuccess` enforce the invariants these guards protect. Do not manufacture invalid objects through reflection just for coverage.
- `ImageProcessingService`: 35/39 branches; remaining outcomes involve nonpositive decoded dimensions (line 53), failed native WebP encoding (line 122), and unknown EXIF orientation (line 152). Investigate which can occur through public inputs before proposing tests or introducing a test seam. Existing tests already cover supported formats, invalid data, decode failure, size limits, resizing, transparency, and all eight EXIF orientations.
- Some analogous save-result guards in Add/Edit and the uninitialized form guard may also be unreachable through normal UI use. Record these explicitly in the final gap review.

Do not refactor production code merely to make a coverage percentage easier to obtain. Any proposed removal of redundant guards or introduction of abstractions should have an independent correctness/design reason and be agreed separately.

## Verification and completion

1. Run focused tests while working, then the full command above after each batch. Use a distinct results directory per checkpoint so reports cannot be confused.
2. Track raw covered/total branches as well as percentages, including per-component gaps. A first pass through a batch may leave legitimate defensive branches uncovered.
3. Finish when the full suite passes and the aggregate report shows at least 90% with the agreed existing scope. Retain a short explanation of remaining uncovered paths.
4. Agree on a CI coverage check that fails below 90% once reached. The collector produces a report; a separate threshold check must enforce the target. Keep collecting the report as a review artifact and guard against missing/empty reports.
5. Separately verify affected UI flows, errors, and mobile layout when the app/database are available. bUnit does not execute browser JavaScript or prove responsive layout, and SQLite repository tests do not prove PostgreSQL-specific behaviour.

No application or test code was changed for this baseline. No runtime/browser validation was performed; this task measured the automated suite and planned the next work.

## Separate issues to revisit

Source review identified two concerns to investigate through behaviour tests before fixing:

- `EditDesign` renders `DesignInput` with a potentially null design after a failed or missing lookup. The child dereferences that parameter; verify whether the intended error display is replaced by a rendering exception.
- Repository page-size validation rejects negative values but accepts zero despite the error text requiring a value greater than zero. Add a zero boundary case when agreeing the desired behaviour.

These were not changed as part of coverage analysis.
