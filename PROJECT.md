# Project: IoBuild Service Connectivity, Screen Validation & OpenCode Audit

## Architecture
- **Monorepo Architecture**:
  * Frontend: Vue 3 Single Page Application (Vite, PrimeVue, Pinia, Vue Router)
  * Reverse Proxy: Nginx on port 80 & 8081 routing `/` -> Frontend, `/api/` -> Backend, `/swagger` -> Swagger UI
  * Backend: ASP.NET Core 9 Modular Monolith (DDD architecture, 8 Bounded Contexts: IAM, Publishing, Subscriptions, Profiles, Devices, Analytics, Proration, Shared)
  * Database: MySQL 8.0 with 27 relational tables and preloaded seeds
  * IoT Infrastructure: Eclipse Mosquitto MQTT broker on port 1883 with active telemetry simulator container
- **Authentication & Roles**:
  * JWT Bearer token authentication
  * PropertyManager: `mveramendi@iobuild.pe` / `Admin123!` (Admin / PropertyManager role)
  * Resident: `cflores@gmail.com` / `Resident123!` (Resident role, Dpto 402, 3.8500% aliquot)

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | F1: Service Connectivity & Infrastructure Health | Verify all 6 Docker containers, `/health` endpoint (HTTP 200), Swagger UI, and Nginx reverse proxy routing | M1 | Survey (explorer 1) / R1 |
| 2 | F2: Role & Authorization Alignment | Support PropertyManager role in PublishingEndpoints and IamEndpoints; allow Resident to view own project details | M2 | Survey (explorer 2 & 3) / R3 |
| 3 | F3: Subscription Seeding & Data Contracts | Seed active subscription for `mveramendi` in DataSeeder.cs; return administratorId and condominiumId in GET /subscriptions | M2 | Survey (explorer 2 & 3) / R3 |
| 4 | F4: REST Endpoint Contract Alignment | Implement or alias endpoints expected by frontend (/condominiums -> /projects, /alerts, /utility_bills, /settlements, /sessions) | M2 | Survey (explorer 1, 2, 3) / R3 |
| 5 | F5: Unit Entity & Resident Query Filtering | Fix `projectId` vs `condominiumId` on Unit responses; respect `residentUserId` filter in PublishingEndpoints | M2 | Survey (explorer 2 & 3) / R3 |
| 6 | F6: PropertyManager Screen Validation | Validate all 11 menu routes and views for PropertyManager using Playwright/Fetch, ensuring no blank screens, 4xx/5xx or console errors | M3 | Survey (explorer 2) / R2 |
| 7 | F7: Resident Screen Validation | Validate all 5 menu routes and views for Resident using Playwright/Fetch, ensuring correct data display for Unit 402 and no errors | M3 | Survey (explorer 2) / R2 |
| 8 | F8: OpenCode DeepSeek Flash Audit | Run scratch/run_audit.ps1 with OpenCode DeepSeek Flash with 90s timer verification | M4 | ORIGINAL_REQUEST / R4 |
| 9 | F9: Git Remote Branch Publishing | Create `fix/verification-fixes` branch, commit verified changes, and push to GitHub remote repository | M4 | ORIGINAL_REQUEST / R4 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Infrastructure & Service Connectivity Check | Verify 6 containers, health endpoints, Swagger, and Nginx proxy | none | DONE |
| M2 | Backend & Integration Defect Remediation | Implement backend fixes (subscription seed, role checks, endpoint aliases, unit filtering) | M1 | DONE |
| M3 | Comprehensive Screen & Flow Validation (Playwright/Fetch) | Validate all screens and navigation for PropertyManager and Resident roles | M2 | DONE |
| M4 | OpenCode 90s Audit & Remote Git Publishing | Execute 90s timer audit with DeepSeek Flash and push to remote git branch | M3 | IN_PROGRESS |

## Interface Contracts
### Frontend (Vue 3 / PrimeVue) ↔ Backend (ASP.NET Core 9)
- **Authentication**: `POST /api/v1/authentication/sign-in` -> `{ token, id, email, roles, ... }`
- **Condominiums / Projects**: `GET /api/v1/condominiums` (aliased to `/api/v1/projects`) -> `[{ id, name, totalUnits, builderId, ... }]`
- **Subscriptions**: `GET /api/v1/subscriptions?administratorId={id}` -> `[{ id, plan, status: "Active", administratorId, projectId, ... }]`
- **Units**: `GET /api/v1/projects/{projectId}/units?residentUserId={id}` -> `[{ id, unitNumber, aliquotPercentage, projectId, condominiumId, ... }]`
- **Alerts**: `GET /api/v1/alerts` or `GET /api/v1/analytics/projects/{projectId}/alerts` -> `[{ id, severity, title, description, timestamp, ... }]`
- **Utility Bills & Settlements**: `/api/v1/utility_bills`, `/api/v1/settlements` -> mapped to Proration Bounded Context endpoints.
- **Sessions**: `GET /api/v1/sessions?userId={id}` -> list of active sessions for security view.

## Code Layout
- Frontend: `src/` (Vue components, views, stores, router, services)
- Backend: `src/IoBuild.Api/`, `src/IoBuild.Modules.*`
- Nginx & Docker: `docker-compose.yml`, `nginx/`, `mosquitto/`
- Audit & Scripts: `scratch/run_audit.ps1`
