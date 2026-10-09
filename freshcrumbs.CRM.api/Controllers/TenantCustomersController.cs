using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/customers")]
    public class TenantCustomersController : ControllerBase
    {
        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantCustomersController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(int companyId, bool includeInactive = false)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            IQueryable<Customer> query = tenantDb.Customers
                .AsNoTracking()
                .Include(x => x.DiscountEligibilities);

            if (!includeInactive)
            {
                query = query.Where(x => x.Status == "Active");
            }

            var customers = await query
                .OrderBy(x => x.CustomerId)
                .ToListAsync();

            if (BranchStock.IsBranchingCompany(HttpContext))
            {
                await FillBranchNamesAsync(tenantDb, customers);
            }

            return Ok(customers);

        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCustomerById(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .AsNoTracking()
                .Include(x => x.DiscountEligibilities)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            return Ok(customer);

        }

        // Sales workflow: fast EXACT-code lookup with just what the sale needs (no contact details, no ID numbers).
        // Recording the sale associates the customer with that branch, so no duplicate customer is ever needed.
        [HttpGet("lookup")]
        public async Task<IActionResult> LookupByCode(int companyId, string? code)
        {
            var exact = (code ?? string.Empty).Trim();

            if (exact.Length == 0)
            {
                return BadRequest(new { message = "Enter a customer code." });
            }

            // Customer codes are at most 50 characters: anything longer cannot match.
            if (exact.Length > 50)
            {
                return NotFound(new { message = "No active customer has that code." });
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .AsNoTracking()
                .Where(x => x.CustomerCode == exact && x.Status == "Active")
                .Select(x => new
                {
                    x.CustomerId,
                    x.CustomerCode,
                    x.FirstName,
                    x.LastName,
                    x.Status,
                    x.LoyaltyPoints,
                    DiscountEligibilities = x.DiscountEligibilities
                        .Where(e => e.VerificationStatus == "Verified")
                        .Select(e => new { e.Category, e.VerificationStatus })
                })
                .FirstOrDefaultAsync();

            return customer == null
                ? NotFound(new { message = $"No active customer has the code \"{exact}\"." })
                : Ok(customer);
        }

        // Suggested code for a NEW customer: MAX + 1 over EVERY customer of the company (all branches, inactive
        // included). Only the next number is returned. The unique index on CustomerCode still decides.
        [HttpGet("next-code")]
        public async Task<IActionResult> GetNextCustomerCode(int companyId, CancellationToken ct)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var code = await CloudSyncApplier.NextCodeAsync(tenantDb.Customers.AsNoTracking().Select(x => x.CustomerCode), "CUST-", ct);
            return Ok(new { customerCode = code });
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer(int companyId, Customer customer)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            string? eligibilityError = NormalizeEligibilities(customer.DiscountEligibilities);
            if (eligibilityError != null)
            {
                return BadRequest(eligibilityError);
            }

            string? fieldError = ValidateCustomerFields(customer, null);
            if (fieldError != null)
            {
                return BadRequest(InputRules.Message(fieldError));
            }

            foreach (var eligibility in customer.DiscountEligibilities)
            {
                eligibility.EligibilityId = 0;
                eligibility.CustomerId = 0;
                eligibility.RowGuid = Guid.NewGuid();
                eligibility.Customer = null;
            }

            if (!HttpContext.HasTenantPermission(TenantPermissions.ManageLoyalty))
            {
                customer.LoyaltyPoints = 0;
            }
            else if (customer.LoyaltyPoints < 0 || customer.LoyaltyPoints > InputRules.MaxPoints)
            {
                return BadRequest(InputRules.Message(LoyaltyRangeMessage));
            }

            // System fields are always set by the server, never by the client.
            customer.CustomerId = 0;
            customer.RowGuid = Guid.NewGuid();
            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.Status = "Active";

            // Registration branch (PREMIUM): from the account's assignment, never from the client.
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);

            if (scope.Restricted && scope.BranchId == null)
            {
                return NoBranchAssigned();
            }

            customer.BranchId = await BranchStock.GetRecordingBranchIdAsync(HttpContext, tenantDb);
            customer.BranchNames = null;

            if (await tenantDb.Customers.AnyAsync(c => c.CustomerCode == customer.CustomerCode))
            {
                return DuplicateCode(customer.CustomerCode);
            }

            tenantDb.Customers.Add(customer);

            try
            {
                await tenantDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("CustomerCode") == true)
            {
                // Another device saved the same code a moment earlier.
                return DuplicateCode(customer.CustomerCode);
            }

            return Created(
                $"api/tenant/{companyId}/customers/{customer.CustomerId}",
                customer);
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageCustomers)]
        public async Task<IActionResult> UpdateCustomer(int companyId, int id, Customer updated)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .Include(x => x.DiscountEligibilities)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            string? eligibilityError = NormalizeEligibilities(updated.DiscountEligibilities);
            if (eligibilityError != null)
            {
                return BadRequest(eligibilityError);
            }

            string? fieldError = ValidateCustomerFields(updated, customer);
            if (fieldError != null)
            {
                return BadRequest(InputRules.Message(fieldError));
            }

            string? status = InputRules.OneOf(updated.Status, "Active", "Inactive");
            if (status == null)
            {
                return BadRequest(InputRules.Message("Status must be Active or Inactive."));
            }

            updated.Status = status;

            if (await tenantDb.Customers.AnyAsync(c => c.CustomerId != id && c.CustomerCode == updated.CustomerCode))
            {
                return DuplicateCode(updated.CustomerCode);
            }

            // Changing the status is a deactivate / reactivate: same rule as those endpoints.
            if (!string.Equals(customer.Status, updated.Status, StringComparison.OrdinalIgnoreCase)
                && !await CanChangeStatusAsync(tenantDb, customer))
            {
                return NotRegistrationBranch();
            }

            customer.CustomerCode = updated.CustomerCode;
            customer.FirstName = updated.FirstName;
            customer.LastName = updated.LastName;
            customer.Email = updated.Email;
            customer.ContactNo = updated.ContactNo;
            customer.Address = updated.Address;
            customer.Status = updated.Status;

            if (HttpContext.HasTenantPermission(TenantPermissions.ManageLoyalty) && customer.LoyaltyPoints != updated.LoyaltyPoints)
            {
                if (updated.LoyaltyPoints < 0 || updated.LoyaltyPoints > InputRules.MaxPoints)
                {
                    return BadRequest(InputRules.Message(LoyaltyRangeMessage));
                }

                customer.LoyaltyPoints = updated.LoyaltyPoints;
            }

            ApplyEligibilities(tenantDb, customer, updated.DiscountEligibilities);

            try
            {
                await tenantDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("CustomerCode") == true)
            {
                return DuplicateCode(updated.CustomerCode);
            }

            return Ok(customer);
        }

        private const string LoyaltyRangeMessage = "Loyalty points must be between 0 and 999,999.";

        // Field rules (database limits). Names are free text: any letters, spaces, hyphens, apostrophes and periods.
        // Same CustomerCode / contact / email rules as the desktop form. existing = the stored record on an edit:
        // an old contact number in another format is kept as it is unless it is changed.
        private static string? ValidateCustomerFields(Customer customer, Customer? existing)
        {
            string? codeError = InputRules.Text(customer.CustomerCode, "Customer code", 50, true, out var code);
            string? firstNameError = InputRules.Text(customer.FirstName, "First name", 100, true, out var firstName);
            string? lastNameError = InputRules.Text(customer.LastName, "Last name", 100, true, out var lastName);
            string? emailError = InputRules.Text(customer.Email, "Email", 150, false, out var email);
            string? contactError = InputRules.Text(customer.ContactNo, "Contact number", 20, false, out var contact);
            string? addressError = InputRules.Text(customer.Address, "Address", 300, false, out var address);
            string? error = codeError ?? firstNameError ?? lastNameError ?? emailError ?? contactError ?? addressError;

            if (error != null)
            {
                return error;
            }

            if (email.Length > 0 && !InputRules.IsValidEmail(email))
            {
                return "Please enter a valid email address.";
            }

            if (contact.Length > 0 && contact != existing?.ContactNo && !InputRules.IsValidPhilippineMobile(contact))
            {
                return "Contact number must be exactly 11 digits (e.g. 09171234567).";
            }

            customer.CustomerCode = code;
            customer.FirstName = firstName;
            customer.LastName = lastName;
            customer.Email = email;
            customer.ContactNo = contact;
            customer.Address = address;
            return null;
        }

        // Senior/PWD eligibility: an operational cashier task, so STAFF may add or edit it too (every plan), using the
        // same categories, fields and validation as the full customer edit. Nothing else about the customer changes.
        [HttpPut("{id:int}/eligibilities")]
        [RequireTenantPermission(TenantPermissions.ProcessSeniorPwdDiscount)]
        public async Task<IActionResult> UpdateEligibilities(int companyId, int id, List<CustomerDiscountEligibility> eligibilities)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers
                .Include(x => x.DiscountEligibilities)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            string? eligibilityError = NormalizeEligibilities(eligibilities);
            if (eligibilityError != null)
            {
                return BadRequest(eligibilityError);
            }

            ApplyEligibilities(tenantDb, customer, eligibilities);

            await tenantDb.SaveChangesAsync();

            return Ok(customer);
        }

        // Same merge as before: categories not sent are removed, new ones added, existing ones updated in place.
        private static void ApplyEligibilities(
            TenantCrmDbContext tenantDb,
            Customer customer,
            ICollection<CustomerDiscountEligibility> incomingList)
        {
            var eligibilitiesToRemove = customer.DiscountEligibilities
                .Where(existing => !incomingList.Any(u =>
                    string.Equals(u.Category, existing.Category, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var eligibility in eligibilitiesToRemove)
            {
                customer.DiscountEligibilities.Remove(eligibility);
                tenantDb.CustomerDiscountEligibilities.Remove(eligibility);
            }

            foreach (var incoming in incomingList)
            {
                var existing = customer.DiscountEligibilities.FirstOrDefault(e =>
                    string.Equals(e.Category, incoming.Category, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    customer.DiscountEligibilities.Add(new CustomerDiscountEligibility
                    {
                        Category = incoming.Category,
                        IdNumber = incoming.IdNumber,
                        VerificationStatus = incoming.VerificationStatus
                    });
                }
                else
                {
                    existing.IdNumber = incoming.IdNumber;
                    existing.VerificationStatus = incoming.VerificationStatus;
                }
            }
        }

        // Every branch a customer is associated with: where they registered plus where they have completed sales.
        private static async Task FillBranchNamesAsync(TenantCrmDbContext tenantDb, List<Customer> customers)
        {
            var names = await tenantDb.Branches.AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

            if (names.Count == 0 || customers.Count == 0)
            {
                return;
            }

            var ids = customers.Select(c => c.CustomerId).ToList();

            var saleBranches = (await tenantDb.SalesTransactions.AsNoTracking()
                    .Where(s => !s.IsDeleted && s.Status == "Completed" && s.BranchId != null && ids.Contains(s.CustomerId))
                    .Select(s => new { s.CustomerId, BranchId = s.BranchId!.Value })
                    .Distinct()
                    .ToListAsync())
                .ToLookup(x => x.CustomerId, x => x.BranchId);

            foreach (var customer in customers)
            {
                var branchIds = saleBranches[customer.CustomerId].ToList();

                if (customer.BranchId != null)
                {
                    branchIds.Insert(0, customer.BranchId.Value);
                }

                var labels = branchIds
                    .Distinct()
                    .Where(names.ContainsKey)
                    .Select(b => names[b])
                    .ToList();

                customer.BranchNames = labels.Count == 0 ? null : string.Join(", ", labels);
            }
        }

        private ObjectResult NoBranchAssigned()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "NoBranchAssigned",
                message = "You are not assigned to a branch. Ask your administrator to assign you to a branch."
            });
        }

        [HttpDelete("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageCustomers)]
        public async Task<IActionResult> DeleteCustomer(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            if (!await CanChangeStatusAsync(tenantDb, customer))
            {
                return NotRegistrationBranch();
            }

            customer.Status = "Inactive";
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:int}/reactivate")]
        [RequireTenantPermission(TenantPermissions.ManageCustomers)]
        public async Task<IActionResult> ReactivateCustomer(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var customer = await tenantDb.Customers.FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                return NotFound($"Customer with id {id} not found.");
            }

            if (!await CanChangeStatusAsync(tenantDb, customer))
            {
                return NotRegistrationBranch();
            }

            customer.Status = "Active";
            await tenantDb.SaveChangesAsync();

            return Ok(customer);
        }

        // Deactivating / reactivating affects the customer at EVERY branch, so a branch-restricted MANAGER may only
        // do it for customers registered at their own branch (a sale at their branch is not enough). ADMIN: any.
        private async Task<bool> CanChangeStatusAsync(TenantCrmDbContext tenantDb, Customer customer)
        {
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);
            return !scope.Restricted || (scope.BranchId != null && customer.BranchId == scope.BranchId);
        }

        private static ObjectResult NotRegistrationBranch()
        {
            return BranchStock.OtherBranch("Only an administrator or the manager of the branch where this customer was registered can deactivate or reactivate them.");
        }

        private ObjectResult DuplicateCode(string customerCode)
        {
            return Conflict(new
            {
                code = "DuplicateCustomerCode",
                message = $"Customer code \"{customerCode}\" already exists. Please use the existing customer: enter this code when recording the sale."
            });
        }

        // Validates each eligibility record and rejects duplicate categories for the same customer.
        // Normalizes Category/VerificationStatus to their canonical casing in place.

        private static string? NormalizeEligibilities(ICollection<CustomerDiscountEligibility>? eligibilities)
        {
            if (eligibilities == null || eligibilities.Count == 0)
            {
                return null;
            }

            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var eligibility in eligibilities)
            {
                var category = Array.Find(CustomerDiscountEligibility.Categories,
                    c => string.Equals(c, eligibility.Category?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (category == null)
                {
                    return $"\"{eligibility.Category}\" is not a valid discount eligibility category.";
                }

                if (!seenCategories.Add(category))
                {
                    return $"Duplicate discount eligibility \"{category}\" for this customer.";
                }

                var status = Array.Find(CustomerDiscountEligibility.VerificationStatuses,
                    s => string.Equals(s, eligibility.VerificationStatus?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (status == null)
                {
                    return $"\"{eligibility.VerificationStatus}\" is not a valid verification status for {category}.";
                }

                var idNumber = eligibility.IdNumber?.Trim() ?? string.Empty;

                if (idNumber.Length == 0)
                {
                    return $"An ID number is required for {category} eligibility.";
                }

                if (idNumber.Length > 50)
                {
                    return $"The ID number for {category} eligibility cannot exceed 50 characters.";
                }

                eligibility.Category = category;
                eligibility.VerificationStatus = status;
                eligibility.IdNumber = idNumber;
            }

            return null;
        }
    }
}