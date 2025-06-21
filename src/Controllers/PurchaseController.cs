using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using aspnet_core_api.Data;
using aspnet_core_api.Models;
using Microsoft.AspNetCore.Authorization;

namespace aspnet_core_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PurchaseController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PurchaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Purchase
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Purchase>>> GetPurchases(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? supplierId = null)
        {
            var query = _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (startDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate <= endDate.Value);
            }

            if (supplierId.HasValue)
            {
                query = query.Where(p => p.SupplierId == supplierId.Value);
            }

            var totalCount = await query.CountAsync();
            var purchases = await query
                .OrderByDescending(p => p.PurchaseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(purchases);
        }

        // GET: api/Purchase/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Purchase>> GetPurchase(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            return purchase;
        }

        // POST: api/Purchase
        [HttpPost]
        public async Task<ActionResult<Purchase>> CreatePurchase(CreatePurchaseRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate supplier exists
                var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
                if (supplier == null)
                {
                    return BadRequest("Supplier not found.");
                }

                // Validate all products exist
                var productIds = request.Items.Select(i => i.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id) && p.IsActive)
                    .ToListAsync();

                if (products.Count != productIds.Distinct().Count())
                {
                    return BadRequest("One or more products not found or inactive.");
                }

                // Generate purchase number
                var purchaseNumber = await GeneratePurchaseNumber();

                // Create purchase
                var purchase = new Purchase
                {
                    PurchaseNumber = purchaseNumber,
                    SupplierId = request.SupplierId,
                    Status = "Pending",
                    PaymentStatus = "Pending",
                    Notes = request.Notes,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Purchases.Add(purchase);
                await _context.SaveChangesAsync();

                // Create purchase items and calculate totals
                decimal subtotal = 0;
                foreach (var itemRequest in request.Items)
                {
                    var totalPrice = itemRequest.UnitPrice * itemRequest.Quantity - itemRequest.Discount;

                    var purchaseItem = new PurchaseItem
                    {
                        PurchaseId = purchase.Id,
                        ProductId = itemRequest.ProductId,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = itemRequest.UnitPrice,
                        Discount = itemRequest.Discount,
                        TotalPrice = totalPrice
                    };

                    _context.PurchaseItems.Add(purchaseItem);
                    subtotal += totalPrice;
                }

                // Calculate purchase totals
                purchase.TotalAmount = subtotal;
                purchase.DiscountAmount = request.Items.Sum(i => i.Discount);
                purchase.TaxAmount = subtotal * (request.TaxRate ?? 0);
                purchase.NetAmount = subtotal + purchase.TaxAmount;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Return the created purchase with all related data
                return await GetPurchase(purchase.Id);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // PUT: api/Purchase/5/receive
        [HttpPut("{id}/receive")]
        public async Task<IActionResult> ReceivePurchase(int id, [FromBody] ReceivePurchaseRequest request)
        {
            var purchase = await _context.Purchases
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            if (purchase.Status != "Pending")
            {
                return BadRequest("Only pending purchases can be received.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Update stock quantities
                foreach (var item in purchase.PurchaseItems)
                {
                    // Check if specific quantity was received for this item
                    var receivedItem = request.ReceivedItems?.FirstOrDefault(ri => ri.ProductId == item.ProductId);
                    var receivedQuantity = receivedItem?.ReceivedQuantity ?? item.Quantity;

                    item.Product.StockQuantity += receivedQuantity;
                    item.Product.UpdatedAt = DateTime.UtcNow;
                }

                purchase.Status = "Received";
                purchase.ReceivedDate = DateTime.UtcNow;
                purchase.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    purchase.Notes += $"\nReceived: {request.Notes}";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // PUT: api/Purchase/5/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdatePurchaseStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var purchase = await _context.Purchases.FindAsync(id);
            if (purchase == null)
            {
                return NotFound();
            }

            purchase.Status = request.Status;
            purchase.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT: api/Purchase/5/payment-status
        [HttpPut("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            var purchase = await _context.Purchases.FindAsync(id);
            if (purchase == null)
            {
                return NotFound();
            }

            purchase.PaymentStatus = request.PaymentStatus;
            purchase.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/Purchase/suppliers
        [HttpGet("suppliers")]
        public async Task<ActionResult<IEnumerable<Supplier>>> GetSuppliers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            var query = _context.Suppliers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search) ||
                                       s.ContactPerson.Contains(search) ||
                                       s.Email.Contains(search));
            }

            if (isActive.HasValue)
            {
                query = query.Where(s => s.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();
            var suppliers = await query
                .OrderBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(suppliers);
        }

        // POST: api/Purchase/suppliers
        [HttpPost("suppliers")]
        public async Task<ActionResult<Supplier>> CreateSupplier(Supplier supplier)
        {
            supplier.CreatedAt = DateTime.UtcNow;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSupplier", "Purchase", new { id = supplier.Id }, supplier);
        }

        private async Task<string> GeneratePurchaseNumber()
        {
            var today = DateTime.Today;
            var prefix = $"PO{today:yyyyMMdd}";

            var lastPurchase = await _context.Purchases
                .Where(p => p.PurchaseNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PurchaseNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastPurchase != null)
            {
                var lastNumberStr = lastPurchase.PurchaseNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }
    }

    public class CreatePurchaseRequest
    {
        public int SupplierId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public decimal? TaxRate { get; set; }
        public List<CreatePurchaseItemRequest> Items { get; set; } = new List<CreatePurchaseItemRequest>();
    }

    public class CreatePurchaseItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class ReceivePurchaseRequest
    {
        public string Notes { get; set; } = string.Empty;
        public List<ReceivedItemRequest>? ReceivedItems { get; set; }
    }

    public class ReceivedItemRequest
    {
        public int ProductId { get; set; }
        public int ReceivedQuantity { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdatePaymentStatusRequest
    {
        public string PaymentStatus { get; set; } = string.Empty;
    }
}
