using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using aspnet_core_api.Data;
using aspnet_core_api.Services;
using aspnet_core_api.Models;
using aspnet_core_api.Middleware;
using AspNetCoreRateLimit;
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=gateway.db"; // Fallback to SQLite

// Configure Entity Framework with SQLite for simplicity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Add API Gateway specific services
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<IServiceDiscovery, ServiceDiscovery>();
builder.Services.AddScoped<IReportService, ReportService>();

// Add YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configure rate limiting
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            // Fallback key for development
            jwtKey = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "api-gateway",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "microservices-users",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Add authorization
builder.Services.AddAuthorization();

// Add controllers with JSON configuration to handle cycles
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Microservices API Gateway",
        Version = "v1",
        Description = "A gateway for managing microservices communication"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

// Add custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

// Use rate limiting
app.UseIpRateLimiting();

// Enable CORS
app.UseCors("AllowAll");

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map reverse proxy
app.MapReverseProxy();

// Map health checks
app.MapHealthChecks("/health");

// Ensure database is created with proper schema
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Drop and recreate database to ensure schema is correct
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();

    // Seed default user if not exists
    try
    {
        if (!context.Users.Any())
        {
            context.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
            });
            context.SaveChanges();
        }

        // Seed sample data for development
        if (!context.Products.Any())
        {
            var products = new List<Product>
            {
                new Product { Name = "Laptop Dell XPS 13", SKU = "DELL-XPS13", Price = 1299.99m, StockQuantity = 15, Category = "Electronics", Description = "High-performance ultrabook" },
                new Product { Name = "iPhone 14 Pro", SKU = "APPLE-IP14P", Price = 999.99m, StockQuantity = 25, Category = "Electronics", Description = "Latest iPhone model" },
                new Product { Name = "Office Chair", SKU = "CHAIR-001", Price = 299.99m, StockQuantity = 50, Category = "Furniture", Description = "Ergonomic office chair" },
                new Product { Name = "Wireless Mouse", SKU = "MOUSE-WL", Price = 29.99m, StockQuantity = 100, Category = "Accessories", Description = "Bluetooth wireless mouse" },
                new Product { Name = "USB-C Hub", SKU = "HUB-USBC", Price = 79.99m, StockQuantity = 75, Category = "Accessories", Description = "Multi-port USB-C hub" }
            };
            context.Products.AddRange(products);
            context.SaveChanges();
        }

        if (!context.Customers.Any())
        {
            var customers = new List<Customer>
            {
                new Customer { FirstName = "John", LastName = "Doe", Email = "john.doe@example.com", PhoneNumber = "555-0123", Address = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "USA" },
                new Customer { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@example.com", PhoneNumber = "555-0124", Address = "456 Oak Ave", City = "Los Angeles", State = "CA", PostalCode = "90210", Country = "USA" },
                new Customer { FirstName = "Bob", LastName = "Johnson", Email = "bob.johnson@example.com", PhoneNumber = "555-0125", Address = "789 Pine St", City = "Chicago", State = "IL", PostalCode = "60601", Country = "USA" }
            };
            context.Customers.AddRange(customers);
            context.SaveChanges();
        }

        if (!context.Suppliers.Any())
        {
            var suppliers = new List<Supplier>
            {
                new Supplier { Name = "Tech Solutions Inc.", ContactPerson = "Mike Wilson", Email = "mike@techsolutions.com", PhoneNumber = "555-0200", Address = "100 Tech Park", City = "Austin", State = "TX", PostalCode = "78701", Country = "USA" },
                new Supplier { Name = "Office Supplies Co.", ContactPerson = "Sarah Davis", Email = "sarah@officesupplies.com", PhoneNumber = "555-0201", Address = "200 Business Dr", City = "Denver", State = "CO", PostalCode = "80202", Country = "USA" }
            };
            context.Suppliers.AddRange(suppliers);
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        // Log the error but don't crash the application
        Console.WriteLine($"Error seeding database: {ex.Message}");
    }
}

app.Run();
