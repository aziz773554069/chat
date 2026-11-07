
using customerplatform.Api.Domain.Entities;
using customerplatform.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace customerplatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context)
    {
        _context = context;
    }

    // ========================================
    // GET: api/customers
    // عرض جميع العملاء
    // Admin + Agent
    // ========================================

    [HttpGet]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
    {
        var customers = await _context.Customers
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(customers);
    }

    // ========================================
    // GET: api/customers/5
    // عرض عميل واحد
    // Admin + Agent
    // ========================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<Customer>> GetCustomer(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "العميل غير موجود"
            });
        }

        return Ok(customer);
    }

    // ========================================
    // POST: api/customers
    // إنشاء عميل جديد
    // Admin + Agent
    // ========================================

    [HttpPost]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<ActionResult<Customer>> CreateCustomer(
        Customer customer)
    {
        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(
                x => x.PhoneNumber == customer.PhoneNumber);

        if (existingCustomer != null)
        {
            return Conflict(new
            {
                message = "رقم الهاتف مسجل مسبقًا"
            });
        }

        customer.Id = 0;
        customer.CreatedAt = DateTime.UtcNow;
        customer.UpdatedAt = null;
        customer.IsActive = true;

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCustomer),
            new { id = customer.Id },
            customer
        );
    }

    // ========================================
    // PUT: api/customers/5
    // تعديل بيانات العميل
    // Admin + Agent
    // ========================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> UpdateCustomer(
        int id,
        Customer customer)
    {
        if (id != customer.Id)
        {
            return BadRequest(new
            {
                message = "معرف العميل غير صحيح"
            });
        }

        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existingCustomer == null)
        {
            return NotFound(new
            {
                message = "العميل غير موجود"
            });
        }

        // التحقق من رقم الهاتف
        var phoneExists = await _context.Customers
            .AnyAsync(x =>
                x.PhoneNumber == customer.PhoneNumber &&
                x.Id != id);

        if (phoneExists)
        {
            return Conflict(new
            {
                message = "رقم الهاتف مسجل مسبقًا لدى عميل آخر"
            });
        }

        existingCustomer.PhoneNumber = customer.PhoneNumber;
        existingCustomer.FullName = customer.FullName;
        existingCustomer.Email = customer.Email;
        existingCustomer.Notes = customer.Notes;
        existingCustomer.IsActive = customer.IsActive;
        existingCustomer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(existingCustomer);
    }

    // ========================================
    // DELETE: api/customers/5
    // تعطيل العميل
    // Admin فقط
    // ========================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "العميل غير موجود"
            });
        }

        if (!customer.IsActive)
        {
            return BadRequest(new
            {
                message = "العميل معطل بالفعل"
            });
        }

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تعطيل العميل بنجاح",
            customerId = id
        });
    }

    // ========================================
    // PUT: api/customers/5/activate
    // إعادة تفعيل العميل
    // Admin فقط
    // ========================================

    [HttpPut("{id:int}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivateCustomer(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == id);

        if (customer == null)
        {
            return NotFound(new
            {
                message = "العميل غير موجود"
            });
        }

        if (customer.IsActive)
        {
            return BadRequest(new
            {
                message = "العميل مفعل بالفعل"
            });
        }

        customer.IsActive = true;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تفعيل العميل بنجاح",
            customerId = id
        });
    }
}
