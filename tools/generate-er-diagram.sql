-- Emits a Mermaid ER diagram from the live schema, so the diagram cannot drift
-- from the migrations. Run through sqlcmd and paste into docs/er-diagram.md.
SET NOCOUNT ON;

-- Identity plumbing adds six tables that say nothing about the domain. AspNetUsers
-- is the only one listings and bookings actually point at, so the rest are hidden.
DECLARE @skip TABLE (name sysname);
INSERT INTO @skip VALUES
    ('AspNetRoles'), ('AspNetRoleClaims'), ('AspNetUserClaims'),
    ('AspNetUserLogins'), ('AspNetUserRoles'), ('AspNetUserTokens'),
    ('__EFMigrationsHistory');

WITH pk AS (
    SELECT ic.object_id, ic.column_id
    FROM sys.index_columns ic
    JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    WHERE i.is_primary_key = 1
),
fk AS (
    SELECT DISTINCT parent_object_id, parent_column_id FROM sys.foreign_key_columns
),
cols AS (
    SELECT t.name AS tbl, c.column_id,
           '        ' + ty.name + ' ' + c.name +
           CASE WHEN pk.column_id IS NOT NULL THEN ' PK'
                WHEN fk.parent_column_id IS NOT NULL THEN ' FK'
                ELSE '' END AS line
    FROM sys.tables t
    JOIN sys.columns c ON c.object_id = t.object_id
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
    LEFT JOIN fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
    WHERE t.name NOT IN (SELECT name FROM @skip)
),
blocks AS (
    SELECT tbl,
           '    ' + tbl + ' {' + CHAR(10)
           + STRING_AGG(line, CHAR(10)) WITHIN GROUP (ORDER BY column_id)
           + CHAR(10) + '    }' AS block
    FROM cols
    GROUP BY tbl
),
rels AS (
    SELECT '    ' + referenced.name + ' ||--o{ ' + parent.name
           + ' : "' + REPLACE(f.name, 'FK_', '') + '"' AS line
    FROM sys.foreign_keys f
    JOIN sys.tables parent ON parent.object_id = f.parent_object_id
    JOIN sys.tables referenced ON referenced.object_id = f.referenced_object_id
    WHERE parent.name NOT IN (SELECT name FROM @skip)
      AND referenced.name NOT IN (SELECT name FROM @skip)
)
SELECT 'erDiagram' + CHAR(10)
       + (SELECT STRING_AGG(block, CHAR(10)) WITHIN GROUP (ORDER BY tbl) FROM blocks)
       + CHAR(10)
       + (SELECT STRING_AGG(line, CHAR(10)) WITHIN GROUP (ORDER BY line) FROM rels);
