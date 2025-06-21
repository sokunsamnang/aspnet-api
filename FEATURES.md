# ASP.NET Core API - Enhanced Features

This ASP.NET Core API has been enhanced with comprehensive business management features including data setup, transaction processing, and reporting capabilities.

## New Features Added

### 1. Data Setup Features

#### Product Management (`/api/Product`)

-   **GET /api/Product** - List products with pagination, search, and filtering
    -   Query parameters: `page`, `pageSize`, `search`, `category`, `isActive`
-   **GET /api/Product/{id}** - Get product by ID
-   **POST /api/Product** - Create new product
-   **PUT /api/Product/{id}** - Update existing product
-   **DELETE /api/Product/{id}** - Soft delete product (marks as inactive)
-   **GET /api/Product/categories** - Get list of product categories
-   **POST /api/Product/{id}/adjust-stock** - Adjust product stock quantity

#### Customer Management (`/api/Customer`)

-   **GET /api/Customer** - List customers with pagination and search
    -   Query parameters: `page`, `pageSize`, `search`, `isActive`
-   **GET /api/Customer/{id}** - Get customer by ID with sales history
-   **POST /api/Customer** - Create new customer
-   **PUT /api/Customer/{id}** - Update existing customer
-   **DELETE /api/Customer/{id}** - Delete customer (soft delete if has sales)
-   **GET /api/Customer/{id}/sales** - Get customer's sales history
-   **GET /api/Customer/{id}/summary** - Get customer summary statistics

#### Supplier Management (`/api/Purchase/suppliers`)

-   **GET /api/Purchase/suppliers** - List suppliers with pagination and search
-   **POST /api/Purchase/suppliers** - Create new supplier

### 2. Transaction Processing

#### Sales Processing (`/api/Sales`)

-   **GET /api/Sales** - List sales with filtering options
    -   Query parameters: `page`, `pageSize`, `status`, `startDate`, `endDate`, `customerId`
-   **GET /api/Sales/{id}** - Get sale details with items
-   **POST /api/Sales** - Create new sale with automatic stock adjustment
-   **PUT /api/Sales/{id}/status** - Update sale status (Pending, Completed, Cancelled)
-   **PUT /api/Sales/{id}/payment-status** - Update payment status
-   **GET /api/Sales/dashboard** - Sales dashboard with key metrics

#### Purchase Processing (`/api/Purchase`)

-   **GET /api/Purchase** - List purchases with filtering options
    -   Query parameters: `page`, `pageSize`, `status`, `startDate`, `endDate`, `supplierId`
-   **GET /api/Purchase/{id}** - Get purchase details with items
-   **POST /api/Purchase** - Create new purchase order
-   **PUT /api/Purchase/{id}/receive** - Receive purchase and update stock
-   **PUT /api/Purchase/{id}/status** - Update purchase status
-   **PUT /api/Purchase/{id}/payment-status** - Update payment status

### 3. Reporting Features (`/api/Reports`)

#### Report Generation

-   **POST /api/Reports/generate** - Generate various types of reports
    -   Supported report types: `sales`, `product-sales`, `customer`, `inventory`, `purchase`
    -   Supports multiple formats: `json`, `pdf`, `excel`

#### Dashboard & Analytics

-   **GET /api/Reports/sales-dashboard** - Sales dashboard with charts and metrics
    -   Query parameter: `days` (default: 30)
-   **GET /api/Reports/inventory-summary** - Inventory summary and low stock alerts
-   **GET /api/Reports/customer-analytics** - Customer analytics and segmentation
    -   Query parameter: `days` (default: 90)

### Report Types Available

1. **Sales Report** - Daily/Weekly/Monthly sales performance
2. **Product Sales Report** - Product performance by revenue and quantity
3. **Customer Report** - Customer purchase history and statistics
4. **Inventory Report** - Current stock levels, values, and status
5. **Purchase Report** - Supplier performance and purchase history

## Data Models

### Core Entities

-   **Product** - Product catalog with SKU, pricing, and inventory
-   **Customer** - Customer information and contact details
-   **Supplier** - Supplier information for purchasing
-   **Sale** - Sales transactions with line items
-   **Purchase** - Purchase orders with line items

### Transaction Features

-   Automatic stock adjustments on sales and purchases
-   Transaction numbering (SL20241221001, PO20241221001)
-   Status tracking (Pending, Completed, Cancelled)
-   Payment status tracking
-   Tax and discount calculations

### Reporting Capabilities

-   Real-time dashboard metrics
-   Exportable reports in multiple formats
-   Customer segmentation and analytics
-   Inventory management alerts
-   Sales performance tracking

## Authentication

All endpoints require JWT authentication. Use the existing auth endpoints:

-   **POST /api/Auth/login** - Login with username/password
-   Default credentials: `admin` / `admin123`

## Sample Data

The application seeds with sample data including:

-   5 sample products across different categories
-   3 sample customers
-   2 sample suppliers

## Database

Uses SQLite database (`gateway.db`) with Entity Framework Core. The database is automatically created and seeded on first run.

## Usage Examples

### Create a Sale

```json
POST /api/Sales
{
  "customerId": 1,
  "paymentMethod": "Card",
  "taxRate": 0.08,
  "notes": "Customer sale",
  "items": [
    {
      "productId": 1,
      "quantity": 2,
      "discount": 0
    }
  ]
}
```

### Generate Sales Report

```json
POST /api/Reports/generate
{
  "reportType": "sales",
  "startDate": "2024-01-01",
  "endDate": "2024-12-31",
  "format": "json",
  "parameters": {
    "period": "monthly"
  }
}
```

### Create Purchase Order

```json
POST /api/Purchase
{
  "supplierId": 1,
  "taxRate": 0.08,
  "notes": "Restock inventory",
  "items": [
    {
      "productId": 1,
      "quantity": 50,
      "unitPrice": 899.99,
      "discount": 0
    }
  ]
}
```

## API Documentation

When running in development mode, full API documentation is available at the root URL via Swagger UI (`http://localhost:5000`).

## Features Summary

✅ **Data Setup**: Complete CRUD operations for Products, Customers, and Suppliers
✅ **Transaction Processing**: Full sales and purchase workflow with stock management
✅ **Reporting**: Comprehensive reporting system with multiple formats and dashboards
✅ **Authentication**: JWT-based security for all endpoints
✅ **Database**: Entity Framework Core with SQLite
✅ **API Documentation**: Swagger/OpenAPI integration
✅ **Error Handling**: Proper validation and error responses
✅ **Pagination**: Efficient data retrieval with pagination support
✅ **Search & Filtering**: Advanced search capabilities across all entities

The API now provides a complete business management solution suitable for retail, e-commerce, or inventory management applications.
