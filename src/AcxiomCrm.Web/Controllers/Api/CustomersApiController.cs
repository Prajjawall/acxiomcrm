using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Application.Common;
using AcxiomCrm.Application.Customers;
using AcxiomCrm.Domain.Enums;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCrm.Web.Controllers.Api;

[Authorize(Policy = Permissions.CustomersView)]
public class CustomersApiController : ApiControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICurrentUser _currentUser;

    public CustomersApiController(ICustomerService customerService, ICurrentUser currentUser)
    {
        _customerService = customerService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] CustomerStatus? status,
        [FromQuery] string? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new CustomerFilter(search, status, ownerId);
        var pageQuery = new PageQuery(page, pageSize, sort, desc);
        var result = await _customerService.SearchAsync(filter, pageQuery, ct);

        var dtos = result.Items.Select(MapFromListItem).ToList();
        return Ok(new PagedDto<CustomerDto>(dtos, result.Page, result.PageSize, result.Total));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await _customerService.GetAsync(id, ct);
        return ToActionResult(result, map: c => MapFromDetail(c));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersCreate)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var ownerId = _currentUser.Role == Roles.SalesExecutive
            ? _currentUser.UserId
            : request.OwnerId;

        var command = new CreateCustomerCommand(
            request.Name,
            request.CompanyName,
            request.Email,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.Status,
            ownerId,
            request.Notes);

        var result = await _customerService.CreateAsync(command, ct);
        return ToActionResult(
            result,
            map: c => MapFromDetail(c),
            location: c => $"/api/customers/{c.Id}");
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CustomersEdit)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var ownerId = _currentUser.Role == Roles.SalesExecutive
            ? _currentUser.UserId
            : request.OwnerId;

        var command = new UpdateCustomerCommand(
            request.Name,
            request.CompanyName,
            request.Email,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.Status,
            ownerId,
            request.Notes,
            []);

        var result = await _customerService.UpdateAsync(id, command, ct);
        return ToActionResult(result, map: c => MapFromDetail(c));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CustomersDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _customerService.DeleteAsync(id, ct);
        return ToActionResult(result);
    }

    private static CustomerDto MapFromDetail(CustomerDetail detail)
    {
        return new CustomerDto(
            detail.Id,
            detail.Code,
            detail.Name,
            detail.CompanyName,
            detail.Email,
            detail.Phone,
            detail.Address,
            detail.City,
            detail.State,
            detail.Status,
            new OwnerSummaryDto(detail.OwnerId, detail.OwnerName),
            detail.Notes,
            detail.CreatedDate,
            detail.ModifiedDate);
    }

    private static CustomerDto MapFromListItem(CustomerListItem item)
    {
        return new CustomerDto(
            item.Id,
            item.Code,
            item.Name,
            item.CompanyName,
            item.Email,
            item.Phone,
            null,
            null,
            null,
            item.Status,
            new OwnerSummaryDto(item.OwnerId, item.OwnerName),
            null,
            item.CreatedDate,
            null);
    }
}
