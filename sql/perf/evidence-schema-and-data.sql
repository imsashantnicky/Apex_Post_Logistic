-- Prints the schema and the row counts of one database, for docs/evidence. It only reads.
SET NOCOUNT ON;

PRINT '-- Migrations applied';
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

PRINT '-- Tables with rows, primary key and size';
SELECT t.name AS TableName,
       SUM(CASE WHEN i.index_id < 2 THEN p.rows ELSE 0 END) AS RowsStored,
       (SELECT STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY k.key_ordinal)
        FROM sys.indexes AS pk
        JOIN sys.index_columns AS k ON k.object_id = pk.object_id AND k.index_id = pk.index_id
        JOIN sys.columns AS c ON c.object_id = k.object_id AND c.column_id = k.column_id
        WHERE pk.object_id = t.object_id AND pk.is_primary_key = 1) AS PrimaryKey
FROM sys.tables AS t
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.partitions AS p ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE t.name <> '__EFMigrationsHistory'
GROUP BY t.object_id, t.name
ORDER BY t.name;

PRINT '-- Columns';
SELECT TABLE_NAME AS TableName,
       COLUMN_NAME AS ColumnName,
       DATA_TYPE + CASE
           WHEN DATA_TYPE = 'decimal' THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
           WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN '(max)'
           WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
           ELSE '' END AS ColumnType,
       IS_NULLABLE AS Nullable,
       ISNULL(COLUMN_DEFAULT, '-') AS DefaultValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME <> '__EFMigrationsHistory'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

PRINT '-- Foreign keys';
SELECT OBJECT_NAME(parent_object_id) AS Child,
       OBJECT_NAME(referenced_object_id) AS Parent,
       name AS ForeignKey,
       delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys
ORDER BY Child, ForeignKey;

PRINT '-- CHECK constraints';
SELECT OBJECT_NAME(parent_object_id) AS TableName, name AS ConstraintName, definition AS Definition
FROM sys.check_constraints
ORDER BY TableName, ConstraintName;

PRINT '-- Indexes outside primary keys';
SELECT OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName,
       i.is_unique AS IsUnique,
       (SELECT STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY k.key_ordinal)
        FROM sys.index_columns AS k
        JOIN sys.columns AS c ON c.object_id = k.object_id AND c.column_id = k.column_id
        WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.is_included_column = 0) AS KeyColumns,
       ISNULL((SELECT STRING_AGG(c.name, ', ')
               FROM sys.index_columns AS k
               JOIN sys.columns AS c ON c.object_id = k.object_id AND c.column_id = k.column_id
               WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.is_included_column = 1), '-')
           AS IncludedColumns
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
WHERE i.is_primary_key = 0 AND i.index_id > 0 AND t.name <> '__EFMigrationsHistory'
ORDER BY TableName, IndexName;

PRINT '-- Stored procedures';
SELECT name AS ProcedureName, create_date AS Created FROM sys.procedures ORDER BY name;
