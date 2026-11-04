/* AgriConnect — a starting point for looking around the database.

    Open this in VS Code, connect with the "AgriConnect (Docker)" profile, then
    select a block and press Ctrl+Shift+E to run just that block.
*/

USE AgriConnect;   -- select this line too, or the dropdown says master


-- What is in here -----------------------------------------------------------
SELECT t.name AS TableName, p.rows AS Rows_
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
ORDER BY p.rows DESC;


-- The three modules, and how deep each category goes -------------------------
SELECT
    CASE c.ListingType WHEN 1 THEN 'Equipment' WHEN 2 THEN 'Marketplace' ELSE 'Labour' END AS Module,
    c.Name                                AS Category,
    c.[Group]                             AS GroupName,
    COUNT(l.Id)                           AS Listings,
    COUNT(DISTINCT l.OwnerId)             AS People,
    MIN(l.Price)                          AS Cheapest,
    MAX(l.Price)                          AS Dearest
FROM Categories c
LEFT JOIN Listings l ON l.CategoryId = c.Id AND l.Status <> 3   -- 3 = Removed
GROUP BY c.ListingType, c.Name, c.[Group]
ORDER BY Module, GroupName, Category;


-- Members by district --------------------------------------------------------
SELECT u.District, COUNT(*) AS Members,
       SUM(CASE WHEN l.Id IS NOT NULL THEN 1 ELSE 0 END) AS Listings
FROM AspNetUsers u
LEFT JOIN Listings l ON l.OwnerId = u.Id AND l.Status <> 3
WHERE u.District IS NOT NULL
GROUP BY u.District
ORDER BY u.District;


-- One worker's shared calendar ------------------------------------------------
-- Labour reserves the person, not the task: book any one of a worker's tasks and
-- the dates are taken across all of them. This shows that for whoever has the most.
DECLARE @worker nvarchar(450) = (
    SELECT TOP 1 OwnerId FROM Listings WHERE Type = 3 AND Status <> 3
    GROUP BY OwnerId ORDER BY COUNT(*) DESC);

SELECT u.FullName, c.Name AS Task, l.Price, l.ExperienceYears
FROM Listings l
JOIN Categories c     ON c.Id = l.CategoryId
JOIN AspNetUsers u    ON u.Id = l.OwnerId
WHERE l.OwnerId = @worker AND l.Type = 3
ORDER BY c.Name;

SELECT b.FromDate, b.ToDate, b.Status, c.Name AS BookedFor
FROM Bookings b
JOIN Listings l   ON l.Id = b.ListingId
JOIN Categories c ON c.Id = l.CategoryId
WHERE l.OwnerId = @worker AND l.Type = 3 AND b.Status IN (2, 5)   -- Approved, Completed
ORDER BY b.FromDate;


-- The integrity check ---------------------------------------------------------
-- Both of these must return zero. An approved or completed booking may never
-- overlap another on the same resource: for equipment that is the machine, for
-- labour it is the person.
SELECT 'equipment double-booked' AS Check_, COUNT(*) AS Violations
FROM Bookings a
JOIN Bookings b  ON b.Id > a.Id AND b.ListingId = a.ListingId
JOIN Listings l  ON l.Id = a.ListingId
WHERE l.Type = 1 AND a.Status IN (2,5) AND b.Status IN (2,5)
  AND a.FromDate < b.ToDate AND b.FromDate < a.ToDate
UNION ALL
SELECT 'worker double-booked', COUNT(*)
FROM Bookings a
JOIN Listings la ON la.Id = a.ListingId
JOIN Bookings b  ON b.Id > a.Id
JOIN Listings lb ON lb.Id = b.ListingId
WHERE la.Type = 3 AND lb.Type = 3 AND la.OwnerId = lb.OwnerId
  AND a.Status IN (2,5) AND b.Status IN (2,5)
  AND a.FromDate < b.ToDate AND b.FromDate < a.ToDate;


-- Schema: every table, its columns and its relationships ----------------------
SELECT OBJECT_NAME(fk.parent_object_id)    AS FromTable,
       cp.name                             AS FromColumn,
       OBJECT_NAME(fk.referenced_object_id) AS ToTable,
       fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
ORDER BY FromTable, FromColumn;


