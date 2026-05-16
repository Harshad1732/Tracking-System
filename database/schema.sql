-- =============================================================================
-- Glass Sheet Tracking System — Full Schema (SQL Server)
-- =============================================================================
-- The Tracker app currently runs on SQLite via EF Core EnsureCreated() so the
-- schema is auto-generated at startup; you do NOT need this script to run the
-- project. Use this when you migrate to SQL Server (or want documentation
-- of every table, FK and index).
-- =============================================================================

-- Optional: create and use a fresh database.
-- CREATE DATABASE TrackerDb;
-- GO
-- USE TrackerDb;
-- GO

-- =====================================================================
-- 1. SECURITY / TENANT TABLES
-- =====================================================================

CREATE TABLE Tenants (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name            NVARCHAR(120)    NOT NULL,
    Slug            NVARCHAR(60)     NOT NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Tenants_Slug ON Tenants(Slug);


CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Email           NVARCHAR(256)    NOT NULL,
    FullName        NVARCHAR(120)    NULL,
    PasswordHash    NVARCHAR(256)    NULL,
    Role            NVARCHAR(40)     NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'User',
    Provider        NVARCHAR(40)     NULL,
    ProviderUserId  NVARCHAR(256)    NULL,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_Users_TenantId_Email ON Users(TenantId, Email);
CREATE INDEX IX_Users_Tenant_Provider ON Users(TenantId, Provider, ProviderUserId);


CREATE TABLE RefreshTokens (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId              UNIQUEIDENTIFIER NOT NULL,
    TokenHash           NVARCHAR(128)    NOT NULL,
    ExpiresAtUtc        DATETIME2        NOT NULL,
    CreatedAtUtc        DATETIME2        NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
    RevokedAtUtc        DATETIME2        NULL,
    ReplacedByTokenId   UNIQUEIDENTIFIER NULL,

    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON RefreshTokens(TokenHash);


CREATE TABLE PasswordResetTokens (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TokenHash       NVARCHAR(128)    NOT NULL,
    ExpiresAtUtc    DATETIME2        NOT NULL,
    UsedAtUtc       DATETIME2        NULL,

    CONSTRAINT FK_PwdResetTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_PwdResetTokens_TokenHash ON PasswordResetTokens(TokenHash);


-- =====================================================================
-- 2. MASTER TABLES
-- =====================================================================

CREATE TABLE Plants (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Plants PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(120)    NOT NULL,
    Address         NVARCHAR(250)    NULL,
    Phone           NVARCHAR(30)     NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Plants_IsActive DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Plants_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Plants_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_Plants_TenantId_Code ON Plants(TenantId, Code);


CREATE TABLE Processes (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Processes PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    PlantId         UNIQUEIDENTIFIER NOT NULL,
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(120)    NOT NULL,
    SequenceNo      INT              NOT NULL CONSTRAINT DF_Processes_Seq DEFAULT 0,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Processes_IsActive DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Processes_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Processes_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Processes_Plants  FOREIGN KEY (PlantId)  REFERENCES Plants(Id)  ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_Processes_TenantId_Code ON Processes(TenantId, Code);


CREATE TABLE Employees (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    PlantId         UNIQUEIDENTIFIER NULL,
    ProcessId       UNIQUEIDENTIFIER NULL,
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(120)    NOT NULL,
    Mobile          NVARCHAR(30)     NULL,
    Department      NVARCHAR(60)     NULL,
    Designation     NVARCHAR(60)     NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Employees_Tenants   FOREIGN KEY (TenantId)  REFERENCES Tenants(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_Employees_Plants    FOREIGN KEY (PlantId)   REFERENCES Plants(Id)    ON DELETE SET NULL,
    CONSTRAINT FK_Employees_Processes FOREIGN KEY (ProcessId) REFERENCES Processes(Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX UX_Employees_TenantId_Code ON Employees(TenantId, Code);


CREATE TABLE Customers (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(120)    NOT NULL,
    ContactPerson   NVARCHAR(120)    NULL,
    Mobile          NVARCHAR(30)     NULL,
    Email           NVARCHAR(150)    NULL,
    Address         NVARCHAR(250)    NULL,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Customers_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_Customers_TenantId_Code ON Customers(TenantId, Code);


CREATE TABLE RoleDefinitions (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RoleDefinitions PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Name            NVARCHAR(60)     NOT NULL,
    Description     NVARCHAR(250)    NULL,
    CanView         BIT              NOT NULL CONSTRAINT DF_Roles_CanView         DEFAULT 1,
    CanAdd          BIT              NOT NULL CONSTRAINT DF_Roles_CanAdd          DEFAULT 0,
    CanEdit         BIT              NOT NULL CONSTRAINT DF_Roles_CanEdit         DEFAULT 0,
    CanDelete       BIT              NOT NULL CONSTRAINT DF_Roles_CanDelete       DEFAULT 0,
    CanViewReports  BIT              NOT NULL CONSTRAINT DF_Roles_CanViewReports  DEFAULT 0,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Roles_IsActive        DEFAULT 1,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Roles_CreatedAt       DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_RoleDefinitions_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_RoleDefinitions_TenantId_Name ON RoleDefinitions(TenantId, Name);


-- =====================================================================
-- 3. TRACKING TABLES
-- =====================================================================

CREATE TABLE Shopfloors (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Shopfloors PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    Code            NVARCHAR(20)     NOT NULL,
    Name            NVARCHAR(80)     NOT NULL,
    SequenceNo      INT              NOT NULL CONSTRAINT DF_Shopfloors_Seq DEFAULT 0,
    IsStorage       BIT              NOT NULL CONSTRAINT DF_Shopfloors_IsStorage DEFAULT 0,
    IsActive        BIT              NOT NULL CONSTRAINT DF_Shopfloors_IsActive  DEFAULT 1,
    ProcessId       UNIQUEIDENTIFIER NULL,
    CreatedAtUtc    DATETIME2        NOT NULL CONSTRAINT DF_Shopfloors_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Shopfloors_Tenants   FOREIGN KEY (TenantId)  REFERENCES Tenants(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_Shopfloors_Processes FOREIGN KEY (ProcessId) REFERENCES Processes(Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX UX_Shopfloors_TenantId_Code ON Shopfloors(TenantId, Code);


CREATE TABLE GlassSheets (
    Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GlassSheets PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    SheetNo              NVARCHAR(60)     NOT NULL,
    OrderNo              NVARCHAR(80)     NULL,
    CustomerId           UNIQUEIDENTIFIER NULL,
    GlassType            NVARCHAR(60)     NULL,
    Thickness            DECIMAL(10,2)    NULL,
    Width                DECIMAL(10,2)    NULL,
    Height               DECIMAL(10,2)    NULL,
    Quantity             INT              NOT NULL CONSTRAINT DF_GlassSheets_Qty DEFAULT 1,
    Status               NVARCHAR(30)     NOT NULL CONSTRAINT DF_GlassSheets_Status DEFAULT 'Pending',
    CurrentShopfloorId   UNIQUEIDENTIFIER NOT NULL,
    Remarks              NVARCHAR(250)    NULL,
    EntryAtUtc           DATETIME2        NOT NULL CONSTRAINT DF_GlassSheets_EntryAt DEFAULT SYSUTCDATETIME(),
    LastMovedAtUtc       DATETIME2        NOT NULL CONSTRAINT DF_GlassSheets_LastMoved DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_GlassSheets_Tenants    FOREIGN KEY (TenantId)           REFERENCES Tenants(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_GlassSheets_Customers  FOREIGN KEY (CustomerId)         REFERENCES Customers(Id)  ON DELETE SET NULL,
    CONSTRAINT FK_GlassSheets_Shopfloors FOREIGN KEY (CurrentShopfloorId) REFERENCES Shopfloors(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_GlassSheets_TenantId_SheetNo ON GlassSheets(TenantId, SheetNo);
CREATE INDEX IX_GlassSheets_CurrentShopfloorId ON GlassSheets(CurrentShopfloorId);


CREATE TABLE SheetMovements (
    Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SheetMovements PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    GlassSheetId        UNIQUEIDENTIFIER NOT NULL,
    FromShopfloorId     UNIQUEIDENTIFIER NULL,
    ToShopfloorId       UNIQUEIDENTIFIER NOT NULL,
    MovedByUserId       UNIQUEIDENTIFIER NULL,
    Remarks             NVARCHAR(250)    NULL,
    Status              NVARCHAR(30)     NULL,
    MovedAtUtc          DATETIME2        NOT NULL CONSTRAINT DF_SheetMovements_MovedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_SheetMovements_Tenants    FOREIGN KEY (TenantId)        REFERENCES Tenants(Id)     ON DELETE CASCADE,
    CONSTRAINT FK_SheetMovements_Sheets     FOREIGN KEY (GlassSheetId)    REFERENCES GlassSheets(Id) ON DELETE CASCADE,
    CONSTRAINT FK_SheetMovements_From       FOREIGN KEY (FromShopfloorId) REFERENCES Shopfloors(Id)  ON DELETE SET NULL,
    CONSTRAINT FK_SheetMovements_To         FOREIGN KEY (ToShopfloorId)   REFERENCES Shopfloors(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_SheetMovements_User       FOREIGN KEY (MovedByUserId)   REFERENCES Users(Id)       ON DELETE SET NULL
);
CREATE INDEX IX_SheetMovements_GlassSheetId ON SheetMovements(GlassSheetId);


-- =====================================================================
-- 4. SEED DATA (Demo Workspace)
--    The application's startup also seeds the admin user automatically;
--    this seed sets up the tenant + shopfloors + sample customers so the
--    Excel import file's customer names match the master.
-- =====================================================================

DECLARE @TenantId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

INSERT INTO Tenants (Id, Name, Slug, IsActive)
VALUES (@TenantId, 'Demo Workspace', 'demo', 1);

-- Shopfloors (Storage + SF1..SF4 with sample processes)
INSERT INTO Shopfloors (Id, TenantId, Code, Name, SequenceNo, IsStorage, IsActive) VALUES
(NEWID(), @TenantId, 'STORAGE', 'Storage',     0,  1, 1),
(NEWID(), @TenantId, 'SF1',     'Cutting',     10, 0, 1),
(NEWID(), @TenantId, 'SF2',     'Edging',      20, 0, 1),
(NEWID(), @TenantId, 'SF3',     'Marking',     30, 0, 1),
(NEWID(), @TenantId, 'SF4',     'Blackborder', 40, 0, 1);

-- Customers — names match the Excel sample data
INSERT INTO Customers (Id, TenantId, Code, Name, ContactPerson, Mobile, Email, IsActive) VALUES
(NEWID(), @TenantId, 'CUS-001', 'ABC Builders',         'Ramesh Shah',     '+91 98200 11111', 'ramesh@abc.example',     1),
(NEWID(), @TenantId, 'CUS-002', 'XYZ Contractors',      'Suresh Iyer',     '+91 98200 22222', 'suresh@xyz.example',     1),
(NEWID(), @TenantId, 'CUS-003', 'Metro Glass Works',    'Priya Menon',     '+91 98200 33333', 'priya@metroglass.example',1),
(NEWID(), @TenantId, 'CUS-004', 'Skyline Architects',   'Anil Kumar',      '+91 98200 44444', 'anil@skyline.example',   1),
(NEWID(), @TenantId, 'CUS-005', 'Heritage Interiors',   'Neha Verma',      '+91 98200 55555', 'neha@heritage.example',  1),
(NEWID(), @TenantId, 'CUS-006', 'Aurora Construction',  'Vikram Singh',    '+91 98200 66666', 'vikram@aurora.example',  1),
(NEWID(), @TenantId, 'CUS-007', 'Bluegate Realty',      'Pooja Rao',       '+91 98200 77777', 'pooja@bluegate.example', 1),
(NEWID(), @TenantId, 'CUS-008', 'Crystal Facades',      'Arjun Pillai',    '+91 98200 88888', 'arjun@crystal.example',  1),
(NEWID(), @TenantId, 'CUS-009', 'Delta Build',          'Sneha Reddy',     '+91 98200 99999', 'sneha@delta.example',    1),
(NEWID(), @TenantId, 'CUS-010', 'Evergreen Homes',      'Manish Joshi',    '+91 98200 10101', 'manish@evergreen.example',1);

-- Plant + processes (optional — only needed if you want to attach a Process to a Shopfloor)
DECLARE @PlantId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Plants (Id, TenantId, Code, Name, Address, Phone)
VALUES (@PlantId, @TenantId, 'PLT-01', 'Main Plant', 'MIDC Bhosari, Pune', '+91 98765 43210');

INSERT INTO Processes (Id, TenantId, PlantId, Code, Name, SequenceNo, IsActive) VALUES
(NEWID(), @TenantId, @PlantId, 'P-CUT', 'Cutting',     10, 1),
(NEWID(), @TenantId, @PlantId, 'P-EDG', 'Edging',      20, 1),
(NEWID(), @TenantId, @PlantId, 'P-MRK', 'Marking',     30, 1),
(NEWID(), @TenantId, @PlantId, 'P-BLK', 'Blackborder', 40, 1);

-- Default roles
INSERT INTO RoleDefinitions (Id, TenantId, Name, Description, CanView, CanAdd, CanEdit, CanDelete, CanViewReports, IsActive) VALUES
(NEWID(), @TenantId, 'Admin',      'Full system access',                       1, 1, 1, 1, 1, 1),
(NEWID(), @TenantId, 'Supervisor', 'Approve & monitor production',             1, 1, 1, 0, 1, 1),
(NEWID(), @TenantId, 'Operator',   'Update sheet locations and statuses',      1, 0, 1, 0, 0, 1),
(NEWID(), @TenantId, 'Viewer',     'Read-only access to dashboards & reports', 1, 0, 0, 0, 1, 1);

-- =====================================================================
-- NOTE on admin user:
-- The Tracker backend runs SeedAdminAsync on startup and will insert the
-- admin user (admin@tracker.local / Admin#12345) with a properly hashed
-- BCrypt password. Do NOT insert it here in plain text. Start the backend
-- once with this DB connected and the admin row appears automatically.
-- =====================================================================
