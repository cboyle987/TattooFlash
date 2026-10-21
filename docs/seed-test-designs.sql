-- Test data from MockDesignService. Run against the intended test database
-- after applying the EF Core migrations that create public."Designs".
-- Skips existing image filenames on subsequent runs; does not update records.
-- Run one copy at a time: ImageName does not have a unique constraint.

INSERT INTO public."Designs"
    ("Id", "Name", "Price", "ImageName", "IsAvailable")
SELECT
    gen_random_uuid(),
    seed.name,
    seed.price,
    seed.image_name,
    seed.is_available
FROM (
    VALUES
        ('Bat',              250, 'Bat-flash.jpeg',     true),
        ('Cocktail',         125, 'cocktail-flash.jpeg', true),
        ('Locket',           100, 'Locket-flash.jpeg',   true),
        ('Perfume',          175, 'Perfume-Flash.jpeg',  true),
        ('Shoe',             175, 'shoe-flash.jpeg',     false),
        ('Coffee table guy', 150, 'ozzy1.jpg',           true),
        ('Sleepy lil guy',   120, 'ozzy2.jpg',           false)
) AS seed(name, price, image_name, is_available)
WHERE NOT EXISTS (
    SELECT 1
    FROM public."Designs" AS existing
    WHERE existing."ImageName" = seed.image_name
)
RETURNING *;
