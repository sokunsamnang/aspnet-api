using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using aspnet_core_api.Data;
using aspnet_core_api.Models;
using aspnet_core_api.Models.Reports;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;

namespace aspnet_core_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Reports/generate
        [HttpPost("generate")]
        public async Task<ActionResult<ReportResponse>> GenerateReport(ReportRequest request)
        {
            var reportId = Guid.NewGuid().ToString();
            object reportData;

            switch (request.ReportType.ToLower())
            {
                case "sales":
                    reportData = await GenerateSalesReport(request.StartDate, request.EndDate, request.Parameters);
                    break;
                case "product-sales":
                    reportData = await GenerateProductSalesReport(request.StartDate, request.EndDate, request.Parameters);
                    break;
                case "customer":
                    reportData = await GenerateCustomerReport(request.StartDate, request.EndDate, request.Parameters);
                    break;
                case "inventory":
                    reportData = await GenerateInventoryReport(request.Parameters);
                    break;
                case "purchase":
                    reportData = await GeneratePurchaseReport(request.StartDate, request.EndDate, request.Parameters);
                    break;
                default:
                    return BadRequest("Invalid report type. Supported types: sales, product-sales, customer, inventory, purchase");
            }

            var response = new ReportResponse
            {
                ReportId = reportId,
                ReportType = request.ReportType,
                GeneratedAt = DateTime.UtcNow,
                Format = request.Format,
                Data = reportData,
                DownloadUrl = request.Format != "json" ? $"/api/reports/{reportId}/download" : string.Empty
            };

            return Ok(response);
        }

        // GET: api/Reports/sales-dashboard
        [HttpGet("sales-dashboard")]
        public async Task<ActionResult<object>> GetSalesDashboard([FromQuery] int days = 30)
        {
            var startDate = DateTime.Today.AddDays(-days);
            var endDate = DateTime.Today.AddDays(1);

            // Load sales data into memory first to avoid SQLite LINQ translation issues
            var salesRawData = await _context.Sales
                .Where(s => s.SaleDate >= startDate && s.SaleDate < endDate && s.Status == "Completed")
                .ToListAsync();

            var salesData = salesRawData
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new SalesReportData
                {
                    Date = g.Key,
                    TotalSales = g.Sum(s => s.NetAmount),
                    TotalOrders = g.Count(),
                    AverageOrderValue = g.Average(s => s.NetAmount),
                    Period = "Daily"
                })
                .OrderBy(s => s.Date)
                .ToList();

            var totalSales = salesData.Sum(s => s.TotalSales);
            var totalOrders = salesData.Sum(s => s.TotalOrders);
            var avgOrderValue = totalOrders > 0 ? totalSales / totalOrders : 0;

            // Load sale items with related data into memory first
            var saleItemsRawData = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startDate && si.Sale.SaleDate < endDate && si.Sale.Status == "Completed")
                .ToListAsync();

            var topProducts = saleItemsRawData
                .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.SKU })
                .Select(g => new ProductSalesReport
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    QuantitySold = g.Sum(si => si.Quantity),
                    Revenue = g.Sum(si => si.TotalPrice)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToList();

            var dashboard = new
            {
                Period = $"Last {days} days",
                Summary = new
                {
                    TotalSales = totalSales,
                    TotalOrders = totalOrders,
                    AverageOrderValue = avgOrderValue
                },
                DailySales = salesData,
                TopProducts = topProducts
            };

            return Ok(dashboard);
        }

        // GET: api/Reports/inventory-summary
        [HttpGet("inventory-summary")]
        public async Task<ActionResult<object>> GetInventorySummary()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            var lowStockProducts = products.Where(p => p.StockQuantity <= 10).ToList();
            var outOfStockProducts = products.Where(p => p.StockQuantity == 0).ToList();
            var totalValue = products.Sum(p => p.Price * p.StockQuantity);

            var categoryBreakdown = products
                .GroupBy(p => p.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    ProductCount = g.Count(),
                    TotalValue = g.Sum(p => p.Price * p.StockQuantity),
                    LowStockCount = g.Count(p => p.StockQuantity <= 10)
                })
                .OrderBy(c => c.Category)
                .ToList();

            var summary = new
            {
                TotalProducts = products.Count,
                TotalValue = totalValue,
                LowStockCount = lowStockProducts.Count,
                OutOfStockCount = outOfStockProducts.Count,
                CategoryBreakdown = categoryBreakdown,
                LowStockProducts = lowStockProducts.Take(10).Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.StockQuantity,
                    p.Price
                }),
                OutOfStockProducts = outOfStockProducts.Take(10).Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SKU,
                    p.Price
                })
            };

            return Ok(summary);
        }

        // GET: api/Reports/customer-analytics
        [HttpGet("customer-analytics")]
        public async Task<ActionResult<object>> GetCustomerAnalytics([FromQuery] int days = 90)
        {
            var startDate = DateTime.Today.AddDays(-days);

            // Load customer data with sales into memory first
            var customersWithSales = await _context.Customers
                .Include(c => c.Sales.Where(s => s.SaleDate >= startDate && s.Status == "Completed"))
                .Where(c => c.IsActive)
                .ToListAsync();

            var customerData = customersWithSales
                .Where(c => c.Sales.Any())
                .Select(c => new CustomerReport
                {
                    CustomerId = c.Id,
                    CustomerName = c.FirstName + " " + c.LastName,
                    Email = c.Email,
                    TotalOrders = c.Sales.Count(),
                    TotalSpent = c.Sales.Sum(s => s.NetAmount),
                    LastOrderDate = c.Sales.Any() ? c.Sales.Max(s => s.SaleDate) : DateTime.MinValue
                })
                .OrderByDescending(c => c.TotalSpent)
                .ToList();

            foreach (var customer in customerData)
            {
                if (customer.LastOrderDate > DateTime.Today.AddDays(-30))
                    customer.CustomerStatus = "Active";
                else if (customer.LastOrderDate > DateTime.Today.AddDays(-90))
                    customer.CustomerStatus = "Recent";
                else
                    customer.CustomerStatus = "Inactive";
            }

            var topCustomers = customerData.Take(10).ToList();
            var newCustomers = await _context.Customers
                .Where(c => c.CreatedAt >= startDate)
                .CountAsync();

            var analytics = new
            {
                Period = $"Last {days} days",
                TotalActiveCustomers = customerData.Count,
                NewCustomers = newCustomers,
                TotalRevenue = customerData.Sum(c => c.TotalSpent),
                AverageOrderValue = customerData.Any() ? customerData.Average(c => c.TotalSpent / c.TotalOrders) : 0,
                TopCustomers = topCustomers,
                CustomersByStatus = customerData
                    .GroupBy(c => c.CustomerStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList()
            };

            return Ok(analytics);
        }

        private async Task<IEnumerable<SalesReportData>> GenerateSalesReport(DateTime startDate, DateTime endDate, Dictionary<string, object> parameters)
        {
            var period = parameters.ContainsKey("period") ? parameters["period"].ToString() : "daily";

            // Load all sales data into memory first to avoid SQLite LINQ translation issues
            var salesData = await _context.Sales
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                .ToListAsync();

            return period?.ToLower() switch
            {
                "monthly" => salesData
                    .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                    .Select(g => new SalesReportData
                    {
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                        TotalSales = g.Sum(s => s.NetAmount),
                        TotalOrders = g.Count(),
                        AverageOrderValue = g.Average(s => s.NetAmount),
                        Period = "Monthly"
                    })
                    .OrderBy(s => s.Date)
                    .ToList(),
                "weekly" => salesData
                    .GroupBy(s => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(s.SaleDate, CalendarWeekRule.FirstDay, DayOfWeek.Monday))
                    .Select(g => new SalesReportData
                    {
                        Date = g.First().SaleDate.Date,
                        TotalSales = g.Sum(s => s.NetAmount),
                        TotalOrders = g.Count(),
                        AverageOrderValue = g.Average(s => s.NetAmount),
                        Period = "Weekly"
                    })
                    .OrderBy(s => s.Date)
                    .ToList(),
                _ => salesData
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new SalesReportData
                    {
                        Date = g.Key,
                        TotalSales = g.Sum(s => s.NetAmount),
                        TotalOrders = g.Count(),
                        AverageOrderValue = g.Average(s => s.NetAmount),
                        Period = "Daily"
                    })
                    .OrderBy(s => s.Date)
                    .ToList()
            };
        }

        private async Task<IEnumerable<ProductSalesReport>> GenerateProductSalesReport(DateTime startDate, DateTime endDate, Dictionary<string, object> parameters)
        {
            var categoryFilter = parameters.ContainsKey("category") ? parameters["category"].ToString() : null;

            var query = _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startDate && si.Sale.SaleDate <= endDate && si.Sale.Status == "Completed");

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                query = query.Where(si => si.Product.Category == categoryFilter);
            }

            // Load data into memory first to avoid SQLite LINQ translation issues
            var saleItemsData = await query.ToListAsync();

            return saleItemsData
                .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.SKU, si.Product.StockQuantity })
                .Select(g => new ProductSalesReport
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    QuantitySold = g.Sum(si => si.Quantity),
                    Revenue = g.Sum(si => si.TotalPrice),
                    StockRemaining = g.Key.StockQuantity
                })
                .OrderByDescending(p => p.Revenue)
                .ToList();
        }

        private async Task<IEnumerable<CustomerReport>> GenerateCustomerReport(DateTime startDate, DateTime endDate, Dictionary<string, object> parameters)
        {
            // Load customer data with sales into memory first
            var customersWithSales = await _context.Customers
                .Include(c => c.Sales.Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed"))
                .Where(c => c.IsActive)
                .ToListAsync();

            return customersWithSales
                .Where(c => c.Sales.Any())
                .Select(c => new CustomerReport
                {
                    CustomerId = c.Id,
                    CustomerName = c.FirstName + " " + c.LastName,
                    Email = c.Email,
                    TotalOrders = c.Sales.Count(),
                    TotalSpent = c.Sales.Sum(s => s.NetAmount),
                    LastOrderDate = c.Sales.Any() ? c.Sales.Max(s => s.SaleDate) : DateTime.MinValue
                })
                .OrderByDescending(c => c.TotalSpent)
                .ToList();
        }

        private async Task<IEnumerable<InventoryReport>> GenerateInventoryReport(Dictionary<string, object> parameters)
        {
            var lowStockThreshold = parameters.ContainsKey("lowStockThreshold") ?
                Convert.ToInt32(parameters["lowStockThreshold"]) : 10;

            return await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new InventoryReport
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    SKU = p.SKU,
                    CurrentStock = p.StockQuantity,
                    ReorderLevel = lowStockThreshold,
                    UnitPrice = p.Price,
                    TotalValue = p.Price * p.StockQuantity,
                    Status = p.StockQuantity == 0 ? "Out of Stock" :
                            p.StockQuantity <= lowStockThreshold ? "Low Stock" : "In Stock"
                })
                .OrderBy(i => i.ProductName)
                .ToListAsync();
        }

        private async Task<object> GeneratePurchaseReport(DateTime startDate, DateTime endDate, Dictionary<string, object> parameters)
        {
            var purchases = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
                .ToListAsync();

            var summary = new
            {
                TotalPurchases = purchases.Count,
                TotalAmount = purchases.Sum(p => p.NetAmount),
                CompletedPurchases = purchases.Count(p => p.Status == "Received"),
                PendingPurchases = purchases.Count(p => p.Status == "Pending"),
                TopSuppliers = purchases
                    .GroupBy(p => new { p.SupplierId, p.Supplier.Name })
                    .Select(g => new
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        TotalPurchases = g.Count(),
                        TotalAmount = g.Sum(p => p.NetAmount)
                    })
                    .OrderByDescending(s => s.TotalAmount)
                    .Take(10)
                    .ToList(),
                Purchases = purchases.Select(p => new
                {
                    p.Id,
                    p.PurchaseNumber,
                    p.PurchaseDate,
                    SupplierName = p.Supplier.Name,
                    p.TotalAmount,
                    p.Status,
                    p.PaymentStatus,
                    ItemCount = p.PurchaseItems.Count
                }).ToList()
            };

            return summary;
        }
    }
}
