:on error exit

IF TRY_CONVERT(uniqueidentifier, '$(MarkerId)') IS NULL
    THROW 51000, 'MarkerId must be a non-empty GUID.', 1;

IF OBJECT_ID(N'dbo.__AnonymyzerDetachedCopy', N'U') IS NOT NULL
    THROW 51001, 'Detached-copy marker table already exists.', 1;

CREATE TABLE dbo.__AnonymyzerDetachedCopy
(
    MarkerId uniqueidentifier NOT NULL PRIMARY KEY,
    DatabaseName sysname NOT NULL,
    CreatedUtc datetimeoffset NOT NULL
);

INSERT INTO dbo.__AnonymyzerDetachedCopy (MarkerId, DatabaseName, CreatedUtc)
VALUES (CONVERT(uniqueidentifier, '$(MarkerId)'), DB_NAME(), SYSUTCDATETIME());
