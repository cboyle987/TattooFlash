# Design tags: staged implementation plan

Status: implementation in progress. Tag entities, relationship configuration,
migrations, repository and service layer are implemented with automated tests.
Tag add/edit pages and their shared input form are implemented and linked from
the management list. Design assignment and homepage filtering remain planned.
The owner implements the stories with hint-first guidance and review, and may
explicitly delegate individual implementation tasks. This document does not
authorise an agent to implement the remaining feature automatically.

## Summary and agreed behaviour

Introduce one shared **tag catalogue**. Each design can have zero or more tags,
and visitors can filter the homepage by one tag at a time.

- Tag management uses the existing signed-in management access.
- Admins create, rename and delete tags.
- Deleting a tag removes its assignments and preserves the designs.
- Design forms select existing tags only. Creating tags within the form is a
  possible future feature.
- “All tags” shows every design, including designs without tags.

Follow the existing component → service → repository structure. Implement the
stories in order, with a review checkpoint after each story.

## Stage 1 — Store the tag catalogue

### Story 1: Persist tags and their relationship to designs

**As an admin, I want designs to support multiple shared tags so that I can
organise the catalogue consistently.**

Implementation:

- Add a `Tag` entity with a `Guid` ID, display name, normalized name and designs
  collection; add a tags collection to `DesignModel`.
- Configure an EF-managed many-to-many relationship with a `DesignTags` join
  table. An explicit join entity is unnecessary because assignments have no
  additional data.
- Require names of 1–100 characters after trimming. Use invariant uppercase
  normalization and a unique index to prevent case-insensitive duplicates.
- Configure deletion to remove join rows when either entity is deleted, without
  deleting the related entities.
- Generate and inspect a migration and updated snapshot. Preserve existing
  design IDs and data.

**Acceptance:** Existing designs remain valid with no tags; one tag can belong
to several designs; duplicate assignments are prevented.

**First learning hint:** Sketch the three tables and identify which table
represents “this design has this tag.” Adapt the existing tag proposal’s
integer-key examples to `Guid`.

**Verification:** Relational tests for shared assignments, uniqueness and
deletion behaviour. Review the migration before applying it to an explicitly
identified development database.

## Stage 2 — Manage the catalogue

### Story 2: List and create tags

**As an admin, I want to view existing tags and create a tag so that it becomes
available for design assignment.**

Implementation:

- Introduce `ITagRepository`/`TagRepository` and `ITagService`/`TagService`;
  register them in dependency injection.
- Support listing tags alphabetically and creating tags by name.
- Keep trimming, normalization and validation in the service; use database
  uniqueness as the final safeguard, including concurrent creates.
- Return a friendly duplicate-name outcome rather than exposing a database
  exception.
- Add an authorised `/management/tags` page with a tag list, create form and
  navigation entry. Use MudBlazor and existing code-behind conventions.
- Use an unpaginated list for the initial, small catalogue.

**Acceptance:** A valid name creates one tag; blank, oversized and duplicate
names show useful validation; loading, empty and failure states are visible.

**First learning hint:** Trace one existing design operation through all three
layers, then identify which responsibilities transfer to tag creation.

**Verification:** Service validation tests, repository persistence tests, and
focused component tests for creation and failure.

### Story 3: Rename a tag

**As an admin, I want to rename a tag so that its name changes everywhere without
reassigning designs.**

Implementation:

- Add update operations to the tag interfaces.
- Provide editing on the management page using a reusable tag form and a local
  edit value.
- Preserve the tag ID and assignments; apply the same naming rules as creation.
- Handle a tag deleted by another session with a clear message and refreshed
  list.

**Acceptance:** Renaming changes the shared name; cancel leaves it unchanged;
duplicate names are rejected.

**First learning hint:** Consider why changing the name should not require
updating every design.

**Verification:** Test preserved assignments, duplicate rejection, cancel and
missing-tag handling.

### Story 4: Delete a tag safely

**As an admin, I want to delete a tag so that obsolete labels disappear from the
catalogue.**

Implementation:

- Add delete operations and a confirmation dialog explaining that assignments
  will be removed.
- Delete the tag and its links atomically through the configured relationship.
- Disable repeated actions while saving and follow existing snackbar patterns.

**Acceptance:** Cancel changes nothing; confirming removes the tag and
assignments; designs and images remain intact; an already-deleted tag receives
an informative response.

**First learning hint:** Distinguish deleting an entity from deleting the
relationship between two entities.

**Verification:** Test linked and unused tags, cancellation, repeated deletion
and database failure.

## Stage 3 — Assign tags to designs

### Story 5: Save a design’s selected tags

**As an admin, I want selected tags saved with a design so that assignments
survive reopening it.**

Implementation:

- Add selected tag IDs to the existing `DesignSumbission` contract and pass them
  explicitly through design create/update service and repository operations.
- Load assigned tags when retrieving a design for editing.
- In each write operation, resolve existing tags within that operation’s
  context. For updates, load the existing relationships and reconcile them with
  the submitted selection.
- Save design fields and assignments together. An empty selection clears
  assignments; repeated IDs are treated once.
- Reject unknown or deleted tag IDs with an actionable error rather than
  silently dropping them.
- Preserve the existing image upload and rollback behaviour.

**Acceptance:** Create and update support zero, one or several tags; removing an
assignment preserves the shared tag; a rejected save leaves database state
unchanged.

**First learning hint:** The form’s objects arrive from a disposed read context.
Think about why attaching that whole object graph could accidentally create
tags.

**Verification:** Repository tests for assignment reconciliation and atomic
failure; service tests confirming tag IDs reach persistence and image cleanup
still works after a failed save.

### Story 6: Select tags in the design form

**As an admin, I want an optional multiple-selection control when adding or
editing a design.**

Implementation:

- Have add/edit pages load the catalogue through `ITagService` and supply it to
  `DesignInput`.
- Use a MudBlazor multi-select bound to tag IDs, with alphabetical options and
  existing assignments selected.
- Keep selection local until submission and disable it during saving.
- Show an empty-catalogue hint. If catalogue loading fails, offer retry and
  block saving to avoid accidentally clearing assignments.
- Preserve entered values after save failures.

**Acceptance:** Both forms save without tags, restore existing selections and
allow removing all tags. The form offers no tag-creation action.

**First learning hint:** Separate the available choices from the selected IDs;
they represent different information.

**Verification:** Component tests for initial selections, submission, empty
catalogue, load failure and retained selection after save failure.

## Stage 4 — Filter the homepage

### Story 7: Query paginated designs by tag

**As a visitor, I want filtering to search the whole catalogue so that matching
designs appear across accurate pages.**

Implementation:

- Retain existing unfiltered pagination methods and add overloads accepting a
  nullable tag ID to the design service and repository.
- Apply the tag predicate before both counting and pagination.
- Preserve configured page size and existing newest-first ordering.
- A null tag ID means all designs; an unknown/deleted ID returns no matches.
- Keep design management using its existing unfiltered query.

**Acceptance:** Counts and page totals describe matching designs; designs appear
once; untagged designs appear under “All tags.”

**First learning hint:** Filtering after `Skip` and `Take` only searches the
current page. Identify where the predicate belongs instead.

**Verification:** Repository tests using enough mixed-tag designs to span pages,
plus empty results and unfiltered regression coverage.

### Story 8: Add the homepage tag filter

**As a visitor, I want to choose one tag or clear the filter so that I can browse
relevant designs.**

Implementation:

- Add a single-select “Tag” control to `DesignsList`, with an “All tags” default
  and alphabetical options.
- Reset to page 1 whenever selection changes; retain the selected tag during
  pagination.
- Show a clear no-matches message.
- Disable selection and pagination during requests to prevent overlapping
  loads.
- If loading tags fails, keep unfiltered browsing available and show a retry
  option.

**Acceptance:** Selecting, changing and clearing a tag produces correct results
and pagination. An empty catalogue still permits normal browsing.

**First learning hint:** Treat the selected tag and page number as one query
state; changing the tag invalidates the previous page.

**Verification:** Component tests for selection, page reset, pagination,
clearing, empty results and load failures.

## Stage 5 — Verify and document

### Story 9: Validate the complete workflow

**As the maintainer, I want confidence that the feature preserves existing
behaviour and works with PostgreSQL.**

- Run `dotnet build TattooFlash.slnx` and `dotnet test TattooFlash.slnx`.
- Use existing SQLite repository, xUnit/Moq service and bUnit component
  conventions. Add tests alongside each story.
- On a configured development database, verify the migration preserves existing
  designs and PostgreSQL enforces uniqueness and relationship deletion
  correctly.
- Manually create tags, assign them, filter across pages, rename a tag and
  delete an assigned tag.
- Check signed-out management route protection through routing, plus
  loading/error states, keyboard interaction and mobile layout.
- Update the tag proposal to describe implemented behaviour and deferred
  inline creation; update repository guidance where structure changes.

**Done:** The complete workflow passes, existing design/image behaviour remains
intact, and any blocked checks are documented.

## Learning checkpoints and defaults

For each story: discuss the concept and first hint, let the owner implement it,
review the changes, then guide the owner through meaningful verification.
Provide progressively fuller guidance when requested.

Defaults: flat tags; `Guid` identifiers; no new dependencies; no seeded tags;
no tag hierarchy, public tag badges, URL-persisted filters or design-management
filtering in this iteration. Apply migrations only to an authorised, known
target.

Reference: [Design tags with EF Core](design-tags-ef-core.md). That document is
an earlier proposal; use the agreed behaviour in this plan where they differ.
