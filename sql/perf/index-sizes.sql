/*
    Pages and megabytes per index of the application's tables, read from the catalog.
        sqlcmd -S localhost -E -C -d ApexPostDb_Perf -W -s "|" -i sql/perf/index-sizes.sql
*/
SET NOCOUNT ON;

SELECT tableInfo.name AS TableName,
       COALESCE(indexInfo.name, 'heap') AS IndexName,
       indexInfo.type_desc AS Kind,
       stats.row_count AS RowTotal,
       stats.used_page_count AS Pages,
       CAST(stats.used_page_count * 8 / 1024.0 AS decimal(10, 2)) AS SizeMb
FROM sys.dm_db_partition_stats AS stats
JOIN sys.tables AS tableInfo ON tableInfo.object_id = stats.object_id
JOIN sys.indexes AS indexInfo ON indexInfo.object_id = stats.object_id AND indexInfo.index_id = stats.index_id
WHERE tableInfo.name <> '__EFMigrationsHistory'
  AND stats.row_count > 1000
ORDER BY tableInfo.name, indexInfo.index_id;

SELECT CAST(SUM(CASE WHEN indexInfo.index_id <= 1 THEN stats.used_page_count ELSE 0 END) * 8 / 1024.0
            AS decimal(10, 2)) AS TableMb,
       CAST(SUM(CASE WHEN indexInfo.index_id > 1 THEN stats.used_page_count ELSE 0 END) * 8 / 1024.0
            AS decimal(10, 2)) AS NonclusteredIndexMb
FROM sys.dm_db_partition_stats AS stats
JOIN sys.tables AS tableInfo ON tableInfo.object_id = stats.object_id
JOIN sys.indexes AS indexInfo ON indexInfo.object_id = stats.object_id AND indexInfo.index_id = stats.index_id
WHERE tableInfo.name <> '__EFMigrationsHistory';
