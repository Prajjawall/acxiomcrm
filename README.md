# AcxiomCRM

A modern, role-based Customer Relationship Management (CRM) platform built on **.NET 10**, **ASP.NET Core MVC**, **Entity Framework Core**, **ASP.NET Core Identity**, and **SQL Server**.

AcxiomCRM implements complete enterprise sales automation featuring strict record-level scoping, full audit logging, an interactive Chart.js analytics dashboard, parameterized reports, and dual-authenticated REST APIs.

---

## Architecture & Technology Stack

The application adheres to clean, four-layer architecture principles:

- **Domain Layer (`AcxiomCrm.Domain`)**: Core business entities, enums, canonical validation rules, and business logic without external dependencies.
- **Application Layer (`AcxiomCrm.Application`)**: Use-case orchestration, command/query contracts, role-based scoping specifications, and reporting models.
- **Infrastructure Layer (`AcxiomCrm.Infrastructure`)**: EF Core persistence, SQL Server mapping, database interceptors (append-only audit trail and soft delete), password policies, token services, and database seeding.
- **Web Presentation & API Layer (`AcxiomCrm.Web`)**: ASP.NET Core MVC controllers, Razor views, responsive layouts, client/server validation, REST API endpoints (`/api/*`), and OpenAPI (Swagger) documentation.

---

## Core Features

### 1. Identity & Role-Based Access Control
- **Three Core Roles**:
  - `Admin`: Full system visibility, user administration, security audit access, and global configuration.
  - `Manager`: Scoped oversight of assigned sales team (direct reports), team pipeline analytics, and read-only user directory.
  - `SalesExecutive`: Strict ownership isolation—only accesses records assigned directly to them.
- **Security**:
  - ASP.NET Core Identity with custom password complexity validation and deny-list checks.
  - Account lockout after 5 consecutive failed attempts.
  - Cross-Site Request Forgery (CSRF) protection with header-based antiforgery tokens.
  - No secrets or plain-text passwords stored or logged.

### 2. CRM Modules
- **Customers**: Uniqueness enforcement on email and phone numbers, sequence-generated identifiers (`CUS-000001`), soft deletion, and related record navigation.
- **Leads**: Complete lifecycle workflow (`New` -> `Contacted` -> `Qualified` -> `Converted` / `Lost`). One-click conversion to Customer and Opportunity within a single database transaction.
- **Opportunities**: Multi-stage sales pipeline (`Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`), derived status, probability scoring (0–100%), and weighted pipeline calculation (`Amount * Probability / 100`).
- **Follow-ups & Activities**: Schedule calls, meetings, emails, and visits linked to customers, leads, or opportunities. Tracks overdue items and daily tasks.
- **Audit Logging**: Append-only audit trail automatically capturing user, action, timestamp, IP address, and JSON diffs of changed properties.

### 3. Analytics & Reporting
- **Interactive Dashboard**:
  - 8 KPI cards: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Deals, Lost Deals, Total Pipeline Value.
  - 3 Chart.js visualisations: Lead Status Breakdown, Opportunity Pipeline by Stage (including weighted values), and 6-Month Won Sales Trend.
  - Role-specific panels: Admin System Health, Manager Team Pipeline, and Executive My Follow-ups.
- **Filterable Reports**:
  - Customer Report, Pipeline Report, and Follow-up Report with multi-column sorting, pagination, and date range filters.

### 4. REST API & Swagger
- RESTful endpoints for Authentication, Customers, Leads, Opportunities, Follow-ups, and Pipeline Reports.
- Dual authentication scheme accepting both JWT Bearer tokens (issued via `/api/auth/login`) and active Identity cookie sessions.
- RFC 7807 Problem Details for consistent error responses.
- Interactive Swagger UI available in Development at `/swagger`.

---

## Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/) (for SQL Server container) or local SQL Server instance

### 1. Start the Database
Launch the local SQL Server instance using Docker Compose:
```bash
docker compose up -d db
```
*(The local SQL Server runs on port `1433` with credentials configured in `docker-compose.yml`.)*

### 2. Run the Application
Start the ASP.NET Core Web application:
```bash
dotnet run --project src/AcxiomCrm.Web
```

On first startup in Development, the database automatically:
1. Applies all pending EF Core migrations (`Database:MigrateOnStartup = true`).
2. Seeds system roles (`Admin`, `Manager`, `SalesExecutive`).
3. Seeds the bootstrap administrator.
4. Generates deterministic demo accounts and sample CRM data (`Database:SeedDemoData = true`).

Open your browser and navigate to:
**https://localhost:7142** (or **http://localhost:5242**)

---

## Demo Accounts

The demo database is pre-seeded with sample users, customers, leads, opportunities, and follow-ups:

| Role | Email | Password | Scope & Responsibilities |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiomcrm.local` | `Admin#2026!Pass` | Global access, Users, Roles, Audit Logs |
| **Manager** | `rohan.mehta@acxiomcrm.local` | `Manager#2026!Pass` | Manages North Team (Asha, Vikram) |
| **Manager** | `priya.nair@acxiomcrm.local` | `Manager#2026!Pass` | Manages South Team (Neha, Arjun) |
| **SalesExecutive** | `asha.kulkarni@acxiomcrm.local` | `Exec#2026!Pass` | Enterprise Accounts (TCS, Infosys, Wipro) |
| **SalesExecutive** | `vikram.singh@acxiomcrm.local` | `Exec#2026!Pass` | Commercial Accounts (Reliance, HDFC, Airtel, L&T) |
| **SalesExecutive** | `neha.iyer@acxiomcrm.local` | `Exec#2026!Pass` | Retail & Consumer Accounts (Bajaj, Godrej, Asian Paints) |
| **SalesExecutive** | `arjun.reddy@acxiomcrm.local` | `Exec#2026!Pass` | Industrial Accounts (Sun Pharma, Adani, ITC, Mahindra) |

---

## Running Tests

Run unit, service, and controller tests:
```bash
# Run all unit tests
dotnet test tests/AcxiomCrm.Domain.Tests
dotnet test tests/AcxiomCrm.Application.Tests
dotnet test tests/AcxiomCrm.Web.Tests

# Run full test suite (requires SQL Server container for integration fixtures)
dotnet test

# Validate code formatting
dotnet format --verify-no-changes

# Verify build with zero warnings
dotnet build -warnaserror
```

---

## Verification & Acceptance

The 14-step acceptance scenario from the specification is formally tracked and verified in [docs/ACCEPTANCE-CHECKLIST.md](docs/ACCEPTANCE-CHECKLIST.md). All 14 acceptance criteria are passing.
