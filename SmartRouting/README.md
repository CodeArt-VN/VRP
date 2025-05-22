# SmartRouting API

SmartRouting is a logistics system API designed for vehicle routing and assignment. It includes features such as address management, distance indexing, and order assignment to vehicles.

## Features
- CRUD operations for managing addresses.
- Distance indexing between addresses using latitude and longitude.
- Basic heuristic for vehicle routing and assignment.
- API documentation with Swagger.

## Prerequisites
- .NET Core 9.0 SDK
- SQL Server (local or Dockerized instance)
- Docker (optional for containerization)

## Setup Instructions

1. **Clone the Repository**
   ```bash
   git clone <repository-url>
   cd SmartRouting
   ```

2. **Configure the Database**
   Update the `appsettings.json` file with your SQL Server connection string:
   ```json
   "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=SmartRoutingDB;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;"
   }
   ```

3. **Run Migrations**
   Apply the database migrations to set up the schema:
   ```bash
   dotnet ef database update
   ```

4. **Run the Application**
   Start the API server:
   ```bash
   dotnet run --project SmartRouting/SmartRouting.csproj
   ```

5. **Access Swagger Documentation**
   Open your browser and navigate to:
   ```
   https://localhost:5001/swagger
   ```

## Testing the API

### Address Management
- **GET** `/api/Address` - Retrieve all addresses.
- **POST** `/api/Address` - Create a new address.
- **PUT** `/api/Address/{id}` - Update an existing address.
- **DELETE** `/api/Address/{id}` - Delete an address.

### Distance Indexing
- **POST** `/api/Address/IndexDistances` - Index distances between all addresses.

### Vehicle Routing
- **POST** `/api/VehicleRouting/AssignOrders` - Assign orders to vehicles based on routing logic.

## Containerization (Optional)

1. **Build the Docker Image**
   ```bash
   docker build -t smartrouting-api .
   ```

2. **Run the Docker Container**
   ```bash
   docker run -p 5000:5000 -p 5001:5001 smartrouting-api
   ```

## License
This project is licensed under the MIT License.