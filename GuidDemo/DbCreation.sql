-- Use the same demo DB
IF DB_ID('GuidDemo2') IS NULL CREATE DATABASE GuidDemo2;
GO
USE GuidDemo2;
GO

-- Clean up existing objects (ignore errors if they don't exist)
IF OBJECT_ID('dbo.Entities_DB')    IS NOT NULL DROP TABLE dbo.Entities_DB;
IF OBJECT_ID('dbo.Addresses_DB')   IS NOT NULL DROP TABLE dbo.Addresses_DB;
IF OBJECT_ID('dbo.Entities_Det')   IS NOT NULL DROP TABLE dbo.Entities_Det;
IF OBJECT_ID('dbo.Addresses_Det')  IS NOT NULL DROP TABLE dbo.Addresses_Det;
GO

/* =========================
   Scenario A: DB-generated PKs (NEWSEQUENTIALID)
   PK is CLUSTERED on Id
   ========================= */

CREATE TABLE dbo.Addresses_DB
(
    Id UNIQUEIDENTIFIER NOT NULL 
        CONSTRAINT DF_Addresses_DB_Id DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SiteId UNIQUEIDENTIFIER NOT NULL,
    BusinessCode NVARCHAR(32) NOT NULL,
    CONSTRAINT PK_Addresses_DB PRIMARY KEY CLUSTERED (Id),               -- clustered PK
    CONSTRAINT UX_Addresses_DB UNIQUE (TenantId, SiteId, BusinessCode)
);
GO

CREATE TABLE dbo.Entities_DB
(
    Id UNIQUEIDENTIFIER NOT NULL 
        CONSTRAINT DF_Entities_DB_Id DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SiteId UNIQUEIDENTIFIER NOT NULL,
    AdminOfficeId UNIQUEIDENTIFIER NOT NULL,  -- references Addresses_DB.Id (not enforced here)
    RegOfficeId UNIQUEIDENTIFIER NOT NULL,    -- references Addresses_DB.Id (not enforced here)
    CreatedAtUtc BIGINT NOT NULL,
    CONSTRAINT PK_Entities_DB PRIMARY KEY CLUSTERED (Id)                 -- clustered PK
);
-- (No separate clustered index on CreatedAtUtc in this variant)
GO

/* =========================
   Scenario B: Deterministic PKs
   PK is CLUSTERED on Id
   ========================= */

CREATE TABLE dbo.Addresses_Det
(
    Id UNIQUEIDENTIFIER NOT NULL,             -- supplied by app (deterministic)
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SiteId UNIQUEIDENTIFIER NOT NULL,
    BusinessCode NVARCHAR(32) NOT NULL,
    CONSTRAINT PK_Addresses_Det PRIMARY KEY CLUSTERED (Id),              -- clustered PK
    CONSTRAINT UX_Addresses_Det UNIQUE (TenantId, SiteId, BusinessCode)
);
GO

CREATE TABLE dbo.Entities_Det
(
    Id UNIQUEIDENTIFIER NOT NULL,             -- supplied by app
    TenantId UNIQUEIDENTIFIER NOT NULL,
    SiteId UNIQUEIDENTIFIER NOT NULL,
    AdminOfficeId UNIQUEIDENTIFIER NOT NULL,  -- references Addresses_Det.Id (not enforced here)
    RegOfficeId UNIQUEIDENTIFIER NOT NULL,    -- references Addresses_Det.Id (not enforced here)
    CreatedAtUtc BIGINT NOT NULL,
    CONSTRAINT PK_Entities_Det PRIMARY KEY CLUSTERED (Id)                -- clustered PK
);
GO
