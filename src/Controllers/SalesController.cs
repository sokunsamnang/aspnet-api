using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using aspnet_core_api.Data;
using aspnet_core_api.Models;
using aspnet_core_api.Models.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace aspnet_core_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Sales
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SaleDto>>> GetSales(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? customerId = null)
        {
            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(s => s.Status == status);
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.SaleDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(s => s.SaleDate <= endDate.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(s => s.CustomerId == customerId.Value);
            }

            var totalCount = await query.CountAsync();
            var sales = await query
                .OrderByDescending(s => s.SaleDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page"] = page.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            var salesDto = sales.Select(s => new SaleDto
            {
                Id = s.Id,
                SaleNumber = s.SaleNumber,
                SaleDate = s.SaleDate,
                CustomerId = s.CustomerId,
                CustomerName = $"{s.Customer.FirstName} {s.Customer.LastName}",
                CustomerEmail = s.Customer.Email,
                SubTotal = s.TotalAmount - s.TaxAmount + s.DiscountAmount, // Calculate subtotal
                TaxAmount = s.TaxAmount,
                DiscountAmount = s.DiscountAmount,
                NetAmount = s.NetAmount,
                Status = s.Status,
                PaymentStatus = s.PaymentStatus,
                PaymentMethod = s.PaymentMethod,
                Notes = s.Notes,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt ?? s.CreatedAt,
                SaleItems = s.SaleItems.Select(si => new SaleItemDto
                {
                    Id = si.Id,
                    ProductId = si.ProductId,
                    ProductName = si.Product.Name,
                    ProductSKU = si.Product.SKU,
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    DiscountAmount = si.Discount,
                    TotalPrice = si.TotalPrice
                }).ToList()
            }).ToList();

            return Ok(salesDto);
        }

        // GET: api/Sales/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SaleDto>> GetSale(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
            {
                return NotFound();
            }

            var saleDto = new SaleDto
            {
                Id = sale.Id,
                SaleNumber = sale.SaleNumber,
                SaleDate = sale.SaleDate,
                CustomerId = sale.CustomerId,
                CustomerName = $"{sale.Customer.FirstName} {sale.Customer.LastName}",
                CustomerEmail = sale.Customer.Email,
                SubTotal = sale.TotalAmount - sale.TaxAmount + sale.DiscountAmount,
                TaxAmount = sale.TaxAmount,
                DiscountAmount = sale.DiscountAmount,
                NetAmount = sale.NetAmount,
                Status = sale.Status,
                PaymentStatus = sale.PaymentStatus,
                PaymentMethod = sale.PaymentMethod,
                Notes = sale.Notes,
                CreatedAt = sale.CreatedAt,
                UpdatedAt = sale.UpdatedAt ?? sale.CreatedAt,
                SaleItems = sale.SaleItems.Select(si => new SaleItemDto
                {
                    Id = si.Id,
                    ProductId = si.ProductId,
                    ProductName = si.Product.Name,
                    ProductSKU = si.Product.SKU,
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    DiscountAmount = si.Discount,
                    TotalPrice = si.TotalPrice
                }).ToList()
            };

            return Ok(saleDto);
        }

        // POST: api/Sales
        [HttpPost]
        public async Task<ActionResult<SaleDto>> CreateSale(CreateSaleRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate customer exists
                var customer = await _context.Customers.FindAsync(request.CustomerId);
                if (customer == null)
                {
                    return BadRequest("Customer not found.");
                }

                // Validate all products exist and have sufficient stock
                var productIds = request.Items.Select(i => i.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id) && p.IsActive)
                    .ToListAsync();

                if (products.Count != productIds.Distinct().Count())
                {
                    return BadRequest("One or more products not found or inactive.");
                }

                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    if (product.StockQuantity < item.Quantity)
                    {
                        return BadRequest($"Insufficient stock for product {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}");
                    }
                }

                // Generate sale number
                var saleNumber = await GenerateSaleNumber();

                // Create sale
                var sale = new Sale
                {
                    SaleNumber = saleNumber,
                    CustomerId = request.CustomerId,
                    Status = "Pending",
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = "Pending",
                    Notes = request.Notes,
                    SaleDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Create sale items and calculate totals
                decimal subtotal = 0;
                foreach (var itemRequest in request.Items)
                {
                    var product = products.First(p => p.Id == itemRequest.ProductId);
                    var unitPrice = itemRequest.UnitPrice ?? product.Price;
                    var totalPrice = unitPrice * itemRequest.Quantity - itemRequest.Discount;

                    var saleItem = new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = itemRequest.ProductId,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = unitPrice,
                        Discount = itemRequest.Discount,
                        TotalPrice = totalPrice
                    };

                    _context.SaleItems.Add(saleItem);
                    subtotal += totalPrice;

                    // Update product stock
                    product.StockQuantity -= itemRequest.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                // Calculate sale totals
                sale.TotalAmount = subtotal;
                sale.DiscountAmount = request.Items.Sum(i => i.Discount);
                sale.TaxAmount = subtotal * (request.TaxRate ?? 0);
                sale.NetAmount = subtotal + sale.TaxAmount;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Return the created sale with all related data
                var result = await GetSale(sale.Id);
                return result;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // PUT: api/Sales/5/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateSaleStatus(int id, [FromBody] UpdateSaleStatusRequest request)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
            {
                return NotFound();
            }

            var oldStatus = sale.Status;
            sale.Status = request.Status;
            sale.UpdatedAt = DateTime.UtcNow;

            // If cancelling a sale, restore stock
            if (request.Status == "Cancelled" && oldStatus != "Cancelled")
            {
                foreach (var item in sale.SaleItems)
                {
                    item.Product.StockQuantity += item.Quantity;
                    item.Product.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/Sales/5/payment-status
        [HttpPut("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdateSalePaymentStatusRequest request)
        {
            var sale = await _context.Sales.FindAsync(id);
            if (sale == null)
            {
                return NotFound();
            }

            sale.PaymentStatus = request.PaymentStatus;
            sale.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/Sales/dashboard
        [HttpGet("dashboard")]
        public async Task<ActionResult<object>> GetSalesDashboard()
        {
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            var thisYear = new DateTime(today.Year, 1, 1);

            var todaySales = await _context.Sales
                .Where(s => s.SaleDate >= today && s.Status == "Completed")
                .SumAsync(s => s.NetAmount);

            var monthSales = await _context.Sales
                .Where(s => s.SaleDate >= thisMonth && s.Status == "Completed")
                .SumAsync(s => s.NetAmount);

            var yearSales = await _context.Sales
                .Where(s => s.SaleDate >= thisYear && s.Status == "Completed")
                .SumAsync(s => s.NetAmount);

            var todayOrders = await _context.Sales
                .CountAsync(s => s.SaleDate >= today);

            var pendingOrders = await _context.Sales
                .CountAsync(s => s.Status == "Pending");

            var dashboard = new
            {
                TodaySales = todaySales,
                MonthSales = monthSales,
                YearSales = yearSales,
                TodayOrders = todayOrders,
                PendingOrders = pendingOrders,
                GeneratedAt = DateTime.UtcNow
            };

            return Ok(dashboard);
        }

        private async Task<string> GenerateSaleNumber()
        {
            var today = DateTime.Today;
            var prefix = $"SL{today:yyyyMMdd}";

            var lastSale = await _context.Sales
                .Where(s => s.SaleNumber.StartsWith(prefix))
                .OrderByDescending(s => s.SaleNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastSale != null)
            {
                var lastNumberStr = lastSale.SaleNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }
    }

    public class CreateSaleRequest
    {
        public int CustomerId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public decimal? TaxRate { get; set; }
        public List<CreateSaleItemRequest> Items { get; set; } = new List<CreateSaleItemRequest>();
    }

    public class CreateSaleItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class UpdateSaleStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdateSalePaymentStatusRequest
    {
        public string PaymentStatus { get; set; } = string.Empty;
    }
}
