# AcxiomCRM — Data Model

Authoritative schema. EF configurations and migrations must match this. Conventions: SQL Server 2022, `nvarchar` for text, `datetime2(0)` for instants (UTC), `date` for business dates, `decimal(18,2)` for money, `rowversion` named `RowVersion` where listed. Unless stated, enums are stored as `tinyint` with a `CHECK` limiting values to the defined set.

## 1. Limits (single source: `Domain/Validation/Limits.cs`)

| Constant | Value | Used for |
|---|---|---|
| `Name` | 150 | person/company/customer/lead/opportunity names |
| `Email` | 254 | email |
| `Phone` | 10 | normalised digits |
| `Address` | 300 | address line |
| `City`, `State` | 100 | |
| `Notes` | 2000 | notes/description/remarks |
| `Subject` | 200 | follow-up/activity subject |
| `Code` | 20 | `CUS-000001` |
| `CloseReason` | 500 | win note / loss reason |
| `MaxMoney` | 100,000,000 | lead expected value cap; opportunity amount cap is 1,000,000,000 |

## 2. Identity (framework-managed)

`AspNetUsers` extended by `ApplicationUser`:

| Column | Type | Notes |
|---|---|---|
| (Identity defaults) | | `Id` nvarchar(450) GUID string, `UserName`, `NormalizedUserName`, `Email`, `PasswordHash`, `LockoutEnd`, `AccessFailedCount`, `SecurityStamp`, … |
| `FullName` | nvarchar(150) NOT NULL | |
| `IsActive` | bit NOT NULL default 1 | inactive cannot sign in |
| `ManagerId` | nvarchar(450) NULL FK → AspNetUsers(Id) | only meaningful for SalesExecutive. Self-reference not allowed (CHECK `ManagerId <> Id`) |
| `MustChangePassword` | bit NOT NULL default 0 | |
| `CreatedDate` | datetime2(0) NOT NULL | |
| `LastLoginDate` | datetime2(0) NULL | |

Roles: `Admin`, `Manager`, `SalesExecutive` (seeded, fixed). Identity's lockout (`LockoutEnabled=1` for all users) provides the failed count/lock end required by Source §9 (`FailedLoginCount`, `LockoutEnd`). The spec's separate password table is **not** created (Source §17.4).

`UserSummary` (keyless projection, `Application`): `Id, FullName, UserName, Email, Role, IsActive, ManagerId, LockedOut, LockoutEnd, MustChangePassword, CreatedDate, LastLoginDate`. Mapped as a query type over a view `vw_UserSummary` (Identity users joined to their single role). It contains no hash/stamp columns.

## 3. CRM tables

### Customers
| Column | Type | Constraints |
|---|---|---|
| `CustomerId` | int identity PK | |
| `CustomerCode` | nvarchar(20) NOT NULL | unique. `CUS-` + 6-digit zero-padded id, assigned after insert via sequence `seq_CustomerCode` |
| `CustomerName` | nvarchar(150) NOT NULL | |
| `CompanyName` | nvarchar(150) NULL | |
| `Email` | nvarchar(254) NOT NULL | stored as entered (trimmed) |
| `EmailNormalized` | nvarchar(254) NOT NULL | lower-invariant |
| `Phone` | nvarchar(10) NOT NULL | 10-digit normalised; CHECK `Phone LIKE '[6-9]%'` and length 10 |
| `Address` | nvarchar(300) NULL | |
| `City`, `State` | nvarchar(100) NULL | |
| `Status` | tinyint NOT NULL | 1 Active, 2 Inactive |
| `OwnerId` | nvarchar(450) NOT NULL FK → AspNetUsers | assigned sales executive |
| `Notes` | nvarchar(2000) NULL | |
| `CreatedDate/CreatedBy`, `ModifiedDate/ModifiedBy` | datetime2(0)/nvarchar(450) | |
| `IsDeleted` bit, `DeletedDate`, `DeletedBy` | | soft delete |
| `RowVersion` | rowversion | |

Indexes: `UX_Customers_EmailNormalized` unique filtered `WHERE IsDeleted = 0`; `UX_Customers_Phone` unique filtered `WHERE IsDeleted = 0`; `IX_Customers_OwnerId_Status` (include CustomerName, CompanyName); `IX_Customers_CustomerName`.

### Leads
| Column | Type | Constraints |
|---|---|---|
| `LeadId` | int identity PK | |
| `LeadCode` | nvarchar(20) NOT NULL unique | `LEAD-000001` |
| `LeadName` | nvarchar(150) NOT NULL | |
| `CompanyName` | nvarchar(150) NULL | |
| `Email` | nvarchar(254) NULL | |
| `Phone` | nvarchar(10) NULL | CHECK at least one of Email/Phone non-null |
| `Source` | tinyint NOT NULL | 1 Website, 2 Referral, 3 ColdCall, 4 Campaign, 5 Event, 6 SocialMedia, 7 Other |
| `Status` | tinyint NOT NULL | 1 New, 2 Contacted, 3 Qualified, 4 Unqualified, 5 Converted, 6 Lost |
| `Priority` | tinyint NOT NULL | 1 Low, 2 Medium, 3 High |
| `ExpectedValue` | decimal(18,2) NOT NULL default 0 | CHECK 0 ≤ v ≤ 100000000 |
| `OwnerId` | nvarchar(450) NOT NULL FK | `AssignedTo` in Source |
| `Notes` | nvarchar(2000) NULL | |
| `ConvertedCustomerId` | int NULL FK → Customers | |
| `ConvertedOpportunityId` | int NULL FK → Opportunities | |
| `ConvertedDate` | datetime2(0) NULL | CHECK (Status = 5) = (ConvertedCustomerId IS NOT NULL) |
| audit/soft-delete/RowVersion | as Customers | |

Indexes: `IX_Leads_OwnerId_Status`, `IX_Leads_Status_CreatedDate`, `IX_Leads_LeadName`.

### Opportunities
| Column | Type | Constraints |
|---|---|---|
| `OpportunityId` | int identity PK | |
| `OpportunityName` | nvarchar(150) NOT NULL | |
| `CustomerId` | int NOT NULL FK → Customers | |
| `LeadId` | int NULL FK → Leads | source lead |
| `Amount` | decimal(18,2) NOT NULL | CHECK `Amount >= 0` and `<= 1000000000`. Positive-while-open is enforced by CHECK `(Status <> 1 OR Amount > 0)` |
| `Stage` | tinyint NOT NULL | 1 Qualification, 2 Proposal, 3 Negotiation, 4 Won, 5 Lost |
| `Status` | tinyint NOT NULL | 1 Open, 2 Won, 3 Lost (derived, D-16). CHECK consistent with Stage |
| `Probability` | tinyint NOT NULL | CHECK 0–100 |
| `ExpectedCloseDate` | date NOT NULL | |
| `ClosedDate` | date NULL | set when Won/Lost. CHECK `(Status = 1) = (ClosedDate IS NULL)` |
| `CloseNote` | nvarchar(500) NULL | required when closing |
| `Source` | tinyint NULL | reuse lead source set |
| `OwnerId` | nvarchar(450) NOT NULL FK | `AssignedTo` |
| `Notes` | nvarchar(2000) NULL | |
| audit/soft-delete/RowVersion | | |

The past-close-date rule is **not** a DB CHECK (it depends on "today"). It is domain-only, tested at boundaries. Indexes: `IX_Opps_OwnerId_Status`, `IX_Opps_CustomerId`, `IX_Opps_Stage_ExpectedCloseDate`, `IX_Opps_Status_ClosedDate`.

### FollowUps
| Column | Type | Constraints |
|---|---|---|
| `FollowUpId` | int identity PK | |
| `CustomerId`, `LeadId`, `OpportunityId` | int NULL FKs | CHECK exactly one non-null (D-05) |
| `FollowUpDate` | datetime2(0) NOT NULL | UTC instant |
| `Subject` | nvarchar(200) NOT NULL | |
| `FollowUpType` | tinyint NOT NULL | 1 Call, 2 Meeting, 3 Email, 4 Visit, 5 Other |
| `Status` | tinyint NOT NULL | 1 Planned, 2 Completed, 3 Missed, 4 Cancelled |
| `Remarks` | nvarchar(2000) NULL | notes |
| `AssignedToId` | nvarchar(450) NOT NULL FK | |
| `CompletedDate` | datetime2(0) NULL | |
| `RescheduleCount` | int NOT NULL default 0 | |
| `IsHistoricalEntry` | bit NOT NULL default 0 | D-20 |
| audit/soft-delete/RowVersion | | |

Indexes: `IX_FollowUps_AssignedToId_Status_Date`, `IX_FollowUps_CustomerId`, `_LeadId`, `_OpportunityId` (filtered NOT NULL).

### Activities
| Column | Type | Constraints |
|---|---|---|
| `ActivityId` | int identity PK | |
| `ActivityType` | tinyint NOT NULL | 1 Call, 2 Meeting, 3 Email, 4 Task |
| `Subject` | nvarchar(200) NOT NULL | |
| `Description` | nvarchar(2000) NULL | |
| `ActivityDate` | datetime2(0) NOT NULL | UTC |
| `CustomerId`, `LeadId`, `OpportunityId` | int NULL FKs | CHECK at least one non-null |
| `AssignedToId` | nvarchar(450) NOT NULL FK | |
| `Status` | tinyint NOT NULL | 1 Planned, 2 Completed, 3 Cancelled |
| audit/soft-delete/RowVersion | | |

### AuditLogs (append-only)
| Column | Type | Notes |
|---|---|---|
| `AuditLogId` | bigint identity PK | |
| `CreatedDate` | datetime2(3) NOT NULL | UTC |
| `UserId` | nvarchar(450) NULL | null for anonymous failed logins |
| `UserName` | nvarchar(256) NULL | snapshot, so history survives renames |
| `Module` | nvarchar(50) NOT NULL | Authentication, Customers, Leads, Opportunities, FollowUps, Activities, Users, Security |
| `Action` | nvarchar(50) NOT NULL | see `AuditActions` constants |
| `EntityName` | nvarchar(100) NOT NULL | |
| `RecordId` | nvarchar(64) NULL | |
| `Result` | tinyint NOT NULL | 1 Success, 2 Failure |
| `OldValue`, `NewValue` | nvarchar(max) NULL | JSON of changed properties only. Sensitive properties excluded |
| `Details` | nvarchar(1000) NULL | |
| `IpAddress` | nvarchar(45) NULL | |
| `CorrelationId` | nvarchar(64) NULL | |

Indexes: `IX_Audit_CreatedDate`, `IX_Audit_UserId_CreatedDate`, `IX_Audit_Module_Action_CreatedDate`, `IX_Audit_EntityName_RecordId`. Immutability per PRD D-25: trigger `TR_AuditLogs_BlockUpdateDelete` raising error 51000, plus the interceptor.

`AuditActions` (strings): `Login`, `LoginFailed`, `Lockout`, `Unlock`, `Logout`, `Register`, `PasswordChanged`, `PasswordReset`, `Create`, `Update`, `Delete`, `RoleChanged`, `StatusChanged`, `StageChanged`, `Assigned`, `Converted`, `FollowUpCompleted`, `FollowUpRescheduled`, `FollowUpMissed`, `Activated`, `Deactivated`.

### RefreshTokens
`RefreshTokenId` bigint PK, `UserId` FK, `TokenHash` char(64) (SHA-256 hex) unique, `CreatedDate`, `ExpiresDate`, `RevokedDate` NULL, `ReplacedByTokenHash` NULL, `CreatedByIp`. Raw token is never stored.

## 4. Relationships summary
Customer 1—* Opportunity, Customer 1—* FollowUp/Activity; Lead 1—0..1 converted Customer/Opportunity; Opportunity 1—* FollowUp/Activity; User 1—* owned records; User(Manager) 1—* User.

FK delete behaviour: `Restrict` everywhere (soft delete makes cascades unnecessary and dangerous).

## 5. Seed data
- Roles (always). Bootstrap admin (config, see ARCHITECTURE §7).
- Demo dataset (`Database:SeedDemoData=true`, dev only, deterministic seed 42): 1 admin, 2 managers, 6 sales executives (3 per manager), 60 customers, 120 leads across all statuses/sources, 90 opportunities across stages with realistic INR amounts, 150 follow-ups (past-due, today, future), 100 activities, and a trail of audit events. All data is plausible Indian business data, never lorem ipsum or "Test User 1". Demo credentials are printed once to the console in dev and never committed.
