using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.api.Services.Sync;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Controllers
{
    // PREMIUM (Branching) only: TenantFeatureMap maps "branches" to the Branching feature, so every call here
    // is refused with FeatureNotIncluded for Basic / Standard companies.
    [ApiController]
    [Route("api/tenant/{companyId:int}/branches")]
    public class TenantBranchesController : ControllerBase
    {
        private const string Active = "Active";
        private const string Inactive = "Inactive";

        private readonly ITenantDbContextFactory _tenantFactory;
        private readonly ISubscriptionService _subscriptions;
        private readonly SyncOptions _syncOptions;

        public TenantBranchesController(
            ITenantDbContextFactory tenantFactory,
            ISubscriptionService subscriptions,
            SyncOptions syncOptions)
        {
            _tenantFactory = tenantFactory;
            _subscriptions = subscriptions;
            _syncOptions = syncOptions;
        }

        // Address and ManagerUserId are optional (nullable so an empty value is not rejected before validation).
        // ManagerUserId = null means the branch has no manager.
        public class BranchRequest
        {
            public string BranchName { get; set; } = string.Empty;
            public string? Address { get; set; }
            public string Status { get; set; } = Active;
            public string? ManagerUserId { get; set; }
        }

        public class StockRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }

            // In, Out or Allocate (move unallocated company stock into the branch; ADMIN only).
            public string Action { get; set; } = string.Empty;
        }

        public class AssignmentRequest
        {
            public int? BranchId { get; set; }
        }

        [HttpGet]
        [RequireTenantPermission(TenantPermissions.ViewBranchStock)]
        public async Task<IActionResult> GetBranches(int companyId, [FromServices] IServiceProvider services)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var branches = await tenantDb.Branches.AsNoTracking()
                .OrderBy(b => b.BranchName)
                .Select(b => new { b.BranchId, b.BranchName, b.Address, b.Status })
                .ToListAsync();

            var users = await GetCompanyUsersAsync(companyId, services);
            var managers = await GetBranchManagersAsync(tenantDb, users);

            return Ok(branches.Select(b =>
            {
                var manager = managers.TryGetValue(b.BranchId, out var m) ? m : null;

                return new
                {
                    b.BranchId,
                    b.BranchName,
                    b.Address,
                    b.Status,
                    ManagerUserId = manager?.Id,
                    ManagerName = manager == null ? null : DisplayName(manager)
                };
            }));
        }

        [HttpGet("me")]
        [RequireTenantPermission(TenantPermissions.ViewBranchStock)]
        public async Task<IActionResult> GetMyBranch(int companyId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var branch = await BranchStock.GetAssignedBranchAsync(tenantDb, User);

            return Ok(new
            {
                BranchId = branch?.BranchId,
                BranchName = branch?.BranchName
            });
        }

        [HttpPost]
        [RequireTenantPermission(TenantPermissions.ManageBranches)]
        public async Task<IActionResult> CreateBranch(int companyId, BranchRequest request, [FromServices] IServiceProvider services)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var error = await ValidateBranchAsync(tenantDb, companyId, request, null);
            if (error != null)
            {
                return error;
            }

            var users = new List<CompanyUserDto>();
            CompanyUserDto? manager = null;

            if (!string.IsNullOrWhiteSpace(request.ManagerUserId))
            {
                users = await GetCompanyUsersAsync(companyId, services);
                manager = FindSelectableManager(users, request.ManagerUserId);

                if (manager == null)
                {
                    return BadRequest(new { message = "The selected manager must be an active MANAGER account of this company." });
                }
            }

            var branch = new Branch
            {
                BranchName = request.BranchName.Trim(),
                Address = (request.Address ?? string.Empty).Trim(),
                Status = NormalizeStatus(request.Status)
            };

            tenantDb.Branches.Add(branch);

            // Same save as the branch, so the branch never exists half-configured.
            if (manager != null)
            {
                await ApplyAssignmentAsync(tenantDb, users, manager, branch);
            }

            await tenantDb.SaveChangesAsync();

            return Ok(new { branch.BranchId, branch.BranchName, branch.Address, branch.Status });
        }

        [HttpPut("{id:int}")]
        [RequireTenantPermission(TenantPermissions.ManageBranches)]
        public async Task<IActionResult> UpdateBranch(int companyId, int id, BranchRequest request, [FromServices] IServiceProvider services)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var branch = await tenantDb.Branches.FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return NotFound(new { message = "Branch not found." });
            }

            var error = await ValidateBranchAsync(tenantDb, companyId, request, branch);
            if (error != null)
            {
                return error;
            }

            var users = await GetCompanyUsersAsync(companyId, services);
            CompanyUserDto? manager = null;

            if (!string.IsNullOrWhiteSpace(request.ManagerUserId))
            {
                manager = FindSelectableManager(users, request.ManagerUserId);

                if (manager == null)
                {
                    return BadRequest(new { message = "The selected manager must be an active MANAGER account of this company." });
                }
            }

            branch.BranchName = request.BranchName.Trim();
            branch.Address = (request.Address ?? string.Empty).Trim();
            branch.Status = NormalizeStatus(request.Status);

            var current = (await GetBranchManagersAsync(tenantDb, users)).TryGetValue(branch.BranchId, out var m) ? m : null;

            if (manager != null && manager.Id != current?.Id)
            {
                // Also unassigns the branch's previous manager (one manager per branch).
                await ApplyAssignmentAsync(tenantDb, users, manager, branch);
            }
            else if (manager == null && current != null)
            {
                // "(None)": the previous manager stays an employee but is no longer assigned to this branch.
                await ApplyAssignmentAsync(tenantDb, users, current, null);
            }

            await tenantDb.SaveChangesAsync();

            return Ok(new { branch.BranchId, branch.BranchName, branch.Address, branch.Status });
        }

        [HttpGet("{id:int}/inventory")]
        [RequireTenantPermission(TenantPermissions.ViewBranchStock)]
        public async Task<IActionResult> GetInventory(int companyId, int id)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var branch = await tenantDb.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return NotFound(new { message = "Branch not found." });
            }

            if (!await IsOwnBranchOrAdminAsync(tenantDb, id))
            {
                return OtherBranch();
            }

            var stock = await tenantDb.BranchInventories.AsNoTracking()
                .Where(x => x.BranchId == id)
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            var products = await tenantDb.Products.AsNoTracking()
                .Where(p => p.Status == Active)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            var rows = products.Select(p =>
            {
                int quantity = stock.TryGetValue(p.ProductId, out var q) ? q : 0;

                return new
                {
                    p.ProductId,
                    p.ProductCode,
                    p.ProductName,
                    p.Category,
                    p.Price,
                    Quantity = quantity,
                    p.ReorderLevel,
                    StockLevel = quantity <= 0 ? "Out of Stock" : quantity <= p.ReorderLevel ? "Low Stock" : "In Stock"
                };
            });

            return Ok(rows);
        }

        [HttpPost("{id:int}/stock")]
        [RequireTenantPermission(TenantPermissions.ManageBranchStock)]
        public async Task<IActionResult> ChangeStock(int companyId, int id, StockRequest request)
        {
            var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();

            if (action != "in" && action != "out" && action != "allocate")
            {
                return BadRequest(new { message = "Action must be In, Out or Allocate." });
            }

            if (request.Quantity <= 0)
            {
                return BadRequest(new { message = "Quantity must be greater than 0." });
            }

            if (action == "allocate" && !BranchStock.IsAdmin(HttpContext))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "RoleNotAllowed",
                    message = "Only an administrator can allocate unallocated stock to a branch."
                });
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var branch = await tenantDb.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return NotFound(new { message = "Branch not found." });
            }

            if (!await IsOwnBranchOrAdminAsync(tenantDb, id))
            {
                return OtherBranch();
            }

            if (branch.Status != Active)
            {
                return Conflict(new { message = $"Branch \"{branch.BranchName}\" is inactive." });
            }

            var product = await tenantDb.Products.FirstOrDefaultAsync(p => p.ProductId == request.ProductId);

            if (product == null || product.Status != Active)
            {
                return BadRequest(new { message = "The product does not exist or is inactive." });
            }

            var inventory = await BranchStock.FindAsync(tenantDb, id, product.ProductId);
            int branchQuantity = inventory?.Quantity ?? 0;

            if (action == "out" && branchQuantity < request.Quantity)
            {
                return Conflict(new { message = $"Not enough stock of {product.ProductName} at {branch.BranchName}. Available: {branchQuantity}." });
            }

            if (action == "allocate")
            {
                int allocated = await tenantDb.BranchInventories
                    .Where(x => x.ProductId == product.ProductId)
                    .SumAsync(x => x.Quantity);

                int unallocated = product.Quantity - allocated;

                if (request.Quantity > unallocated)
                {
                    return Conflict(new { message = $"Only {Math.Max(unallocated, 0)} unallocated unit(s) of {product.ProductName} are available." });
                }
            }

            if (inventory == null)
            {
                inventory = new BranchInventory { BranchId = id, ProductId = product.ProductId, Quantity = 0 };
                tenantDb.BranchInventories.Add(inventory);
            }

            switch (action)
            {
                case "in":
                    inventory.Quantity += request.Quantity;
                    product.Quantity += request.Quantity;
                    break;

                case "out":
                    inventory.Quantity -= request.Quantity;
                    product.Quantity -= request.Quantity;
                    break;

                case "allocate":
                    // The units already belong to the company total; they are only assigned to this branch.
                    inventory.Quantity += request.Quantity;
                    break;
            }

            await tenantDb.SaveChangesAsync();

            int allocatedAfter = await tenantDb.BranchInventories
                .Where(x => x.ProductId == product.ProductId)
                .SumAsync(x => x.Quantity);

            return Ok(new
            {
                BranchId = id,
                product.ProductId,
                BranchQuantity = inventory.Quantity,
                TotalQuantity = product.Quantity,
                UnallocatedQuantity = product.Quantity - allocatedAfter
            });
        }

        [HttpGet("products/{productId:int}")]
        [RequireTenantPermission(TenantPermissions.ManageBranches)]
        public async Task<IActionResult> GetProductStock(int companyId, int productId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var product = await tenantDb.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            var stock = await tenantDb.BranchInventories.AsNoTracking()
                .Where(x => x.ProductId == productId)
                .ToDictionaryAsync(x => x.BranchId, x => x.Quantity);

            var branches = await tenantDb.Branches.AsNoTracking()
                .OrderBy(b => b.BranchName)
                .ToListAsync();

            int allocated = stock.Values.Sum();

            return Ok(new
            {
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.ReorderLevel,
                TotalQuantity = product.Quantity,
                AllocatedQuantity = allocated,
                UnallocatedQuantity = product.Quantity - allocated,
                Branches = branches.Select(b => new
                {
                    b.BranchId,
                    b.BranchName,
                    b.Status,
                    Quantity = stock.TryGetValue(b.BranchId, out var q) ? q : 0
                })
            });
        }

        [HttpGet("assignments")]
        [RequireTenantPermission(TenantPermissions.ManageBranches)]
        public async Task<IActionResult> GetAssignments(int companyId, [FromServices] IServiceProvider services)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var users = await GetCompanyUsersAsync(companyId, services);

            var assignments = await tenantDb.BranchAssignments.AsNoTracking()
                .ToDictionaryAsync(a => a.UserId, a => a.BranchId);

            var branchNames = await tenantDb.Branches.AsNoTracking()
                .ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

            var rows = users.Select(u =>
            {
                int? branchId = assignments.TryGetValue(u.Id, out var b) ? b : null;

                return new
                {
                    UserId = u.Id,
                    u.UserName,
                    u.FullName,
                    u.Role,
                    u.Status,
                    BranchId = branchId,
                    BranchName = branchId != null && branchNames.TryGetValue(branchId.Value, out var name) ? name : null
                };
            });

            return Ok(rows);
        }

        [HttpPut("assignments/{userId}")]
        [RequireTenantPermission(TenantPermissions.ManageBranches)]
        public async Task<IActionResult> SetAssignment(
            int companyId,
            string userId,
            AssignmentRequest request,
            [FromServices] IServiceProvider services)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var users = await GetCompanyUsersAsync(companyId, services);
            var user = users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = users.Count == 0
                        ? "The company's accounts are not available yet. Sync once while online, then try again."
                        : "This account does not belong to this company."
                });
            }

            Branch? branch = null;

            if (request.BranchId != null)
            {
                branch = await tenantDb.Branches
                    .FirstOrDefaultAsync(b => b.BranchId == request.BranchId.Value && b.Status == Active);

                if (branch == null)
                {
                    return BadRequest(new { message = "The selected branch does not exist or is inactive." });
                }
            }

            await ApplyAssignmentAsync(tenantDb, users, user, branch);
            await tenantDb.SaveChangesAsync();

            return NoContent();
        }

        // The ONLY place a BranchAssignment changes (branch form, Employees dialog and User Management all come here).
        // BranchAssignment stays the single source of truth; a branch's manager is the MANAGER-role account assigned
        // to it. Assigning a MANAGER to a branch unassigns any other MANAGER there, so a branch has at most one.
        private static async Task ApplyAssignmentAsync(
            TenantCrmDbContext tenantDb,
            List<CompanyUserDto> users,
            CompanyUserDto user,
            Branch? branch)
        {
            var assignment = await tenantDb.BranchAssignments.FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (assignment == null)
            {
                if (branch == null)
                {
                    return;
                }

                assignment = new BranchAssignment { UserId = user.Id };
                tenantDb.BranchAssignments.Add(assignment);
            }

            assignment.UserName = user.UserName;

            if (branch == null)
            {
                assignment.Branch = null;
                assignment.BranchId = null;
                return;
            }

            // A branch created in this same save has no id yet; the navigation links them on save.
            assignment.Branch = branch;

            if (branch.BranchId == 0)
            {
                return;
            }

            assignment.BranchId = branch.BranchId;

            if (!IsManager(user))
            {
                return;
            }

            var otherManagerIds = users
                .Where(u => u.Id != user.Id && IsManager(u))
                .Select(u => u.Id)
                .ToList();

            var previous = await tenantDb.BranchAssignments
                .Where(a => a.BranchId == branch.BranchId && otherManagerIds.Contains(a.UserId))
                .ToListAsync();

            foreach (var other in previous)
            {
                other.Branch = null;
                other.BranchId = null;
            }
        }

        // Branch id -> its manager: the MANAGER-role account assigned to it (most recent assignment if data from
        // before this rule ever had two).
        private static async Task<Dictionary<int, CompanyUserDto>> GetBranchManagersAsync(
            TenantCrmDbContext tenantDb,
            List<CompanyUserDto> users)
        {
            var managers = users.Where(IsManager).ToDictionary(u => u.Id);

            if (managers.Count == 0)
            {
                return new Dictionary<int, CompanyUserDto>();
            }

            var managerIds = managers.Keys.ToList();

            var assignments = await tenantDb.BranchAssignments.AsNoTracking()
                .Where(a => a.BranchId != null && managerIds.Contains(a.UserId))
                .OrderByDescending(a => a.BranchAssignmentId)
                .Select(a => new { BranchId = a.BranchId!.Value, a.UserId })
                .ToListAsync();

            return assignments
                .GroupBy(a => a.BranchId)
                .ToDictionary(g => g.Key, g => managers[g.First().UserId]);
        }

        private static CompanyUserDto? FindSelectableManager(List<CompanyUserDto> users, string? userId)
        {
            return users.FirstOrDefault(u => u.Id == userId
                && IsManager(u)
                && string.Equals(u.Status, Active, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsManager(CompanyUserDto user)
        {
            return TenantRoles.Normalize(user.Role) == TenantRoles.Manager;
        }

        private static string DisplayName(CompanyUserDto user)
        {
            return string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
        }

        // Cloud: the master database. Desktop (Local mode): the company snapshot the cloud supplied at the last sync.
        private async Task<List<CompanyUserDto>> GetCompanyUsersAsync(int companyId, IServiceProvider services)
        {
            if (_syncOptions.IsLocal)
            {
                var cache = services.GetRequiredService<LocalAccessCache>();
                return cache.Read(d => d.Companies.TryGetValue(companyId, out var c) ? c.Snapshot.Users.ToList() : new List<CompanyUserDto>());
            }

            var masterDb = services.GetRequiredService<MasterCrmDbContext>();

            var users = await masterDb.Users.AsNoTracking()
                .Where(u => u.TenantId == companyId)
                .OrderBy(u => u.UserName)
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.Role, u.Status })
                .ToListAsync();

            return users.Select(u => new CompanyUserDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                Role = u.Role,
                Status = u.Status
            }).ToList();
        }

        private async Task<IActionResult?> ValidateBranchAsync(
            TenantCrmDbContext tenantDb,
            int companyId,
            BranchRequest request,
            Branch? existing)
        {
            var name = (request.BranchName ?? string.Empty).Trim();

            if (name.Length == 0 || name.Length > 100)
            {
                return BadRequest(new { message = "Branch name is required (up to 100 characters)." });
            }

            if ((request.Address ?? string.Empty).Trim().Length > 300)
            {
                return BadRequest(new { message = "Address can be up to 300 characters." });
            }

            var rawStatus = (request.Status ?? Active).Trim();

            if (!string.Equals(rawStatus, Active, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rawStatus, Inactive, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Status must be Active or Inactive." });
            }

            var status = NormalizeStatus(rawStatus);
            int? excludeId = existing?.BranchId;

            var otherBranches = await tenantDb.Branches.AsNoTracking()
                .Where(b => excludeId == null || b.BranchId != excludeId)
                .Select(b => new { b.BranchName, b.Status })
                .ToListAsync();

            if (otherBranches.Any(b => string.Equals(b.BranchName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new { message = $"A branch named \"{name}\" already exists." });
            }

            bool becomesActive = status == Active && (existing == null || existing.Status != Active);

            if (becomesActive)
            {
                var subscription = await _subscriptions.GetCurrentAsync(companyId);
                int? maxBranches = subscription?.MaxBranches;
                int activeBranches = otherBranches.Count(b => b.Status == Active);

                if (maxBranches != null && activeBranches >= maxBranches.Value)
                {
                    return Conflict(new { message = $"Your plan allows up to {maxBranches.Value} active branch(es)." });
                }
            }

            return null;
        }

        private static string NormalizeStatus(string? status)
        {
            return string.Equals(status?.Trim(), Inactive, StringComparison.OrdinalIgnoreCase) ? Inactive : Active;
        }

        private async Task<bool> IsOwnBranchOrAdminAsync(TenantCrmDbContext tenantDb, int branchId)
        {
            if (BranchStock.IsAdmin(HttpContext))
            {
                return true;
            }

            var assigned = await BranchStock.GetAssignedBranchAsync(tenantDb, User);
            return assigned != null && assigned.BranchId == branchId;
        }

        private ObjectResult OtherBranch()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "OtherBranch",
                message = "You can only work with your assigned branch."
            });
        }
    }
}
