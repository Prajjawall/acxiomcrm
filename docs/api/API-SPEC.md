# AcxiomCRM — REST API Specification (v1)

Base path `/api`. JSON (`application/json`, camelCase, enums as strings). Dates: business dates `YYYY-MM-DD`; instants ISO-8601 UTC (`2026-10-16T09:30:00Z`). Money: JSON numbers with 2 decimals (INR).

## 1. Authentication

| Scheme | Use |
|---|---|
| `Bearer` JWT | Programmatic clients. Obtained from `POST /api/auth/login` |
| Identity cookie | Browser session of the MVC app. Unsafe methods require header `X-CSRF-TOKEN` |

A policy scheme picks Bearer when an `Authorization: Bearer` header is present, otherwise the cookie. All endpoints except `login` and `refresh` require authentication and permission `api.access` plus the module permission from SECURITY-AUTHZ.

### Endpoints
| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | Exchange credentials for tokens | 200 `AuthTokensDto` | 400, 401 (generic message), 423 (locked, with `retryAfterSeconds`), 429 |
| POST | `/api/auth/refresh` | Rotate refresh token | 200 `AuthTokensDto` | 400, 401 |
| POST | `/api/auth/logout` | Revoke the supplied refresh token (and end cookie session if any) | 204 | 401 |
| GET | `/api/auth/me` | Current user summary | 200 `CurrentUserDto` | 401 |

`LoginRequest { userNameOrEmail, password }`. `AuthTokensDto { accessToken, expiresInSeconds, refreshToken, tokenType: "Bearer", user: CurrentUserDto }`. `CurrentUserDto { id, fullName, userName, email, role, permissions[] }`.

## 2. Conventions

**Pagination**: `?page=1&pageSize=25&sort=name&desc=false`. Response `PagedDto<T> { items, page, pageSize, total }`. `pageSize` max 100, otherwise clamped.

**Errors** are `application/problem+json` (RFC 7807):
```json
{ "type": "https://acxiomcrm.local/problems/validation", "title": "One or more validation errors occurred.",
  "status": 400, "traceId": "00-…", "errors": { "amount": ["Opportunity Amount must be greater than 0."] } }
```
Field keys are camelCase. Non-validation problems have no `errors`. 500 responses return `title: "An unexpected error occurred."` and the `traceId`, never exception text.

| Status | When |
|---|---|
| 200 | Read/update ok |
| 201 | Created. `Location` header |
| 204 | Delete ok / logout |
| 400 | Validation or malformed JSON |
| 401 | Missing/invalid/expired token |
| 403 | Authenticated, permission missing |
| 404 | Not found **or out of scope** |
| 409 | Duplicate (email/phone), concurrency mismatch, invalid state transition |
| 423 | Account locked (login only) |
| 429 | Rate limited, with `Retry-After` |

**Concurrency**: `GET` returns `ETag: "<base64 RowVersion>"`. `PUT` requires `If-Match`. Missing → 428, mismatch → 409. (428 Precondition Required added for the missing header.)

**Idempotency**: not required in v1.

## 3. Resources

All DTOs omit internal fields (`IsDeleted`, `DeletedBy`, `RowVersion`, `EmailNormalized`, user hashes). Owner appears as `{ id, name }`.

### Customers (`customers.*`)
| Method | Path | Notes |
|---|---|---|
| GET | `/api/customers` | Query: `search` (name/email/phone/company), `status`, `ownerId`, `page`, `pageSize`, `sort` ∈ `name,createdDate,status`, `desc` |
| GET | `/api/customers/{id}` | `CustomerDto` + ETag |
| POST | `/api/customers` | `CreateCustomerRequest` → 201 |
| PUT | `/api/customers/{id}` | `UpdateCustomerRequest` + `If-Match` |
| DELETE | `/api/customers/{id}` | Soft delete → 204. 409 if open opportunities exist |

`CustomerDto { id, code, name, companyName, email, phone, address, city, state, status, owner, notes, createdDate, modifiedDate }`.
`CreateCustomerRequest { name*, companyName, email*, phone*, address, city, state, status, ownerId, notes }`. `ownerId` ignored for SalesExecutive (forced to self).

### Leads (`leads.*`)
| Method | Path | Notes |
|---|---|---|
| GET | `/api/leads` | `search`, `status`, `source`, `priority`, `ownerId`, paging |
| GET | `/api/leads/{id}` | |
| POST | `/api/leads` | |
| PUT | `/api/leads/{id}` | |
| DELETE | `/api/leads/{id}` | |
| POST | `/api/leads/{id}/status` | `{ status }` → validates transitions (409 on invalid) |
| POST | `/api/leads/{id}/assign` | `{ ownerId }` (`leads.assign`) |
| POST | `/api/leads/{id}/convert` | `ConvertLeadRequest { existingCustomerId?, createOpportunity, opportunity?: { name, amount, probability, expectedCloseDate } }` → 200 `ConvertLeadResultDto { leadId, customerId, opportunityId? }` |

`LeadDto { id, code, name, companyName, email, phone, source, status, priority, expectedValue, owner, notes, convertedCustomerId, convertedOpportunityId, createdDate }`.

### Opportunities (`opportunities.*`)
| Method | Path | Notes |
|---|---|---|
| GET | `/api/opportunities` | `search`, `customerId`, `stage`, `status`, `ownerId`, paging |
| GET | `/api/opportunities/{id}` | |
| POST | `/api/opportunities` | |
| PUT | `/api/opportunities/{id}` | |
| DELETE | `/api/opportunities/{id}` | |
| POST | `/api/opportunities/{id}/stage` | `{ stage, closeNote? }` |
| POST | `/api/opportunities/{id}/reopen` | Manager/Admin |

`OpportunityDto { id, name, customer: {id,name,code}, leadId, amount, probability, weightedAmount, stage, status, expectedCloseDate, closedDate, closeNote, source, owner, notes, createdDate }`.

### Follow-ups (`followups.*`)
| Method | Path | Notes |
|---|---|---|
| GET | `/api/followups` | `from`, `to`, `status`, `assignedToId`, `customerId`, `leadId`, `opportunityId`, `overdue=true` |
| POST | `/api/followups` | exactly one related id |
| POST | `/api/followups/{id}/complete` | `{ remarks?, logActivity? }` |
| POST | `/api/followups/{id}/reschedule` | `{ followUpDate }` |
| POST | `/api/followups/{id}/miss`, `/cancel` | |

`FollowUpDto { id, relatedType, relatedId, relatedName, followUpDate, subject, type, status, remarks, assignedTo, isOverdue, rescheduleCount, completedDate }`.

### Activities (`activities.*`) — SHOULD
`GET/POST /api/activities`, `GET/PUT/DELETE /api/activities/{id}`; `ActivityDto { id, type, subject, description, activityDate, status, relatedType, relatedId, assignedTo }`.

### Reports
| Method | Path | Access |
|---|---|---|
| GET | `/api/reports/pipeline` | `reports.pipeline`; `from`, `to`, `ownerId`; returns `PipelineReportDto { byStage[{stage,count,amount,weightedAmount}], byOwner[{owner,count,amount,weightedAmount}], totals }` |
| GET | `/api/reports/{customers\|leads\|followups\|opportunities\|conversion\|useractivity}` | Source §10 requires only pipeline; others SHOULD exist for parity with MVC |

## 4. Documentation
- Swagger/OpenAPI 3 via Swashbuckle (or built-in .NET 10 OpenAPI + Scalar), XML doc comments on controllers and DTOs, `[ProducesResponseType]` on every action including 400/401/403/404/409, security schemes for Bearer.
- Served at `/swagger` in Development; in Production only for Admin users. The raw `openapi.json` is checked in at `docs/api/openapi.v1.json` and a test fails if the generated document differs (contract drift guard).

## 5. API test matrix (each endpoint)
No token → 401. Token without permission → 403. Out-of-scope id → 404. Invalid body → 400 with the right field key and message. Duplicate → 409. Happy path → documented status and DTO shape. A response-shape test asserts no property named `passwordHash`, `securityStamp`, `rowVersion`, `isDeleted` exists in any response.
