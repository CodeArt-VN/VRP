# SmartRouting Project Backlog

## Phase 1: Address Management & Spatial Data Integration

### Core Setup & GEOGRAPHY Type Adoption
- [x] Install `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` package.
- [x] Update `Microsoft.EntityFrameworkCore.SqlServer` package version to be compatible.
- [x] Update `Address` model (`Models/Address.cs`) to use `NetTopologySuite.Geometries.Point? Location` for coordinates, removing separate Latitude/Longitude properties.
- [x] Configure `ApplicationDbContext` (`Configurations/ApplicationDbContext.cs`) for `Address.Location` to use `geography` column type.
- [x] Update `Program.cs` to include `.UseNetTopologySuite()` in `AddDbContext` configuration.

### Database Migration for Spatial Index
- [x] Generate a new database migration for initial schema and `GEOGRAPHY` type.
- [x] Manually add `CREATE SPATIAL INDEX IX_tbl_001_Location ON tbl_001(Location);` to the `Up` method of the migration.
- [x] Manually add `DROP INDEX IX_tbl_001_Location ON tbl_001;` to the `Down` method of the migration.
- [x] Apply the database migration successfully.

### Address API Endpoint Adjustments & DTO Cleanup
- [x] Initialize `GeometryFactory` in `AddressController.cs` for creating `Point` objects.
- [x] Update `CreateAddresses` (POST) in `AddressController.cs` to accept `AddressInputModel` and convert Lat/Lon to `Point`.
- [x] Update `UpdateAddress` (PUT) in `AddressController.cs` to accept `AddressInputModel` and convert Lat/Lon to `Point`.
- [x] Refactor `PatchAddress` (PATCH) in `AddressController.cs`:
    - [x] Modify to accept `AddressInputModel` (instead of `JsonPatchDocument` or a separate DTO).
    - [x] Update logic to only modify properties provided in the request body.
    - [x] Ensure `Content-Type: application/json` is used.
    - [x] Add check for `Id` in request body matching route `Id`, similar to PUT.
- [x] Remove the `AddressInputModelForPatch` DTO class.
- [x] Test and confirm all `Address` API endpoints (GET all, GET by ID, POST, PUT, PATCH, DELETE) are working correctly.

### IndexDistance Entity & Relationship Refactoring
- [x] Configure `IndexDistance` entity in `ApplicationDbContext.cs` with `Id` as auto-generated PK and a unique index on `(Loc1, Loc2)`.
- [x] Remove navigation properties (`IndexDistancesAsLoc1`, `IndexDistancesAsLoc2`) from `Address.cs`.
- [x] Remove foreign key relationship configurations between `Address` and `IndexDistance` in `ApplicationDbContext.cs`'s `OnModelCreating`.
- [x] Generate and apply a new database migration (`RemoveAddressIndexDistanceRelationship`) for these changes.

### AddressController - Distance Indexing (`IndexDistances` endpoint)
- [x] **DEFERRED/REMOVED:** The `IndexDistances` API endpoint and its associated logic has been removed. Distance calculation will be handled by `DistanceService`.

## Phase 2: Vehicle Routing Feature

### Distance Calculation Service
- [x] Create `DistanceService.cs` (concrete class, no interface).
- [x] Implement Haversine distance calculation logic within `DistanceService.cs` accepting `Point` objects.
- [x] Register `DistanceService` for DI in `Program.cs`.

### Core Routing Logic & API (`api/Routes/Calc`)
- [x] Rename `VehicleRoutingController.cs` to `RoutesController.cs`.
- [x] Update endpoint to `api/Routes/Calc` and method to `CalculateRoutes`.
- [x] Inject `DistanceService` into `RoutesController.cs`.
- [x] Clear body of `CalculateRoutes` and replace with `throw new NotImplementedException()`.
- [x] **Define/Refine DTOs:**
    - [x] Review and update `VehicleRoutingRequest.cs`:
        - [x] For the initial planning phase (`api/Routes/Calc`), `Vehicle` objects in the request should **not** include `currentLocation`, `remainingVolume`, `remainingWeight`, `currentStatus`. These are for future tracking/live updates.
        - [x] `Vehicle.CurrentLocation` model property updated to `Point?` for future use.
    - [x] Review and finalize `VehicleRoutingResponse.cs`.
    - [ ] Define any other necessary DTOs for vehicle, order, or Depot data if not already covered.
- [x] **Implement `RoutesController.CalculateRoutes` method:**
    - [x] Develop API endpoint (`api/Routes/Calc`) to receive routing requests (`VehicleRoutingRequest`).
    - [x] Implement core vehicle routing algorithm/logic. This will likely involve:
        - [x] Retrieving address locations (using `IDDepotAddress` and `Order.IDAddress`).
        - [x] Using `DistanceService.CalculateHaversineDistance` for distance calculations between points.
        - [x] Considering vehicle capacities (MaxVolume, MaxWeight), and order details (Weight, Volume).
        - [x] Assigning orders to vehicles based on constraints and optimizing for a simple heuristic (e.g., minimizing total distance, filling vehicles efficiently).
        - [x] The Depot (`IDDepotAddress`) is the start and end point for each vehicle's route.
    - [x] Format and return routing solutions via `VehicleRoutingResponse`.
- [x] **Error Handling & Logging:**
    - [x] Implement robust error handling for invalid inputs, unsolvable routes, etc.
    - [x] Add detailed logging for the routing process.

## Phase 3: Testing & Refinements

### Test Data
- [x] Update `Test/Address.http` with 51 addresses (1 Depot, 50 delivery locations).
- [x] Create `Test/Route.http` with 5 vehicles and 50 orders, reflecting simplified vehicle payload for `api/Routes/Calc`.

### Unit & Integration Testing
- [ ] Write comprehensive unit tests for `AddressController.cs`.
- [ ] Write comprehensive unit tests for `DistanceService.cs`.
- [ ] Write comprehensive unit tests for `RoutesController.cs`.
- [ ] Consider adding integration tests for API endpoints.

### Data Management & Seeding
- [ ] Review/Implement `DataSeeder.cs` for populating initial/test data for addresses, vehicles, etc. (partially addressed with `.http` files).

### Configuration & Security
- [ ] Ensure secure management of API keys (e.g., Google Maps API key if used later) using user secrets or environment variables.
- [ ] Review overall application configuration in `appsettings.json`.

### Documentation
- [ ] Update `README.md` with detailed setup instructions, API endpoint documentation, and usage examples.
- [ ] Consider setting up Swagger/OpenAPI for interactive API documentation.

## General Tasks
- [ ] Code cleanup and refactoring as needed.
- [ ] Performance optimization, especially for routing algorithms.
