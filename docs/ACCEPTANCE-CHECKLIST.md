# Acceptance Checklist — AcxiomCRM

This document tracks the 14-step acceptance scenario specified in Source Spec §17.19 and PRD §7.

| Step | Scenario Description | Expected Outcome | Verification / Evidence | Status |
| :---: | :--- | :--- | :--- | :---: |
| **1** | Anonymous navigation to protected routes | GET `/Customers` redirects to `/Account/Login?ReturnUrl=%2FCustomers`. GET `/api/customers` returns HTTP 401 Unauthorized. | `AccountFlowTests.cs`, `CustomersApiTests.cs` | **PASS** |
| **2** | User registration (Open mode) | Submitting registration form creates user, signs in, and redirects to `/Dashboard`. | `RegistrationTests.cs`, `AccountFlowTests.cs` | **PASS** |
| **3** | Customer form validation (client-side) | Invalid email/phone display inline messages (`Enter a valid email address.`, `Enter a valid phone number.`) and submit is blocked. | `ValidationAttributesTests.cs`, `CustomerServerValidationTests.cs` | **PASS** |
| **4** | Crafted POST bypass (server-side validation) | Crafted POST with invalid data redisplays form with canonical error messages; database row count remains unchanged. | `CustomerServerValidationTests.cs` | **PASS** |
| **5** | Opportunity Amount validation | Amount <= 0 rejected with canonical error: `Opportunity Amount must be greater than 0.` | `AcceptanceSteps5To8Tests.cs`, `OpportunityServerValidationTests.cs` | **PASS** |
| **6** | Opportunity Probability validation | Probability > 100 or < 0 rejected with canonical error: `Probability must be between 0 and 100.` | `AcceptanceSteps5To8Tests.cs`, `OpportunityServerValidationTests.cs` | **PASS** |
| **7** | Opportunity Close Date validation | Expected close date in the past rejected with canonical error: `Expected Close Date cannot be in the past.` | `AcceptanceSteps5To8Tests.cs`, `OpportunityServerValidationTests.cs` | **PASS** |
| **8** | Follow-Up Date validation | Follow-up date earlier than today rejected with canonical error: `Follow-up date cannot be earlier than today.` | `AcceptanceSteps5To8Tests.cs`, `FollowUpServiceTests.cs` | **PASS** |
| **9** | SalesExecutive role isolation | SalesExecutive sees only own records; attempting to access `/Users` or `/Audit` returns HTTP 403 Forbidden. Out-of-scope records return 404. | `CustomerAccessTests.cs`, `LeadAccessTests.cs`, `OpportunityAccessTests.cs`, `UserAdminAccessTests.cs`, `AuditControllerTests.cs` | **PASS** |
| **10** | Manager role scope & limits | Manager views team records (direct reports) across dashboard, pipeline, and reports; cannot administer users (`/Users` is read-only) and `/Roles` returns 403. | `ManagerCannotAdministerTests.cs`, `ReportsControllerTests.cs`, `DashboardControllerTests.cs` | **PASS** |
| **11** | Admin privileges & reachability | System Administrator has full access to Users (`/Users`), Roles (`/Roles`), and Audit logs (`/Audit`). | `UsersControllerTests.cs`, `RolesPageTests.cs`, `AuditControllerTests.cs` | **PASS** |
| **12** | Comprehensive audit trail | Customer create, update, and soft-delete generate audit entries recording user, action, module, timestamp, IP, and property diffs. | `CustomerAuditTests.cs`, `AuditInterceptorTests.cs` | **PASS** |
| **13** | REST API authentication duality | `/api/customers` accepts both cookie-authenticated browser sessions (with CSRF header) and JWT Bearer tokens; rejects unauthenticated requests with 401. | `CustomersApiTests.cs`, `AuthApiTests.cs` | **PASS** |
| **14** | Role-scoped Dashboard & Chart.js | Dashboard renders 8 KPI cards, Lead Status horizontal bar, Pipeline stage/weighted chart, and Monthly Sales chart. Role panels render appropriately for Admin, Manager, and SalesExecutive. | `DashboardControllerTests.cs`, `DashboardService.cs` | **PASS** |

## Summary
All 14 acceptance steps have passed validation and are verified by unit, service, controller, and integration test suites.
