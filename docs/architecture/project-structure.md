# ApexPost project structure

## Current solution layout

```text
ApexPost.sln
src/
  ApexPost.Domain/              # Domain model and business invariants
  ApexPost.Application/         # Use cases and application ports
  ApexPost.Infrastructure/      # Persistence and external adapters
  ApexPost.Web/                 # ASP.NET Core MVC application
tests/
  ApexPost.Domain.Tests/
  ApexPost.Application.Tests/
  ApexPost.Infrastructure.Tests/
  ApexPost.Web.Tests/
docs/
  architecture/
  adr/
  api/
```

The test projects mirror the production project boundaries. At present, each contains only a placeholder test and none references its production project. The production layers also remain mostly placeholders. The layout below is a convention for adding implementation incrementally, not a description of files that already exist.

## Suggested contents by project

```text
src/ApexPost.Domain/
  <Capability>/                 # Entities, value objects, enums, domain policies
src/ApexPost.Application/
  <Capability>/                 # Use cases, requests/responses, validation
  Abstractions/                  # Cross-cutting application ports where justified
src/ApexPost.Infrastructure/
  Persistence/Json/              # JSON storage, serializers, repositories, initialization
  Caching/                       # Cache adapters, if needed
  Integrations/                  # Explicit simulated or configured service adapters
src/ApexPost.Web/
  Controllers/                   # Thin MVC endpoints
  Models/                        # View/binding models; not application DTOs
  Views/                         # Razor views grouped by controller
  Middleware/                    # Web pipeline middleware, when needed
  wwwroot/                       # CSS, JavaScript, and browser assets
```

`<Capability>` should be replaced by a real business capability such as `Shipments`, `Inventory`, or `Branches`. Keep related behavior together. Avoid creating empty folders or speculative layers before a use case needs them. A class should have one clear owning layer; do not duplicate domain models as web view models and then treat the web type as a business contract.

## Test placement

Place domain rule tests in `ApexPost.Domain.Tests`, application use-case and validation tests in `ApexPost.Application.Tests`, JSON and adapter tests in `ApexPost.Infrastructure.Tests`, and MVC composition/request behavior tests in `ApexPost.Web.Tests`. Tests should reference the production project they exercise and use temporary isolated data for persistence tests. Name test files after the subject under test and test methods after the behavior and expected result.

## Naming and namespaces

- Match namespaces to project and folder paths under `ApexPost`.
- Use `PascalCase` for projects, folders, files, types, and public members; use `camelCase` for locals and parameters.
- Give application requests, responses, validators, and handlers names that include their operation or capability.
- Use `I`-prefixed names for interfaces, such as `IShipmentRepository`.
- Use `*Controller` for MVC controllers and `*ViewModel` for Web-only presentation models.
- Keep test names descriptive; replace starter names like `UnitTest1` as tests are implemented.

## Project references and build configuration

The allowed production reference graph is:

```text
Domain <- Application <- Web
   ^           ^          |
   |           |          | composition root may register adapters
   +---- Infrastructure --+
```

In project-reference terms, Application references Domain; Infrastructure references Application and Domain; Web references Application and Infrastructure. Domain has no project references. Test projects reference the production project under test. Avoid project cycles and references from inner layers to Web or Infrastructure.

All eight projects inherit shared `net10.0`, C# 14, nullable-reference-type, and implicit-using settings from the root `Directory.Build.props`. `global.json` pins SDK 10.0.401 and permits later servicing patches in that SDK feature band. Package versions remain explicitly pinned in the test project files; no `Directory.Packages.props` is needed while the package set is small and consistent. Keep target frameworks consistent unless a documented compatibility need justifies a change.
