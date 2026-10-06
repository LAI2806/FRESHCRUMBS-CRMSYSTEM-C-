using freshcrumbs.CRM.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/subscription")]
    public class TenantSubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptions;

        public TenantSubscriptionController(ISubscriptionService subscriptions)
        {
            _subscriptions = subscriptions;
        }

        // Access (authentication + company match) is enforced by TenantAccessFilter.
        [HttpGet]
        public async Task<IActionResult> Get(int companyId)
        {
            var access = await _subscriptions.CheckAccessAsync(companyId, null);
            var subscription = access.Display;
            var activeUsers = await _subscriptions.CountActiveUsersAsync(companyId);

            return Ok(new
            {
                HasAccess = access.Allowed,
                access.Code,
                access.Message,
                PlanCode = subscription?.PlanCode,
                PlanName = subscription?.PlanName,
                EndDate = subscription == null ? (DateTime?)null : DateTime.SpecifyKind(subscription.EndDate, DateTimeKind.Utc),
                Features = access.Allowed ? subscription!.FeatureList : new List<string>(),
                MaxUsers = subscription?.MaxUsers,
                ActiveUsers = activeUsers,
                BranchingEnabled = subscription?.BranchingEnabled ?? false,
                MaxBranches = subscription?.MaxBranches
            });
        }
    }
}