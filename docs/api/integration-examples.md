# API and MVC endpoint inventory

## Overview

ApexPost currently exposes conventional MVC endpoints rather than a separate REST API with Swagger/OpenAPI generation. There is no exposed `/api` namespace or scaffolded OpenAPI document in the current startup configuration.

The project uses ASP.NET Core MVC with server-rendered HTML views and a few response-object endpoints for money-order operations.

The partner integration route is `POST /api/integrations/money-orders`. It requires `X-API-Key`, an active configured Counter Clerk staff identity and an allowed source IP. See [security boundaries](../operations/security-boundaries.md) for secret and proxy configuration.

## Authentication and authorization model

- `/Account/Login` and `/Account/Bootstrap` are public.
- `/Account/Provision` requires the `AdministratorOnly` policy.
- Management endpoints require the `Management` policy.
- `/dashboard` requires a valid authenticated user.
- Health endpoints are public and intentionally minimal.

## Endpoint reference

| Method | Route | Controller action | Auth | Notes |
| --- | --- | --- | --- | --- |
| GET | `/` | `HomeController.Index` | None | Landing page |
| GET | `/Home/Privacy` | `HomeController.Privacy` | None | Privacy page |
| GET | `/Home/Error` | `HomeController.Error` | None | Error page |
| GET | `/Account/Bootstrap` | `AccountController.Bootstrap` | None | First-admin setup if no accounts exist |
| POST | `/Account/Bootstrap` | `AccountController.Bootstrap` | None | Creates first administrator |
| GET | `/Account/Login` | `AccountController.Login` | None | Sign-in page |
| POST | `/Account/Login` | `AccountController.Login` | None | Validates credentials and signs in |
| POST | `/Account/Logout` | `AccountController.Logout` | Authenticated | Signs out |
| GET | `/Account/AccessDenied` | `AccountController.AccessDenied` | None | Access denied page |
| GET | `/Account/Provision` | `AccountController.Provision` | AdministratorOnly | Create additional local accounts |
| POST | `/Account/Provision` | `AccountController.Provision` | AdministratorOnly | Create local account |
| GET | `/dashboard` | `DashboardController.Index` | Authenticated | Dashboard |
| GET | `/Branches` | `BranchesController.Index` | AdministratorOnly | List branches |
| GET | `/Branches/Create` | `BranchesController.Create` | AdministratorOnly | Create branch form |
| POST | `/Branches/Create` | `BranchesController.Create` | AdministratorOnly | Create branch |
| GET | `/Branches/Edit/{id}` | `BranchesController.Edit` | AdministratorOnly | Edit branch form |
| POST | `/Branches/Edit` | `BranchesController.Edit` | AdministratorOnly | Update branch |
| POST | `/Branches/Deactivate` | `BranchesController.Deactivate` | AdministratorOnly | Deactivate branch |
| GET | `/Catalogue` | `CatalogueController.Index` | AdministratorOnly | List services |
| GET | `/Catalogue/Create` | `CatalogueController.Create` | AdministratorOnly | Create service form |
| POST | `/Catalogue/Create` | `CatalogueController.Create` | AdministratorOnly | Create service |
| GET | `/Catalogue/Edit/{id}` | `CatalogueController.Edit` | AdministratorOnly | Edit service form |
| POST | `/Catalogue/Edit` | `CatalogueController.Edit` | AdministratorOnly | Update service |
| POST | `/Catalogue/Deactivate` | `CatalogueController.Deactivate` | AdministratorOnly | Deactivate service |
| GET | `/Staff` | `StaffController.Index` | AdministratorOnly | List staff |
| GET | `/Staff/Create` | `StaffController.Create` | AdministratorOnly | Create staff form |
| POST | `/Staff/Create` | `StaffController.Create` | AdministratorOnly | Create staff |
| GET | `/Staff/Edit/{id}` | `StaffController.Edit` | AdministratorOnly | Edit staff form |
| POST | `/Staff/Edit` | `StaffController.Edit` | AdministratorOnly | Update staff |
| POST | `/Staff/Deactivate` | `StaffController.Deactivate` | AdministratorOnly | Deactivate staff |
| GET | `/Inventory` | `InventoryController.Index` | InventoryOperations | List inventory |
| GET | `/Inventory/Create` | `InventoryController.Create` | InventoryOperations | Create inventory entry |
| POST | `/Inventory/Create` | `InventoryController.Create` | InventoryOperations | Create inventory item |
| POST | `/Inventory/Receive` | `InventoryController.Receive` | InventoryOperations | Receive stock |
| POST | `/Inventory/Issue` | `InventoryController.Issue` | InventoryOperations | Issue stock |
| GET | `/Shipments` | `ShipmentsController.Index` | CounterOperations | List shipments |
| GET | `/Shipments/Book` | `ShipmentsController.Book` | CounterOperations | Book shipment form |
| POST | `/Shipments/Book` | `ShipmentsController.Book` | CounterOperations | Book shipment |
| POST | `/Shipments/Advance` | `ShipmentsController.Advance` | CounterOperations | Accept, dispatch, deliver, or cancel shipment |
| GET | `/Reports` | `ReportsController.Index` | ReportsAccess | Operational summary, branch-scoped for managers |
| POST | `/MoneyOrders/Issue` | `MoneyOrdersController.Issue` | CounterOperations | Issue a money order |
| POST | `/MoneyOrders/Pay` | `MoneyOrdersController.Pay` | CounterOperations | Mark money order as paid |
| GET | `/health` | ASP.NET Core | None | General health endpoint |
| GET | `/health/ready` | ASP.NET Core | None | Readiness endpoint |
| GET | `/health/live` | ASP.NET Core | None | Liveness endpoint |

## Request and response notes

### Login

- Method: `POST /Account/Login`
- Form fields: `UserName`, `Password`
- Validation: model validation and rate limiting
- Success: redirects to `/dashboard`
- Failure: returns the login page with validation errors

### Provisioning

- Method: `POST /Account/Provision`
- Form fields: `UserName`, `Password`, `Role`, `StaffId`
- Restriction: only an authenticated admin may access the page or action
- The submitted `StaffId` selects a record; the server reloads it, requires active employment, verifies role compatibility, and derives the assigned branch.
- Success: redirects back to the provisioning page and sets a status message

### Money-order operations

- Method: `POST /MoneyOrders/Issue` and `POST /MoneyOrders/Pay`
- These actions return `200 OK` on success. Application authorization failures map to `401` or `403`, missing records/branches to `404`, closed-branch conflicts to `409`, and invalid business input to `400`.

### Health checks

The app exposes three health endpoints with minimal payloads, no secret values, and no operational configuration details.

## OpenAPI / Swagger status

Not implemented in the current repository. There is no configured Swagger UI or OpenAPI generation in startup.
