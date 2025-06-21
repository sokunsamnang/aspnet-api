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
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Customer
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            var query = _context.Customers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.FirstName.Contains(search) ||
                                       c.LastName.Contains(search) ||
                                       c.Email.Contains(search) ||
                                       c.PhoneNumber.Contains(search));
            }

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();
            var customers = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(customers);
        }

        // GET: api/Customer/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Sales)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return customer;
        }

        // POST: api/Customer
        [HttpPost]
        public async Task<ActionResult<Customer>> CreateCustomer(Customer customer)
        {
            // Check if email already exists
            if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
            {
                return BadRequest("A customer with this email already exists.");
            }

            customer.CreatedAt = DateTime.UtcNow;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }

        // PUT: api/Customer/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return BadRequest();
            }

            var existingCustomer = await _context.Customers.FindAsync(id);
            if (existingCustomer == null)
            {
                return NotFound();
            }

            // Check if email already exists for another customer
            if (await _context.Customers.AnyAsync(c => c.Email == customer.Email && c.Id != id))
            {
                return BadRequest("A customer with this email already exists.");
            }

            existingCustomer.FirstName = customer.FirstName;
            existingCustomer.LastName = customer.LastName;
            existingCustomer.Email = customer.Email;
            existingCustomer.PhoneNumber = customer.PhoneNumber;
            existingCustomer.Address = customer.Address;
            existingCustomer.City = customer.City;
            existingCustomer.State = customer.State;
            existingCustomer.PostalCode = customer.PostalCode;
            existingCustomer.Country = customer.Country;
            existingCustomer.IsActive = customer.IsActive;
            existingCustomer.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Customer/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            // Check if customer has any sales
            var hasSales = await _context.Sales.AnyAsync(s => s.CustomerId == id);
            if (hasSales)
            {
                // Soft delete - just mark as inactive
                customer.IsActive = false;
                customer.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Hard delete if no sales
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        // GET: api/Customer/5/sales
        [HttpGet("{id}/sales")]
        public async Task<ActionResult<IEnumerable<Sale>>> GetCustomerSales(int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var query = _context.Sales
                .Where(s => s.CustomerId == id)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product);

            var totalCount = await query.CountAsync();
            var sales = await query
                .OrderByDescending(s => s.SaleDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(sales);
        }

        // GET: api/Customer/5/summary
        [HttpGet("{id}/summary")]
        public async Task<ActionResult<object>> GetCustomerSummary(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var sales = await _context.Sales
                .Where(s => s.CustomerId == id && s.Status == "Completed")
                .ToListAsync();

            var summary = new
            {
                CustomerId = id,
                CustomerName = customer.FullName,
                TotalOrders = sales.Count,
                TotalSpent = sales.Sum(s => s.NetAmount),
                AverageOrderValue = sales.Any() ? sales.Average(s => s.NetAmount) : 0,
                LastOrderDate = sales.Any() ? sales.Max(s => s.SaleDate) : (DateTime?)null,
                FirstOrderDate = sales.Any() ? sales.Min(s => s.SaleDate) : (DateTime?)null
            };

            return Ok(summary);
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
