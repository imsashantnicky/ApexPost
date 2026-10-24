# API and MVC endpoint inventory

## Overview

ApexPost currently exposes conventional MVC endpoints rather than a separate REST API with Swagger/OpenAPI generation. There is no exposed `/api` namespace or scaffolded OpenAPI document in the current startup configuration.

The project uses ASP.NET Core MVC with server-rendered HTML views and a few response-object endpoints for money-order operations.

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
| GET | `/Branches` | `BranchesController.Index` | Management | List branches |
| GET | `/Branches/Create` | `BranchesController.Create` | Management | Create branch form |
| POST | `/Branches/Create` | `BranchesController.Create` | Management | Create branch |
| GET | `/Branches/Edit/{id}` | `BranchesController.Edit` | Management | Edit branch form |
| POST | `/Branches/Edit` | `BranchesController.Edit` | Management | Update branch |
| POST | `/Branches/Deactivate` | `BranchesController.Deactivate` | Management | Deactivate branch |
| GET | `/Catalogue` | `CatalogueController.Index` | Management | List services |
| GET | `/Catalogue/Create` | `CatalogueController.Create` | Management | Create service form |
| POST | `/Catalogue/Create` | `CatalogueController.Create` | Management | Create service |
| GET | `/Catalogue/Edit/{id}` | `CatalogueController.Edit` | Management | Edit service form |
| POST | `/Catalogue/Edit` | `CatalogueController.Edit` | Management | Update service |
| POST | `/Catalogue/Deactivate` | `CatalogueController.Deactivate` | Management | Deactivate service |
| GET | `/Staff` | `StaffController.Index` | Management | List staff |
| GET | `/Staff/Create` | `StaffController.Create` | Management | Create staff form |
| POST | `/Staff/Create` | `StaffController.Create` | Management | Create staff |
| GET | `/Staff/Edit/{id}` | `StaffController.Edit` | Management | Edit staff form |
| POST | `/Staff/Edit` | `StaffController.Edit` | Management | Update staff |
| POST | `/Staff/Deactivate` | `StaffController.Deactivate` | Management | Deactivate staff |
| GET | `/Inventory` | `InventoryController.Index` | Management | List inventory |
| GET | `/Inventory/Create` | `InventoryController.Create` | Management | Create inventory entry |
| POST | `/Inventory/Create` | `InventoryController.Create` | Management | Create inventory item |
| POST | `/Inventory/Receive` | `InventoryController.Receive` | Management | Receive stock |
| POST | `/Inventory/Issue` | `InventoryController.Issue` | Management | Issue stock |
| GET | `/Shipments` | `ShipmentsController.Index` | Management | List shipments |
| GET | `/Shipments/Book` | `ShipmentsController.Book` | Management | Book shipment form |
| POST | `/Shipments/Book` | `ShipmentsController.Book` | Management | Book shipment |
| POST | `/Shipments/Advance` | `ShipmentsController.Advance` | Management | Accept, dispatch, deliver, or cancel shipment |
| GET | `/Reports` | `ReportsController.Index` | Management | Operational summary |
| POST | `/MoneyOrders/Issue` | `MoneyOrdersController.Issue` | Management | Issue a money order |
| POST | `/MoneyOrders/Pay` | `MoneyOrdersController.Pay` | Management | Mark money order as paid |
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
- Form fields: `UserName`, `Password`, `Role`
- Restriction: only an authenticated admin may access the page or action
- Success: redirects back to the provisioning page and sets a status message

### Money-order operations

- Method: `POST /MoneyOrders/Issue` and `POST /MoneyOrders/Pay`
- These actions return `200 OK` on success and `400 Bad Request` on validation or domain-rule failures.

### Health checks

The app exposes three health endpoints with minimal payloads, no secret values, and no operational configuration details.

## OpenAPI / Swagger status

Not implemented in the current repository. There is no configured Swagger UI or OpenAPI generation in startup.
