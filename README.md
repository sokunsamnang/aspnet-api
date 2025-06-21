# Microservices API Gateway

A production-ready API Gateway built with ASP.NET Core 9.0 that provides centralized routing, authentication, rate limiting, and monitoring for microservices architecture.

## Features

### 🔄 **Reverse Proxy & Load Balancing**

-   Routes requests to multiple microservices using YARP (Yet Another Reverse Proxy)
-   Automatic load balancing across service instances
-   Health checks and automatic failover

### 🔐 **Security**

-   JWT-based authentication and authorization
-   API key validation for service-to-service communication
-   Rate limiting to prevent abuse
-   CORS configuration for cross-origin requests

### 📊 **Monitoring & Observability**

-   Request/response logging with correlation IDs
-   Health check endpoints for all services
-   Service discovery and registration
-   Comprehensive error handling

### 🚀 **Performance**

-   Built-in caching mechanisms
-   Connection pooling
-   Async/await throughout for optimal performance

## Architecture

```
[Client Apps] -> [API Gateway] -> [Service 1]
                     |         -> [Service 2]
                     |         -> [Service N]
                     |         -> [Auth Service]
```

## Quick Start

### Prerequisites

-   .NET 9.0 SDK
-   Docker & Docker Compose (optional)

### Running Locally

1. **Clone and navigate to the project:**

    ```bash
    git clone <repository-url>
    cd aspnet-core-api
    ```

2. **Restore packages:**

    ```bash
    dotnet restore
    ```

3. **Run the gateway:**

    ```bash
    cd src
    dotnet run
    ```

4. **Access the API:**
    - Swagger UI: http://localhost:5000
    - Health Check: http://localhost:5000/health
    - Gateway Status: http://localhost:5000/api/gateway/health

### Running with Docker Compose

```bash
docker-compose up --build
```

This will start:

-   API Gateway (port 5000)
-   Service 1 (port 5001)
-   Service 2 (port 5002)
-   Auth Service (port 5003)

## Configuration

### Service Routes

The gateway automatically routes requests based on URL patterns:

-   `/api/service1/*` → Service 1 (http://localhost:5001)
-   `/api/service2/*` → Service 2 (http://localhost:5002)
-   `/api/auth/*` → Auth Service (http://localhost:5003)

### Authentication

1. **Get JWT Token:**

    ```bash
    curl -X POST http://localhost:5000/api/auth/login \
      -H "Content-Type: application/json" \
      -H "X-API-Key: gateway-api-key-123" \
      -d '{"username": "admin", "password": "admin123"}'
    ```

2. **Use Token in Requests:**
    ```bash
    curl -X GET http://localhost:5000/api/gateway/services \
      -H "Authorization: Bearer YOUR_JWT_TOKEN" \
      -H "X-API-Key: gateway-api-key-123"
    ```

### API Keys

Add the following header to all requests:

```
X-API-Key: gateway-api-key-123
```

Valid API keys are configured in `appsettings.json`:

-   `gateway-api-key-123` (Default)
-   `client-api-key-456`
-   `service-api-key-789`

## API Endpoints

### Gateway Management

-   `GET /api/gateway/health` - Gateway health status
-   `GET /api/gateway/services` - List all registered services
-   `POST /api/gateway/services/{serviceName}/register` - Register new service instance
-   `DELETE /api/gateway/services/{serviceName}/deregister` - Remove service instance

### Authentication

-   `POST /api/auth/login` - Authenticate and get JWT token
-   `POST /api/auth/register` - Register new user

### Health Checks

-   `GET /health` - Overall system health

## Rate Limiting

The gateway implements rate limiting:

-   **100 requests per minute** per IP
-   **1000 requests per hour** per IP
-   Returns HTTP 429 when limits exceeded

## Project Structure

```
aspnet-core-api
├── src
│   ├── Controllers
│   │   ├── AuthController.cs
│   │   └── WeatherController.cs
│   ├── Models
│   │   ├── User.cs
│   │   └── LoginModel.cs
│   ├── Data
│   │   └── ApplicationDbContext.cs
│   ├── Services
│   │   └── AuthService.cs
│   ├── Program.cs
│   └── appsettings.json
├── Dockerfile
├── docker-compose.yml
├── aspnet-core-api.csproj
└── README.md
```

## Getting Started

### Prerequisites

-   Docker
-   Docker Compose

### Setup

1. Clone the repository:

    ```
    git clone <repository-url>
    cd aspnet-core-api
    ```

2. Build and run the application using Docker Compose:

    ```
    docker-compose up --build
    ```

3. The API will be available at `http://localhost:5000`.

### API Endpoints

-   **Login**: `POST /api/auth/login`

    -   Request body: `{ "username": "your_username", "password": "your_password" }`
    -   Response: JWT token for authenticated access.

-   **Logout**: `POST /api/auth/logout`

    -   Response: Confirmation of logout.

-   **Get Weather**: `GET /api/weather`
    -   Response: Sample weather data.

### Database Configuration

The application uses SQL Server as the database. The connection string and other settings are configured in `src/appsettings.json`.

### Docker Configuration

The project includes a `Dockerfile` and `docker-compose.yml` for containerization. The Dockerfile sets up the ASP.NET Core application, while the docker-compose file defines the services, including the SQL Server database.

## Contributing

Feel free to submit issues or pull requests for improvements or bug fixes.

## License

This project is licensed under the MIT License.
