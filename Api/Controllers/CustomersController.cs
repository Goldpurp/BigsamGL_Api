using Application.Common.Interfaces;
using Application.DTOs;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers;

[Authorize(Policy = "StaffOnly")]
public class CustomersController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IClientIdentityService _identity;

    public CustomersController(IApplicationDbContext context, IClientIdentityService identity)
    {
        _context = context;
        _identity = identity;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        var duplicate = await _identity.ValidateNewClientAsync(new ClientIdentityInput(request.FullName,
            request.Email, request.Phone, null), request.DuplicateReviewId, null, default);
        if (duplicate is not null) return Conflict(duplicate);
        var customer = new Customer(
            Guid.NewGuid(),
            request.FullName,
            request.Email,
            request.InitialDeposit,
            request.CountryCode
        )
        {
            Phone = request.Phone.Trim(),
            CompanyName = request.CompanyName?.Trim(),
            AccountType = request.AccountType ?? (!string.IsNullOrWhiteSpace(request.CompanyName)
                ? ClientAccountType.Business : ClientAccountType.Individual),
            IsBusiness = request.AccountType is ClientAccountType.Business or ClientAccountType.PartnerAgency ||
                (request.AccountType is null && !string.IsNullOrWhiteSpace(request.CompanyName))
        };

        _context.Customers.Add(customer);

        if (request.InitialDeposit > 0)
        {
            var ledger = new WalletLedger(
                Guid.NewGuid(),
                customer.Id,
                customer.Id,
                request.InitialDeposit,
                customer.WalletCurrency,
                LedgerTransactionType.Deposit,
                "Initial wallet funding deposit on customer profile creation."
            );
            _context.WalletLedgers.Add(ledger);
        }

        await _context.SaveChangesAsync(default);

        return Ok(customer.ToDto());
    }

    [HttpGet]
    public async Task<ActionResult> GetAllCustomers([FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var ordered = _context.Customers.AsNoTracking().OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(x => x.Id);
        if (paged)
        {
            var result = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            return Ok(new PagedResponse<CustomerDto>(result.Items.Select(x => x.ToDto()).ToList(),
                result.Page, result.PageSize, result.HasPrevious, result.HasNext,
                result.Total, result.TotalPages));
        }
        return Ok((await ordered.Take(500).ToListAsync(cancellationToken)).Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound("Customer profile not found.");

        return Ok(customer.ToDto());
    }

    [HttpPost("{id}/topup")]
    public async Task<ActionResult<CustomerDto>> TopupWallet(Guid id, [FromBody] TopupRequest request)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound("Customer profile not found.");

        customer.CreditWallet(request.Amount);

        var ledger = new WalletLedger(
            Guid.NewGuid(),
            customer.Id,
            customer.Id,
            request.Amount,
            customer.WalletCurrency,
            LedgerTransactionType.Deposit,
            $"Wallet credited via top-up. Reference: {request.Reference ?? "N/A"}"
        );

        _context.WalletLedgers.Add(ledger);
        await _context.SaveChangesAsync(default);

        return Ok(customer.ToDto());
    }

    [HttpGet("{id}/ledger")]
    public async Task<ActionResult> GetCustomerLedger(Guid id, [FromQuery] bool paged = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeTotal = false, CancellationToken cancellationToken = default)
    {
        var ordered = _context.WalletLedgers.AsNoTracking()
            .Where(w => w.CustomerId == id)
            .OrderByDescending(w => w.Timestamp).ThenByDescending(x => x.Id);
        if (paged)
        {
            var result = await ordered.ToPagedResponseAsync(page, pageSize, includeTotal, cancellationToken);
            return Ok(new PagedResponse<WalletLedgerDto>(result.Items.Select(x => x.ToDto()).ToList(),
                result.Page, result.PageSize, result.HasPrevious, result.HasNext,
                result.Total, result.TotalPages));
        }
        return Ok((await ordered.Take(500).ToListAsync(cancellationToken)).Select(x => x.ToDto()).ToList());
    }
}
