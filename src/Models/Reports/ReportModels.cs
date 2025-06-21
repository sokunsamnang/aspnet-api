using System;
using System.Collections.Generic;

namespace aspnet_core_api.Models.Reports
{
    public class SalesReportData
    {
        public DateTime Date { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public string Period { get; set; } = string.Empty;
    }

    public class ProductSalesReport
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int StockRemaining { get; set; }
    }

    public class CustomerReport
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime LastOrderDate { get; set; }
        public string CustomerStatus { get; set; } = string.Empty;
    }

    public class InventoryReport
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public string Status { get; set; } = string.Empty; // Low Stock, In Stock, Out of Stock
    }

    public class ReportRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = "json"; // json, pdf, excel
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class ReportResponse
    {
        public string ReportId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public string Format { get; set; } = string.Empty;
        public object Data { get; set; } = null!;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
