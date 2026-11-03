-- NAS snapshot supplied on 2026-11-02. Apply EF migrations before running.
-- Preserves IDs, filename casing, name whitespace, and UTC timestamps.
-- Re-running updates matching IDs; unrelated rows remain. Images are not copied.
INSERT INTO public."Designs"
    ("Id", "Name", "Price", "ImageName", "IsAvailable", "Created", "Updated")
VALUES
    ('a3ad5f29-ef63-434b-86b8-d4a68a992615', 'Cocktail', 125, 'cocktail-flash.jpeg', true,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('f92a843b-6b04-4b91-9922-90d9c03382a9', 'Perfume', 175, 'Perfume-Flash.jpeg', true,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('48ad41d6-4427-4791-8697-bfe0bbfedc56', 'Sleepy lil guy', 120, 'Ozzy2.jpg', true,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('f858e885-141d-4290-aa8f-9f51d3709825', 'Coffee table guy', 150, 'Ozzy1.jpg', true,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('7a961e6e-b003-4893-bf6a-7f0865cd394d', 'Bat', 250, 'Bat-flash.jpeg', false,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('97db8740-a02c-44b8-8ff8-06d8094990ef', 'Locket', 100, 'Locket-flash.jpeg', true,
     TIMESTAMPTZ '2026-10-31 00:00:00+00', TIMESTAMPTZ '2026-10-31 00:00:00+00'),
    ('0d876d0a-aa96-4bd2-aa13-25983ae9a445', 'New Test', 123, 'fa9c68b1-7cbc-4d06-bec3-1cf14ac29d5f.webp', true,
     TIMESTAMPTZ '2026-10-31 19:07:06.243597+00', TIMESTAMPTZ '2026-10-31 19:07:06.243666+00'),
    ('04f0cbf7-6143-4e0a-aacb-ea5a2587a095', 'container test', 999, 'c45f806c-0500-4a9c-afc6-c3e8da7c4132.webp', true,
     TIMESTAMPTZ '2026-11-01 19:23:45.762749+00', TIMESTAMPTZ '2026-11-01 19:23:45.762897+00'),
    ('8bfbad27-c1d2-4e19-a015-9d806cc8872d', 'caddy test ', 123, 'cd29361c-be31-4cca-8008-a66859b680b3.webp', true,
     TIMESTAMPTZ '2026-11-01 20:12:17.204533+00', TIMESTAMPTZ '2026-11-01 20:12:17.204667+00')
ON CONFLICT ("Id") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "Price" = EXCLUDED."Price",
    "ImageName" = EXCLUDED."ImageName",
    "IsAvailable" = EXCLUDED."IsAvailable",
    "Created" = EXCLUDED."Created",
    "Updated" = EXCLUDED."Updated"
RETURNING *;
