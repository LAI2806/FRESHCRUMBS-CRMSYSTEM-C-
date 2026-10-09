using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.api.Services;
using freshcrumbs.CRM.infrastructure.data;
using freshcrumbs.CRM.infrastructure.services;

namespace freshcrumbs.CRM.api.Controllers
{
    [ApiController]
    [Route("api/tenant/{companyId:int}/reports")]
    public class TenantReportsController : ControllerBase
    {
        private const string InvalidMonthMessage = "Year and month must form a valid calendar month.";
        private const string InvalidDateRangeMessage = "Start date and end date must both be provided, and the start date cannot be later than the end date.";
        private const string DateRangeTooLargeMessage = "The selected date range cannot exceed 10 years.";
        private const string ComplaintType = "Complaint";
        private const string ResolvedStatus = "Resolved";
        private const string PendingStatus = "Pending";
        private const string CancellationType = "Cancellation";

        private static readonly string[] FeedbackCategoryOrder =
        {
            "Customer Service",
            "Product Quality",
            "Product Availability",
            "Orders",
            "Pricing and Payments",
            "Packaging"
        };

        private readonly ITenantDbContextFactory _tenantFactory;

        public TenantReportsController(ITenantDbContextFactory tenantFactory)
        {
            _tenantFactory = tenantFactory;
        }

        private class ReportInsight
        {
            public string Category { get; set; } = string.Empty;
            public string DataSummary { get; set; } = string.Empty;
            public string Analysis { get; set; } = string.Empty;
            public string Insight { get; set; } = string.Empty;
            public string SuggestedAction { get; set; } = string.Empty;
            public string ActionTarget { get; set; } = string.Empty;
            public string ActionLabel { get; set; } = string.Empty;
        }

        private class ChartPoint
        {
            public string Label { get; set; } = string.Empty;

            // Set only for daily time-series points, so charts can plot a real date axis.
            public DateTime? Date { get; set; }

            public double Value { get; set; }
        }

        private record SaleRow(int CustomerId, int? PromotionId, DateTime Date, decimal FinalAmount, decimal DiscountAmount);

        private record ItemRow(int ProductId, string ProductName, string Category, int Quantity, decimal Subtotal);

        private record PromotionRow(int PromotionId, string Name, string Status);

        private record FeedbackRow(string Type, string Category, string Status, DateTime Date);

        private record InquiryRow(string Type, string Status, DateTime? RespondedAt, DateTime Date);

        private record StockRow(int ProductId, string Name, int Quantity, int ReorderLevel);

        private sealed class MonthReport
        {
            private readonly TenantCrmDbContext _db;
            private List<SaleRow>? _sales;
            private List<ItemRow>? _items;
            private List<PromotionRow>? _promotions;
            private List<FeedbackRow>? _feedback;
            private List<InquiryRow>? _inquiries;
            private List<StockRow>? _activeProducts;

            // branchId: null = company-wide; a PREMIUM MANAGER / STAFF passes their branch (NoBranch when not assigned = no data).
            // Purchase-based figures then use that branch's sales; the customer records themselves stay shared.
            private readonly int? _branchId;

            public MonthReport(TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
            {
                _db = db;
                Start = start;
                End = end;
                _branchId = branchId;
            }

            public DateTime Start { get; }

            public DateTime End { get; }

            public bool IsCalendarMonth => Start.Day == 1 && End == Start.AddMonths(1);

            public DateTime PreviousStart => IsCalendarMonth ? Start.AddMonths(-1) : Start - (End - Start);

            public string Label => FormatPeriod(Start, End.AddDays(-1));

            public string PreviousLabel => FormatPeriod(PreviousStart, Start.AddDays(-1));

            public string ThisPeriod => IsCalendarMonth ? "this month" : "this period";

            private static string FormatPeriod(DateTime first, DateTime last)
            {
                bool calendarMonth = first.Day == 1 && last.Date == first.AddMonths(1).AddDays(-1).Date;

                if (calendarMonth)
                {
                    return first.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
                }

                if (first.Date == last.Date)
                {
                    return first.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
                }

                return first.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) + " - " +
                       last.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            }

            public async Task<List<SaleRow>> GetSalesAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                return _sales ??= await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate >= start &&
                                x.TransactionDate < end &&
                                (branchId == null || x.BranchId == branchId))
                    .Select(x => new SaleRow(x.CustomerId, x.PromotionId, x.TransactionDate, x.FinalAmount, x.DiscountAmount))
                    .ToListAsync();
            }

            public async Task<List<ItemRow>> GetItemsAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                return _items ??= await _db.TransactionItems
                    .AsNoTracking()
                    .Where(ti => ti.SalesTransaction != null &&
                                 !ti.SalesTransaction.IsDeleted &&
                                 ti.SalesTransaction.Status == "Completed" &&
                                 ti.SalesTransaction.TransactionDate >= start &&
                                 ti.SalesTransaction.TransactionDate < end &&
                                 (branchId == null || ti.SalesTransaction.BranchId == branchId))
                    .Select(ti => new ItemRow(
                        ti.ProductId,
                        ti.Product != null ? ti.Product.ProductName : string.Empty,
                        ti.Product != null ? ti.Product.Category : string.Empty,
                        ti.Quantity,
                        ti.Subtotal))
                    .ToListAsync();
            }

            // Branch scope: company-wide promotions plus that branch's own promotions.
            public async Task<List<PromotionRow>> GetPromotionsAsync()
            {
                var branchId = _branchId;

                return _promotions ??= await _db.Promotions
                    .AsNoTracking()
                    .Where(p => branchId != NoBranch && (branchId == null || p.BranchId == null || p.BranchId == branchId))
                    .Select(p => new PromotionRow(p.PromotionId, p.PromotionName, p.Status))
                    .ToListAsync();
            }

            public async Task<List<FeedbackRow>> GetFeedbackAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                return _feedback ??= await _db.Feedbacks
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end &&
                                (branchId == null || x.BranchId == branchId))
                    .Select(x => new FeedbackRow(x.Type, x.Category, x.Status, x.DateSubmitted))
                    .ToListAsync();
            }

            public async Task<List<InquiryRow>> GetInquiriesAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                return _inquiries ??= await _db.Inquiries
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end &&
                                (branchId == null || x.BranchId == branchId))
                    .Select(x => new InquiryRow(x.Type, x.Status, x.RespondedAt, x.DateSubmitted))
                    .ToListAsync();
            }

            // Branch scope: the stock held at that branch (BranchInventory), as in the Inventory report.
            public async Task<List<StockRow>> GetActiveProductsAsync()
            {
                if (_activeProducts != null)
                {
                    return _activeProducts;
                }

                if (_branchId == NoBranch)
                {
                    return _activeProducts = new List<StockRow>();
                }

                var products = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.Status == "Active")
                    .Select(p => new StockRow(p.ProductId, p.ProductName, p.Quantity, p.ReorderLevel))
                    .ToListAsync();

                if (_branchId != null)
                {
                    int branchId = _branchId.Value;

                    var branchStock = await _db.BranchInventories
                        .AsNoTracking()
                        .Where(i => i.BranchId == branchId)
                        .ToDictionaryAsync(i => i.ProductId, i => i.Quantity);

                    products = products
                        .Select(p => p with { Quantity = branchStock.TryGetValue(p.ProductId, out var quantity) ? quantity : 0 })
                        .ToList();
                }

                return _activeProducts = products;
            }

            // Branch scope: points earned/used on that branch's sales (manual adjustments have no branch).
            public async Task<(int Earned, int Used)> GetPointsAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                var totals = await _db.LoyaltyTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Date >= start &&
                                x.Date < end &&
                                x.TransactionType != CancellationType &&
                                (x.SalesTransactionId == null ||
                                 (x.SalesTransaction != null &&
                                  !x.SalesTransaction.IsDeleted &&
                                  x.SalesTransaction.Status == "Completed")) &&
                                (branchId == null ||
                                 (x.SalesTransaction != null && x.SalesTransaction.BranchId == branchId)))
                    .GroupBy(x => 1)
                    .Select(g => new { Earned = g.Sum(x => x.PointsEarned), Used = g.Sum(x => x.PointsUsed) })
                    .FirstOrDefaultAsync();

                return (totals?.Earned ?? 0, totals?.Used ?? 0);
            }

            public async Task<decimal> GetPreviousMonthSalesAsync()
            {
                var previousStart = PreviousStart;
                var start = Start;
                var branchId = _branchId;

                return await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate >= previousStart &&
                                x.TransactionDate < start &&
                                (branchId == null || x.BranchId == branchId))
                    .SumAsync(x => x.FinalAmount);
            }

            public async Task<(int New, int Returning)> GetNewVsReturningAsync()
            {
                var sales = await GetSalesAsync();
                var customerIds = sales.Select(s => s.CustomerId).Distinct().ToList();

                if (customerIds.Count == 0)
                {
                    return (0, 0);
                }

                var start = Start;
                var branchId = _branchId;

                // Branch scope: "returning" means they bought at THIS branch before.
                var returningIds = await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate < start &&
                                customerIds.Contains(x.CustomerId) &&
                                (branchId == null || x.BranchId == branchId))
                    .Select(x => x.CustomerId)
                    .Distinct()
                    .ToListAsync();

                return (customerIds.Count - returningIds.Count, returningIds.Count);
            }

            // Branch scope: customers registered at that branch.
            public async Task<List<ChartPoint>> GetCustomerGrowthAsync()
            {
                var start = Start;
                var end = End;
                var branchId = _branchId;

                int running = await _db.Customers
                    .AsNoTracking()
                    .CountAsync(c => c.CreatedAt < start && (branchId == null || c.BranchId == branchId));

                var createdInMonth = await _db.Customers
                    .AsNoTracking()
                    .Where(c => c.CreatedAt >= start && c.CreatedAt < end && (branchId == null || c.BranchId == branchId))
                    .Select(c => c.CreatedAt)
                    .ToListAsync();

                var perDay = createdInMonth
                    .GroupBy(d => d.Date)
                    .ToDictionary(g => g.Key, g => g.Count());

                var points = new List<ChartPoint>();

                foreach (var day in Days())
                {
                    if (perDay.TryGetValue(day, out int created))
                    {
                        running += created;
                    }

                    points.Add(new ChartPoint { Label = DayLabel(day), Date = day, Value = running });
                }

                return points;
            }

            // Branch scope: customers who completed a purchase at that branch (the balance shown is their shared balance).
            public async Task<List<ChartPoint>> GetTopLoyalCustomersAsync()
            {
                var branchId = _branchId;
                var db = _db;

                var rows = await db.Customers
                    .AsNoTracking()
                    .Where(c => c.Status == "Active" && c.LoyaltyPoints > 0 &&
                                (branchId == null || db.SalesTransactions.Any(s =>
                                    s.CustomerId == c.CustomerId && s.BranchId == branchId && !s.IsDeleted && s.Status == "Completed")))
                    .OrderByDescending(c => c.LoyaltyPoints)
                    .Take(10)
                    .Select(c => new { Name = c.FirstName + " " + c.LastName, c.LoyaltyPoints })
                    .ToListAsync();

                return rows
                    .Select(r => new ChartPoint { Label = r.Name.Trim(), Value = r.LoyaltyPoints })
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetSalesTrendAsync()
            {
                var sales = await GetSalesAsync();

                var perDay = sales
                    .GroupBy(s => s.Date.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.FinalAmount));

                return Days()
                    .Select(d => new ChartPoint
                    {
                        Label = DayLabel(d),
                        Date = d,
                        Value = perDay.TryGetValue(d, out decimal total) ? (double)total : 0
                    })
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetTopProductsAsync()
            {
                var items = await GetItemsAsync();

                return items
                    .GroupBy(i => i.ProductId)
                    .Select(g => new ChartPoint
                    {
                        Label = LabelOrDefault(g.First().ProductName, $"Product #{g.Key}"),
                        Value = g.Sum(i => i.Quantity)
                    })
                    .OrderByDescending(p => p.Value)
                    .Take(10)
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetSalesByCategoryAsync()
            {
                var items = await GetItemsAsync();

                return items
                    .GroupBy(i => LabelOrDefault(i.Category, "Uncategorized"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ChartPoint { Label = g.Key, Value = (double)g.Sum(i => i.Subtotal) })
                    .OrderByDescending(p => p.Value)
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetProductStockAsync()
            {
                var products = await GetActiveProductsAsync();

                return products
                    .OrderBy(p => p.Quantity)
                    .ThenBy(p => p.Name)
                    .Take(10)
                    .Select(p => new ChartPoint { Label = p.Name, Value = p.Quantity })
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetSalesByPromotionAsync()
            {
                var sales = await GetSalesAsync();
                var names = await GetPromotionNamesAsync();

                return sales
                    .GroupBy(s => s.PromotionId)
                    .Select(g => new ChartPoint
                    {
                        Label = g.Key == null ? "No Promotion" : PromotionName(names, g.Key.Value),
                        Value = (double)g.Sum(s => s.FinalAmount)
                    })
                    .OrderByDescending(p => p.Value)
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetDiscountByPromotionAsync()
            {
                var sales = await GetSalesAsync();
                var names = await GetPromotionNamesAsync();

                return sales
                    .Where(s => s.PromotionId != null)
                    .GroupBy(s => s.PromotionId!.Value)
                    .Select(g => new ChartPoint
                    {
                        Label = PromotionName(names, g.Key),
                        Value = (double)g.Sum(s => s.DiscountAmount)
                    })
                    .Where(p => p.Value > 0)
                    .OrderByDescending(p => p.Value)
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetPromotionUsageAsync()
            {
                var sales = await GetSalesAsync();
                var promotions = await GetPromotionsAsync();

                var usage = sales
                    .Where(s => s.PromotionId != null)
                    .GroupBy(s => s.PromotionId!.Value)
                    .ToDictionary(g => g.Key, g => g.Count());

                return promotions
                    .Where(p => IsActive(p.Status) || usage.ContainsKey(p.PromotionId))
                    .Select(p => new ChartPoint
                    {
                        Label = p.Name,
                        Value = usage.TryGetValue(p.PromotionId, out int count) ? count : 0
                    })
                    .OrderByDescending(p => p.Value)
                    .ThenBy(p => p.Label)
                    .ToList();
            }

            public async Task<List<ChartPoint>> GetPromotionStatusAsync()
            {
                var promotions = await GetPromotionsAsync();
                int active = promotions.Count(p => IsActive(p.Status));

                return new List<ChartPoint>
                {
                    new ChartPoint { Label = "Active", Value = active },
                    new ChartPoint { Label = "Inactive", Value = promotions.Count - active }
                };
            }

            public List<ChartPoint> GetDailyCounts(IEnumerable<DateTime> dates)
            {
                var perDay = dates
                    .GroupBy(d => d.Date)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Days()
                    .Select(d => new ChartPoint
                    {
                        Label = DayLabel(d),
                        Date = d,
                        Value = perDay.TryGetValue(d, out int count) ? count : 0
                    })
                    .ToList();
            }

            private async Task<Dictionary<int, string>> GetPromotionNamesAsync()
            {
                var promotions = await GetPromotionsAsync();
                return promotions.ToDictionary(p => p.PromotionId, p => p.Name);
            }

            private List<DateTime> Days()
            {
                DateTime last = End.AddDays(-1).Date;
                DateTime today = DateTime.Now.Date;

                if (today >= Start.Date && today < last)
                {
                    last = today;
                }

                var days = new List<DateTime>();

                for (DateTime day = Start.Date; day <= last; day = day.AddDays(1))
                {
                    days.Add(day);
                }

                return days;
            }
        }

        private static bool TryGetMonthRange(int? year, int? month, out DateTime start, out DateTime end)
        {
            DateTime now = DateTime.Now;
            int selectedYear = year ?? now.Year;
            int selectedMonth = month ?? now.Month;

            start = default;
            end = default;

            if (selectedYear < 2000 || selectedYear > 2100 || selectedMonth < 1 || selectedMonth > 12)
            {
                return false;
            }

            start = new DateTime(selectedYear, selectedMonth, 1);
            end = start.AddMonths(1);
            return true;
        }

        private static bool TryGetReportRange(
            int? year,
            int? month,
            DateTime? startDate,
            DateTime? endDate,
            out DateTime start,
            out DateTime end,
            out string error)
        {
            error = string.Empty;

            if (startDate == null && endDate == null)
            {
                if (TryGetMonthRange(year, month, out start, out end))
                {
                    return true;
                }

                error = InvalidMonthMessage;
                return false;
            }

            start = default;
            end = default;

            if (startDate == null || endDate == null || startDate.Value.Date > endDate.Value.Date)
            {
                error = InvalidDateRangeMessage;
                return false;
            }

            if (startDate.Value.Year < 2000 || endDate.Value.Year > 2100)
            {
                error = InvalidDateRangeMessage;
                return false;
            }

            start = startDate.Value.Date;
            end = endDate.Value.Date.AddDays(1);

            if ((end - start).TotalDays > 3660)
            {
                error = DateRangeTooLargeMessage;
                return false;
            }

            return true;
        }

        private static string DayLabel(DateTime day)
        {
            return day.ToString("MMM d", CultureInfo.InvariantCulture);
        }

        private static string FormatPeso(decimal amount)
        {
            return "₱" + amount.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static double Rate(int part, int whole)
        {
            return whole > 0 ? Math.Round(part * 100.0 / whole, 1) : 0;
        }

        private static string Plural(int count, string singular, string plural)
        {
            return $"{count} {(count == 1 ? singular : plural)}";
        }

        private static string LabelOrDefault(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string PromotionName(Dictionary<int, string> names, int promotionId)
        {
            return names.TryGetValue(promotionId, out var name) ? name : $"Promotion #{promotionId}";
        }

        private static bool IsActive(string? status)
        {
            return string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStatus(string? status, string expected)
        {
            return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsComplaint(FeedbackRow row)
        {
            return string.Equals(row.Type, ComplaintType, StringComparison.OrdinalIgnoreCase);
        }

        private static List<ChartPoint> CountByCategory(IEnumerable<FeedbackRow> rows)
        {
            var counts = rows
                .GroupBy(r => LabelOrDefault(r.Category, "Unspecified"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var points = new List<ChartPoint>();

            foreach (var category in FeedbackCategoryOrder)
            {
                counts.TryGetValue(category, out int count);
                points.Add(new ChartPoint { Label = category, Value = count });
                counts.Remove(category);
            }

            foreach (var extra in counts.OrderByDescending(x => x.Value))
            {
                points.Add(new ChartPoint { Label = extra.Key, Value = extra.Value });
            }

            return points;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!TryGetReportRange(year, month, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            bool management = HttpContext.HasTenantPermission(TenantPermissions.ViewManagementDashboard);
            bool retention = HttpContext.HasTenantPermission(TenantPermissions.ViewRetentionDashboard);
            bool promotionsVisible = retention || HttpContext.HasTenantPermission(TenantPermissions.UsePromotions);
            bool feedbackVisible = HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("feedback"));
            bool inquiriesVisible = HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("inquiries"));

            int? branchId = await GetReportBranchAsync(tenantDb);
            var report = new MonthReport(tenantDb, start, end, branchId);

            decimal totalSales = 0;
            int transactionCount = 0;
            decimal averageTransaction = 0;
            int totalProductsSold = 0;
            decimal previousPeriodSales = 0;

            if (management)
            {
                var sales = await report.GetSalesAsync();
                totalSales = sales.Sum(x => x.FinalAmount);
                transactionCount = sales.Count;
                averageTransaction = transactionCount > 0 ? totalSales / transactionCount : 0;

                var items = await report.GetItemsAsync();
                totalProductsSold = items.Sum(x => x.Quantity);

                previousPeriodSales = await report.GetPreviousMonthSalesAsync();
            }

            var activeCustomerQuery = tenantDb.Customers.AsNoTracking().Where(x => x.Status == "Active");

            if (branchId != null)
            {
                activeCustomerQuery = BranchStock.AssociatedWith(activeCustomerQuery, tenantDb, branchId.Value);
            }

            int totalCustomers = await activeCustomerQuery.CountAsync();

            int newCustomers = management
                ? await tenantDb.Customers
                    .AsNoTracking()
                    .CountAsync(x => x.CreatedAt >= DateTime.UtcNow.AddDays(-30) && (branchId == null || x.BranchId == branchId))
                : 0;

            double repeatRate = 0;

            if (retention)
            {
                var customerTransactionCounts = await tenantDb.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.Status == "Completed" && (branchId == null || x.BranchId == branchId))
                    .GroupBy(x => x.CustomerId)
                    .Select(g => g.Count())
                    .ToListAsync();

                int customersWithPurchases = customerTransactionCounts.Count;
                int repeatCustomers = customerTransactionCounts.Count(c => c > 1);
                repeatRate = customersWithPurchases > 0
                    ? Math.Round(repeatCustomers * 100.0 / customersWithPurchases, 1)
                    : 0;
            }

            int totalActivePromotions = 0;

            if (promotionsVisible)
            {
                var promotions = await report.GetPromotionsAsync();
                totalActivePromotions = promotions.Count(p => IsActive(p.Status));
            }

            int earnedPoints = 0;
            int usedPoints = 0;

            if (retention)
            {
                (earnedPoints, usedPoints) = await report.GetPointsAsync();
            }

            int resolvedFeedback = 0;
            int resolvedComplaints = 0;
            double resolutionRate = 0;

            if (management && feedbackVisible)
            {
                var feedback = await report.GetFeedbackAsync();
                resolvedFeedback = feedback.Count(f => !IsComplaint(f) && IsStatus(f.Status, ResolvedStatus));
                resolvedComplaints = feedback.Count(f => IsComplaint(f) && IsStatus(f.Status, ResolvedStatus));
                resolutionRate = Rate(resolvedFeedback + resolvedComplaints, feedback.Count);
            }

            int respondedInquiries = 0;
            double responseRate = 0;

            if (management && inquiriesVisible)
            {
                var inquiries = await report.GetInquiriesAsync();
                respondedInquiries = inquiries.Count(i => i.RespondedAt != null);
                responseRate = Rate(respondedInquiries, inquiries.Count);
            }

            return Ok(new
            {
                TotalSales = totalSales,
                PreviousPeriodSales = previousPeriodSales,
                TransactionCount = transactionCount,
                AverageTransaction = averageTransaction,
                TotalProductsSold = totalProductsSold,
                TotalCustomers = totalCustomers,
                NewCustomersLast30Days = newCustomers,
                RepeatCustomerRatePercent = repeatRate,
                TotalActivePromotions = totalActivePromotions,
                TotalEarnedPoints = earnedPoints,
                TotalUsedPoints = usedPoints,
                TotalResolvedFeedback = resolvedFeedback,
                TotalResolvedComplaints = resolvedComplaints,
                TotalRespondedInquiries = respondedInquiries,
                ResolutionRatePercent = resolutionRate,
                ResponseRatePercent = responseRate
            });
        }

        [HttpGet("insights")]
        [RequireTenantPermission(TenantPermissions.ViewRetentionDashboard)]
        public async Task<IActionResult> GetInsights(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!TryGetReportRange(year, month, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var report = new MonthReport(tenantDb, start, end, await GetReportBranchAsync(tenantDb));
            var insights = new List<ReportInsight>();
            string monthLabel = report.Label;
            string thisPeriod = report.ThisPeriod;

            void Add(string category, string dataSummary, string analysis, string insight, string action, string target, string label)
            {
                insights.Add(new ReportInsight
                {
                    Category = category,
                    DataSummary = dataSummary,
                    Analysis = analysis,
                    Insight = insight,
                    SuggestedAction = action,
                    ActionTarget = target,
                    ActionLabel = label
                });
            }

            var sales = await report.GetSalesAsync();
            var items = await report.GetItemsAsync();
            var activeProducts = await report.GetActiveProductsAsync();

            var topSold = items
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Name = LabelOrDefault(g.First().ProductName, $"Product #{g.Key}"),
                    Quantity = g.Sum(i => i.Quantity)
                })
                .OrderByDescending(x => x.Quantity)
                .FirstOrDefault();

            if (topSold != null)
            {
                int totalUnits = items.Sum(i => i.Quantity);
                var topStock = activeProducts.FirstOrDefault(p => p.ProductId == topSold.ProductId);
                bool lowStock = topStock != null && topStock.Quantity <= topStock.ReorderLevel;

                Add(
                    "Product Insight",
                    $"\"{topSold.Name}\" had the highest sales in {monthLabel} with {Plural(topSold.Quantity, "unit", "units")} sold ({Rate(topSold.Quantity, totalUnits)}% of all units sold).",
                    topStock != null
                        ? $"It currently has {topStock.Quantity} in stock against a reorder level of {topStock.ReorderLevel}."
                        : "It is not currently listed among the active products.",
                    lowStock
                        ? "Its current stock is at or below the reorder level, so availability may limit further sales."
                        : "Its current stock is above the reorder level.",
                    lowStock
                        ? "Restock this product soon to keep it available."
                        : "Check its stock and maintain availability.",
                    "products",
                    "View Products");
            }

            int outOfStock = activeProducts.Count(p => p.Quantity <= 0);
            int lowStockCount = activeProducts.Count(p => p.Quantity > 0 && p.Quantity <= p.ReorderLevel);

            if (outOfStock + lowStockCount > 0)
            {
                Add(
                    "Product Insight",
                    $"{Plural(outOfStock, "active product is", "active products are")} out of stock and {lowStockCount} more {(lowStockCount == 1 ? "is" : "are")} at or below the reorder level.",
                    "Stock levels are current and are not limited to the selected month.",
                    "Products at or below their reorder level may limit sales if they are not replenished.",
                    "Review these products and restock where needed.",
                    "products",
                    "View Products");
            }

            decimal previousSales = await report.GetPreviousMonthSalesAsync();

            if (sales.Count > 0 || previousSales > 0)
            {
                decimal totalSales = sales.Sum(s => s.FinalAmount);
                string previousLabel = report.PreviousLabel;
                double change = previousSales > 0
                    ? Math.Round((double)((totalSales - previousSales) / previousSales * 100m), 1)
                    : 0;

                string dataSummary = sales.Count > 0
                    ? $"Sales in {monthLabel} total {FormatPeso(totalSales)} from {Plural(sales.Count, "completed transaction", "completed transactions")}."
                    : $"No completed sales were recorded in {monthLabel}.";

                string analysis;

                if (previousSales <= 0)
                {
                    analysis = $"No completed sales were recorded in {previousLabel} to compare against.";
                }
                else if (sales.Count == 0)
                {
                    analysis = $"{previousLabel} had {FormatPeso(previousSales)} in completed sales.";
                }
                else if (change == 0)
                {
                    analysis = $"That is unchanged from {previousLabel} ({FormatPeso(previousSales)}).";
                }
                else
                {
                    analysis = $"That is {Math.Abs(change)}% {(change > 0 ? "higher" : "lower")} than {previousLabel} ({FormatPeso(previousSales)}).";
                }

                string insight = $"There is no sales activity to analyze for {thisPeriod}.";

                if (sales.Count > 0)
                {
                    var bestDay = sales
                        .GroupBy(s => s.Date.Date)
                        .Select(g => new { Day = g.Key, Total = g.Sum(s => s.FinalAmount) })
                        .OrderByDescending(x => x.Total)
                        .First();

                    insight = $"The strongest day was {DayLabel(bestDay.Day)} with {FormatPeso(bestDay.Total)} in sales.";
                }

                bool declining = sales.Count == 0 || (previousSales > 0 && change < 0);

                Add(
                    "Sales Insight",
                    dataSummary,
                    analysis,
                    insight,
                    declining
                        ? "Review promotions and product availability for factors that may be holding sales back."
                        : $"Consider repeating the activities that supported the sales of {thisPeriod}.",
                    "sales",
                    "View Sales");
            }

            var (newCount, returningCount) = await report.GetNewVsReturningAsync();
            int purchasingCustomers = newCount + returningCount;

            if (purchasingCustomers > 0)
            {
                double returningShare = Rate(returningCount, purchasingCustomers);
                bool mostlyReturning = returningShare >= 50;

                Add(
                    "Customer Insight",
                    $"{returningCount} of {purchasingCustomers} purchasing customers in {monthLabel} ({returningShare}%) were returning customers, and {newCount} were new.",
                    mostlyReturning
                        ? $"Most purchasing customers {thisPeriod} had bought before."
                        : $"Most purchasing customers {thisPeriod} were making their first purchase.",
                    mostlyReturning
                        ? "This may indicate healthy repeat engagement."
                        : "This may indicate an opportunity to encourage repeat purchases.",
                    mostlyReturning
                        ? "Consider rewarding returning customers to keep them engaged."
                        : "Consider a follow-up or loyalty incentive aimed at first-time customers.",
                    "customers",
                    "View Customers");
            }

            var (earned, used) = await report.GetPointsAsync();

            if (earned > 0 || used > 0)
            {
                double redemptionRate = Rate(used, earned);
                bool lowRedemption = earned > 0 && redemptionRate < 30;

                Add(
                    "Loyalty Insight",
                    $"{Plural(earned, "loyalty point was", "loyalty points were")} earned and {used} redeemed in {monthLabel}.",
                    earned > 0
                        ? $"Redemption was {redemptionRate}% of the points earned {thisPeriod}."
                        : $"Points were redeemed {thisPeriod} without any new points being earned.",
                    lowRedemption
                        ? "Relatively few of the earned points are being redeemed."
                        : "Customers are actively redeeming loyalty points.",
                    lowRedemption
                        ? "Consider a loyalty-based promotion to encourage point redemption."
                        : "Consider monitoring outstanding points while redemption remains active.",
                    "loyalty",
                    "View Loyalty");
            }

            var promotions = await report.GetPromotionsAsync();
            var activePromotions = promotions.Where(p => IsActive(p.Status)).ToList();
            var promotionUsage = sales
                .Where(s => s.PromotionId != null)
                .GroupBy(s => s.PromotionId!.Value)
                .Select(g => new { PromotionId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            var unusedActive = activePromotions
                .Where(p => !promotionUsage.Any(u => u.PromotionId == p.PromotionId))
                .ToList();

            if (promotionUsage.Count > 0)
            {
                var topUsage = promotionUsage.First();
                string topName = promotions.FirstOrDefault(p => p.PromotionId == topUsage.PromotionId)?.Name
                    ?? $"Promotion #{topUsage.PromotionId}";

                Add(
                    "Promotion Insight",
                    $"\"{topName}\" was the most used promotion in {monthLabel}, applied in {Plural(topUsage.Count, "completed transaction", "completed transactions")}.",
                    unusedActive.Count > 0
                        ? $"{Plural(unusedActive.Count, "active promotion was", "active promotions were")} not used in any completed transaction {thisPeriod}."
                        : $"All active promotions were used at least once {thisPeriod}.",
                    unusedActive.Count > 0
                        ? $"Low usage may indicate limited awareness or appeal for {string.Join(", ", unusedActive.Take(3).Select(p => p.Name))}."
                        : "Active promotions are seeing use.",
                    unusedActive.Count > 0
                        ? "Consider reviewing the terms or visibility of low-usage promotions."
                        : "Consider comparing usage across months to see which promotions perform best.",
                    "promotions",
                    "View Promotions");
            }
            else if (activePromotions.Count > 0)
            {
                Add(
                    "Promotion Insight",
                    $"{Plural(activePromotions.Count, "active promotion exists", "active promotions exist")}, but none were used in a completed transaction in {monthLabel}.",
                    $"No completed transaction {thisPeriod} references a promotion.",
                    "This may indicate low customer awareness of current promotions.",
                    "Consider promoting current offers more visibly to customers.",
                    "promotions",
                    "View Promotions");
            }

            var feedback = await report.GetFeedbackAsync();

            if (feedback.Count > 0)
            {
                var complaints = feedback.Where(IsComplaint).ToList();
                int pendingFeedback = feedback.Count(f => IsStatus(f.Status, PendingStatus));
                var focus = complaints.Count > 0 ? complaints : feedback;

                var topCategory = focus
                    .GroupBy(f => LabelOrDefault(f.Category, "Unspecified"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .First();

                string dataSummary = complaints.Count > 0
                    ? $"{Plural(complaints.Count, "complaint", "complaints")} logged in {monthLabel}; \"{topCategory.Name}\" is the most common category ({topCategory.Count})."
                    : $"{Plural(feedback.Count, "feedback entry", "feedback entries")} logged in {monthLabel} with no complaints; \"{topCategory.Name}\" is the most common category ({topCategory.Count}).";

                Add(
                    "Feedback Insight",
                    dataSummary,
                    $"{pendingFeedback} of {feedback.Count} submitted {thisPeriod} remain pending.",
                    pendingFeedback > 0
                        ? "Pending submissions have not yet been reviewed."
                        : $"No submissions from {thisPeriod} are waiting for review.",
                    pendingFeedback > 0
                        ? "Process the pending feedback, starting with the most common category."
                        : "Review the most common category for recurring themes.",
                    "feedback",
                    "View Feedback");
            }

            var inquiries = await report.GetInquiriesAsync();

            if (inquiries.Count > 0)
            {
                int respondedInquiries = inquiries.Count(i => i.RespondedAt != null);
                int pendingInquiries = inquiries.Count(i => IsStatus(i.Status, PendingStatus));

                var topType = inquiries
                    .GroupBy(i => LabelOrDefault(i.Type, "Unspecified"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .First();

                Add(
                    "Inquiry Insight",
                    $"{Plural(inquiries.Count, "inquiry", "inquiries")} received in {monthLabel}; \"{topType.Name}\" is the most common type ({topType.Count}).",
                    $"{respondedInquiries} of {inquiries.Count} ({Rate(respondedInquiries, inquiries.Count)}%) have a recorded response, and {pendingInquiries} are still pending.",
                    pendingInquiries > 0
                        ? "Some inquiries are still waiting for staff attention."
                        : $"No inquiries from {thisPeriod} are waiting in Pending status.",
                    pendingInquiries > 0
                        ? "Follow up on pending inquiries to keep response times low."
                        : "Consider reviewing common inquiry types for recurring concerns.",
                    "inquiries",
                    "View Inquiries");
            }

            return Ok(insights);
        }

        [HttpGet("charts")]
        public async Task<IActionResult> GetCharts(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!TryGetReportRange(year, month, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var report = new MonthReport(tenantDb, start, end, await GetReportBranchAsync(tenantDb));

            bool management = HttpContext.HasTenantPermission(TenantPermissions.ViewManagementDashboard);
            bool retention = HttpContext.HasTenantPermission(TenantPermissions.ViewRetentionDashboard);
            bool feedbackVisible = management && HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("feedback"));
            bool inquiriesVisible = management && HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("inquiries"));

            async Task<List<ChartPoint>> Load(bool allowed, Func<Task<List<ChartPoint>>> source)
            {
                return allowed ? await source() : new List<ChartPoint>();
            }

            var customerGrowth = await Load(management, report.GetCustomerGrowthAsync);
            var newVsReturning = new List<ChartPoint>();

            if (retention)
            {
                var (newCustomers, returningCustomers) = await report.GetNewVsReturningAsync();
                newVsReturning.Add(new ChartPoint { Label = "New Customers", Value = newCustomers });
                newVsReturning.Add(new ChartPoint { Label = "Returning Customers", Value = returningCustomers });
            }

            var topLoyalCustomers = await Load(retention, report.GetTopLoyalCustomersAsync);

            var productPerformance = await Load(management, report.GetTopProductsAsync);
            var salesByCategory = await Load(management, report.GetSalesByCategoryAsync);
            var productStock = await report.GetProductStockAsync();

            var salesTrend = await Load(management, report.GetSalesTrendAsync);
            var salesByPromotion = await Load(retention, report.GetSalesByPromotionAsync);
            var discountByPromotion = await Load(retention, report.GetDiscountByPromotionAsync);

            var promotionStatus = await Load(retention, report.GetPromotionStatusAsync);
            var promotionUsage = await Load(retention, report.GetPromotionUsageAsync);

            var complaintsByCategory = new List<ChartPoint>();
            var feedbackByCategory = new List<ChartPoint>();
            var feedbackTrend = new List<ChartPoint>();

            if (feedbackVisible)
            {
                var feedback = await report.GetFeedbackAsync();
                complaintsByCategory = CountByCategory(feedback.Where(IsComplaint));
                feedbackByCategory = CountByCategory(feedback.Where(f => !IsComplaint(f)));
                feedbackTrend = report.GetDailyCounts(feedback.Select(f => f.Date));
            }

            var inquiryTrend = new List<ChartPoint>();
            var inquiryTypes = new List<ChartPoint>();

            if (inquiriesVisible)
            {
                var inquiries = await report.GetInquiriesAsync();
                inquiryTrend = report.GetDailyCounts(inquiries.Select(i => i.Date));
                inquiryTypes = inquiries
                    .GroupBy(i => LabelOrDefault(i.Type, "Unspecified"), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(p => p.Value)
                    .ToList();
            }

            return Ok(new
            {
                CustomerGrowth = customerGrowth,
                NewVsReturningCustomers = newVsReturning,
                TopLoyalCustomers = topLoyalCustomers,
                ProductPerformance = productPerformance,
                SalesByCategory = salesByCategory,
                ProductStock = productStock,
                SalesTrend = salesTrend,
                SalesByPromotion = salesByPromotion,
                DiscountGiven = discountByPromotion,
                PointsByCustomer = topLoyalCustomers,
                DiscountByPromotion = discountByPromotion,
                PromotionStatus = promotionStatus,
                PromotionUsage = promotionUsage,
                ComplaintsByCategory = complaintsByCategory,
                FeedbackByCategory = feedbackByCategory,
                FeedbackTrend = feedbackTrend,
                InquiryTrend = inquiryTrend,
                InquiryTypes = inquiryTypes
            });
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(int companyId)
        {
            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            DateTime today = DateTime.Now.Date;
            DateTime tomorrow = today.AddDays(1);
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime monthEnd = monthStart.AddMonths(1);
            DateTime overviewStart = today.AddDays(-6);

            // PREMIUM MANAGER / STAFF: sales, customer activity, loyalty activity and stock cover their branch only
            // (no branch assigned = nothing). ADMIN and non-branching plans stay company-wide.
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);
            bool branchOnly = scope.Restricted;
            int scopeBranchId = scope.BranchId ?? -1;

            var activeCustomerQuery = tenantDb.Customers.AsNoTracking().Where(c => c.Status == "Active");

            if (branchOnly)
            {
                activeCustomerQuery = BranchStock.AssociatedWith(activeCustomerQuery, tenantDb, scopeBranchId);
            }

            int activeCustomers = await activeCustomerQuery.CountAsync();

            int activeProducts = await tenantDb.Products
                .AsNoTracking()
                .CountAsync(p => p.Status == "Active");

            var todaysCompletedSales = tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= today &&
                            x.TransactionDate < tomorrow &&
                            (!branchOnly || x.BranchId == scopeBranchId));

            decimal todaysSales = await todaysCompletedSales.SumAsync(x => (decimal?)x.FinalAmount) ?? 0m;
            int todaysTransactions = await todaysCompletedSales.CountAsync();

            // Branch scope: the branch's own stock (BranchInventory), as in the Inventory report.
            bool hasBranch = scope.BranchId != null;

            var lowStockProducts = branchOnly
                ? await tenantDb.Products
                    .AsNoTracking()
                    .Where(p => hasBranch && p.Status == "Active")
                    .Select(p => new
                    {
                        p.ProductName,
                        Quantity = tenantDb.BranchInventories
                            .Where(i => i.BranchId == scopeBranchId && i.ProductId == p.ProductId)
                            .Select(i => i.Quantity)
                            .FirstOrDefault(),
                        p.ReorderLevel
                    })
                    .Where(p => p.Quantity <= p.ReorderLevel)
                    .OrderBy(p => p.Quantity)
                    .ToListAsync()
                : await tenantDb.Products
                    .AsNoTracking()
                    .Where(p => p.Status == "Active" && p.Quantity <= p.ReorderLevel)
                    .OrderBy(p => p.Quantity)
                    .Select(p => new { p.ProductName, p.Quantity, p.ReorderLevel })
                    .ToListAsync();

            var recentSalesRaw = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status == "Completed" && (!branchOnly || x.BranchId == scopeBranchId))
                .OrderByDescending(x => x.TransactionDate)
                .Take(5)
                .Select(x => new { x.CustomerId, x.FinalAmount, x.PaymentMethod, x.Status })
                .ToListAsync();

            var recentSaleCustomerIds = recentSalesRaw.Select(s => s.CustomerId).Distinct().ToList();

            var recentSaleCustomerNames = await tenantDb.Customers
                .AsNoTracking()
                .Where(c => recentSaleCustomerIds.Contains(c.CustomerId))
                .Select(c => new { c.CustomerId, c.FirstName, c.LastName })
                .ToDictionaryAsync(c => c.CustomerId, c => (c.FirstName + " " + c.LastName).Trim());

            var recentSales = recentSalesRaw
                .Select(s => new
                {
                    Customer = recentSaleCustomerNames.TryGetValue(s.CustomerId, out var name)
                        ? name
                        : $"Customer #{s.CustomerId}",
                    s.FinalAmount,
                    s.PaymentMethod,
                    s.Status
                })
                .ToList();

            var salesByDay = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= overviewStart &&
                            x.TransactionDate < tomorrow &&
                            (!branchOnly || x.BranchId == scopeBranchId))
                .Select(x => new { x.TransactionDate, x.FinalAmount })
                .ToListAsync();

            var perDay = salesByDay
                .GroupBy(x => x.TransactionDate.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.FinalAmount));

            var salesOverview = new List<ChartPoint>();

            for (DateTime day = overviewStart; day <= today; day = day.AddDays(1))
            {
                salesOverview.Add(new ChartPoint
                {
                    Label = DayLabel(day),
                    Date = day,
                    Value = perDay.TryGetValue(day, out decimal total) ? (double)total : 0
                });
            }

            bool management = HttpContext.HasTenantPermission(TenantPermissions.ViewManagementDashboard);
            bool retention = HttpContext.HasTenantPermission(TenantPermissions.ViewRetentionDashboard);
            bool company = HttpContext.HasTenantPermission(TenantPermissions.ViewCompanyDashboard);

            int newCustomersThisMonth = 0;
            int activeCustomersThisMonth = 0;

            if (management)
            {
                newCustomersThisMonth = await tenantDb.Customers
                    .AsNoTracking()
                    .CountAsync(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd &&
                                     (!branchOnly || c.BranchId == scopeBranchId));

                activeCustomersThisMonth = await tenantDb.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate >= monthStart &&
                                x.TransactionDate < monthEnd &&
                                (!branchOnly || x.BranchId == scopeBranchId))
                    .Select(x => x.CustomerId)
                    .Distinct()
                    .CountAsync();
            }

            int loyaltyActivityThisMonth = retention
                ? await tenantDb.LoyaltyTransactions
                    .AsNoTracking()
                    .CountAsync(x => !x.IsDeleted && x.Date >= monthStart && x.Date < monthEnd &&
                                     (!branchOnly || (x.SalesTransaction != null && x.SalesTransaction.BranchId == scopeBranchId)))
                : 0;

            object? companySummary = null;

            if (company)
            {
                var monthSales = tenantDb.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.TransactionDate >= monthStart &&
                                x.TransactionDate < monthEnd);

                decimal customerDiscounts = await monthSales
                    .Where(x => x.Status == "Completed")
                    .SumAsync(x => (decimal?)x.CustomerDiscountAmount) ?? 0m;

                decimal promotionDiscounts = retention
                    ? await monthSales
                        .Where(x => x.Status == "Completed")
                        .SumAsync(x => (decimal?)x.DiscountAmount) ?? 0m
                    : 0m;

                int cancelledTransactions = await monthSales.CountAsync(x => x.Status == "Cancelled");

                decimal inventoryValue = await tenantDb.Products
                    .AsNoTracking()
                    .Where(p => p.Status == "Active")
                    .SumAsync(p => (decimal?)(p.Price * p.Quantity)) ?? 0m;

                int outstandingPoints = retention
                    ? await tenantDb.Customers
                        .AsNoTracking()
                        .Where(c => c.Status == "Active")
                        .SumAsync(c => (int?)c.LoyaltyPoints) ?? 0
                    : 0;

                companySummary = new
                {
                    DiscountsGivenThisMonth = customerDiscounts + promotionDiscounts,
                    CancelledTransactionsThisMonth = cancelledTransactions,
                    InventoryValue = inventoryValue,
                    OutstandingLoyaltyPoints = outstandingPoints
                };
            }

            return Ok(new
            {
                ActiveCustomers = activeCustomers,
                ActiveProducts = activeProducts,
                TodaysSales = todaysSales,
                TodaysTransactions = todaysTransactions,
                LowStockCount = lowStockProducts.Count,
                RecentSales = recentSales,
                LowStockProducts = lowStockProducts,
                SalesOverview = salesOverview,
                NewCustomersThisMonth = newCustomersThisMonth,
                ActiveCustomersThisMonth = activeCustomersThisMonth,
                LoyaltyActivityThisMonth = loyaltyActivityThisMonth,
                Company = companySummary
            });
        }

        // Operational reports (every role, every plan): read-only figures for daily bakery work, limited to the modules
        // the user can open. PREMIUM MANAGER / STAFF: their assigned branch only (NoBranch = nothing); ADMIN and plans
        // without branches: company-wide. Purchases count only Completed, non-deleted sales. No retention, discount or
        // insight analytics are included: those stay in the management reports.
        [HttpGet("operations")]
        [RequireTenantPermission(TenantPermissions.ViewOperationalReports)]
        public async Task<IActionResult> GetOperationalReport(int companyId, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!TryGetReportRange(null, null, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            int? branchId = await GetReportBranchAsync(tenantDb);
            var report = new MonthReport(tenantDb, start, end, branchId);

            bool dataCollection = HttpContext.GetTenantFeatures().Contains(freshcrumbs.CRM.domain.entities.PlanFeatureKeys.DataCollection, StringComparer.OrdinalIgnoreCase);
            bool productsVisible = HttpContext.HasTenantPermission(TenantPermissions.ViewProducts);
            bool promotionsVisible = HttpContext.HasTenantPermission(TenantPermissions.UsePromotions);
            bool loyaltyVisible = HttpContext.HasTenantPermission(TenantPermissions.UseLoyalty);

            var sales = await report.GetSalesAsync();
            decimal totalSales = sales.Sum(s => s.FinalAmount);

            var recentSales = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end &&
                            (branchId == null || x.BranchId == branchId))
                .OrderByDescending(x => x.TransactionDate)
                .Take(10)
                .Select(x => new { x.TransactionDate, x.CustomerId, x.FinalAmount, x.PaymentMethod })
                .ToListAsync();

            var customerActivity = sales
                .GroupBy(s => s.CustomerId)
                .Select(g => new { CustomerId = g.Key, Purchases = g.Count(), LastPurchase = g.Max(s => s.Date) })
                .OrderByDescending(x => x.LastPurchase)
                .ToList();

            int newCustomers = await tenantDb.Customers
                .AsNoTracking()
                .CountAsync(c => c.CreatedAt >= start && c.CreatedAt < end && (branchId == null || c.BranchId == branchId));

            var recentFeedback = dataCollection
                ? await tenantDb.Feedbacks
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end && (branchId == null || x.BranchId == branchId))
                    .OrderByDescending(x => x.DateSubmitted)
                    .Take(10)
                    .Select(x => new { x.DateSubmitted, x.CustomerId, x.Type, x.Category, x.Status })
                    .ToListAsync()
                : new();

            var recentInquiries = dataCollection
                ? await tenantDb.Inquiries
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end && (branchId == null || x.BranchId == branchId))
                    .OrderByDescending(x => x.DateSubmitted)
                    .Take(10)
                    .Select(x => new { x.DateSubmitted, x.CustomerId, x.Type, x.Subject, x.Status })
                    .ToListAsync()
                : new();

            var customerIds = recentSales.Select(s => s.CustomerId)
                .Concat(customerActivity.Take(10).Select(c => c.CustomerId))
                .Concat(recentFeedback.Select(f => f.CustomerId))
                .Concat(recentInquiries.Select(i => i.CustomerId))
                .Distinct()
                .ToList();

            var customers = await tenantDb.Customers
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.CustomerId))
                .Select(c => new { c.CustomerId, c.CustomerCode, Name = (c.FirstName + " " + c.LastName).Trim() })
                .ToDictionaryAsync(c => c.CustomerId);

            string CustomerName(int id) => customers.TryGetValue(id, out var c) ? c.Name : $"Customer #{id}";

            object? products = null;

            if (productsVisible)
            {
                var stock = await report.GetActiveProductsAsync();
                var lowStock = stock
                    .Where(p => p.Quantity <= p.ReorderLevel)
                    .OrderBy(p => p.Quantity)
                    .ThenBy(p => p.Name)
                    .ToList();

                products = new
                {
                    ActiveProducts = stock.Count,
                    LowStockCount = lowStock.Count,
                    LowStock = lowStock.Take(10).Select(p => new { p.Name, p.Quantity, p.ReorderLevel }),
                    TopProducts = await report.GetTopProductsAsync()
                };
            }

            object? feedback = null;
            object? inquiries = null;

            if (dataCollection)
            {
                var feedbackRows = await report.GetFeedbackAsync();

                feedback = new
                {
                    Total = feedbackRows.Count,
                    Complaints = feedbackRows.Count(IsComplaint),
                    Open = feedbackRows.Count(f => !IsStatus(f.Status, ResolvedStatus)),
                    ByCategory = CountByCategory(feedbackRows),
                    Recent = recentFeedback.Select(f => new
                    {
                        Date = f.DateSubmitted,
                        Customer = CustomerName(f.CustomerId),
                        f.Type,
                        f.Category,
                        f.Status
                    })
                };

                var inquiryRows = await report.GetInquiriesAsync();

                inquiries = new
                {
                    Total = inquiryRows.Count,
                    Responded = inquiryRows.Count(i => i.RespondedAt != null),
                    Pending = inquiryRows.Count(i => i.RespondedAt == null),
                    ByStatus = inquiryRows
                        .GroupBy(i => LabelOrDefault(i.Status, PendingStatus), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                        .OrderByDescending(p => p.Value)
                        .ToList(),
                    Recent = recentInquiries.Select(i => new
                    {
                        Date = i.DateSubmitted,
                        Customer = CustomerName(i.CustomerId),
                        i.Type,
                        i.Subject,
                        i.Status
                    })
                };
            }

            object? promotions = null;

            if (promotionsVisible)
            {
                var promotionRows = await report.GetPromotionsAsync();

                promotions = new
                {
                    ActivePromotions = promotionRows.Count(p => IsActive(p.Status)),
                    SalesWithPromotion = sales.Count(s => s.PromotionId != null),
                    Usage = (await report.GetPromotionUsageAsync()).Where(p => p.Value > 0).ToList()
                };
            }

            object? loyalty = null;

            if (loyaltyVisible)
            {
                var (earned, used) = await report.GetPointsAsync();
                loyalty = new { PointsEarned = earned, PointsUsed = used };
            }

            string? branchName = branchId is > 0
                ? await tenantDb.Branches.AsNoTracking().Where(b => b.BranchId == branchId).Select(b => b.BranchName).FirstOrDefaultAsync()
                : null;

            return Ok(new
            {
                Period = report.Label,
                BranchScoped = branchId != null,
                BranchName = branchName,
                Sales = new
                {
                    TotalSales = totalSales,
                    Transactions = sales.Count,
                    AverageSale = sales.Count > 0 ? Math.Round(totalSales / sales.Count, 2) : 0m,
                    Trend = await report.GetSalesTrendAsync(),
                    Recent = recentSales.Select(s => new
                    {
                        Date = s.TransactionDate,
                        Customer = CustomerName(s.CustomerId),
                        s.FinalAmount,
                        s.PaymentMethod
                    })
                },
                Customers = new
                {
                    ActiveCustomers = customerActivity.Count,
                    NewCustomers = newCustomers,
                    Recent = customerActivity.Take(10).Select(c => new
                    {
                        Code = customers.TryGetValue(c.CustomerId, out var info) ? info.CustomerCode : string.Empty,
                        Name = CustomerName(c.CustomerId),
                        c.LastPurchase,
                        c.Purchases
                    })
                },
                Products = products,
                Feedback = feedback,
                Inquiries = inquiries,
                Promotions = promotions,
                Loyalty = loyalty
            });
        }

        // Generated "Business Summary": one table of the key figures of the period, built from MonthReport.
        // ADMIN: company-wide; PREMIUM MANAGER: their branch (branchId), like every generated report.
        private async Task<GeneratedReport> BuildBusinessSummaryReportAsync(TenantCrmDbContext db, DateTime start, DateTime end, int? branchId, string period)
        {
            var report = NewReport("business-summary", "Business Summary Report", period,
                Col("Section"), Col("Metric"), Col("Value"));

            void Add(string section, string metric, string value) => report.Rows.Add(new List<string> { section, metric, value });

            var month = new MonthReport(db, start, end, branchId);

            var sales = await month.GetSalesAsync();
            decimal total = sales.Sum(s => s.FinalAmount);
            decimal previous = await month.GetPreviousMonthSalesAsync();

            Add("Sales", "Total sales", FormatPeso(total));
            Add("Sales", "Transactions", ReportCount(sales.Count));
            Add("Sales", "Average transaction", FormatPeso(sales.Count > 0 ? Math.Round(total / sales.Count, 2) : 0m));
            Add("Sales", "Previous period (" + month.PreviousLabel + ")", FormatPeso(previous));
            Add("Sales", "Change vs previous period",
                previous > 0 ? Math.Round((double)((total - previous) * 100m / previous), 1).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "n/a");

            var items = await month.GetItemsAsync();
            Add("Products", "Units sold", ReportCount(items.Sum(i => i.Quantity)));

            var topProducts = (await month.GetTopProductsAsync()).Take(5).ToList();

            foreach (var product in topProducts)
            {
                Add("Products", "Top seller: " + product.Label, ReportCount((int)product.Value) + " sold");
            }

            var stock = await month.GetActiveProductsAsync();
            Add("Stock", "Active products", ReportCount(stock.Count));
            Add("Stock", "At or below reorder level", ReportCount(stock.Count(p => p.Quantity <= p.ReorderLevel)));

            var (newCount, returningCount) = await month.GetNewVsReturningAsync();
            int purchasing = newCount + returningCount;
            int repeat = sales.GroupBy(s => s.CustomerId).Count(g => g.Count() > 1);
            int registered = await db.Customers
                .AsNoTracking()
                .CountAsync(c => c.CreatedAt >= start && c.CreatedAt < end && (branchId == null || c.BranchId == branchId));

            Add("Customers", "Purchasing customers", ReportCount(purchasing));
            Add("Customers", "New (first purchase)", ReportCount(newCount));
            Add("Customers", "Returning", $"{ReportCount(returningCount)} ({Rate(returningCount, purchasing)}%)");
            Add("Customers", "Bought 2+ times in the period", $"{ReportCount(repeat)} ({Rate(repeat, purchasing)}%)");
            Add("Customers", "New registrations", ReportCount(registered));

            if (HttpContext.HasTenantPermission(TenantPermissions.UseLoyalty))
            {
                var (earned, used) = await month.GetPointsAsync();
                Add("Loyalty", "Points earned", ReportCount(earned));
                Add("Loyalty", "Points used", ReportCount(used));
            }

            if (HttpContext.HasTenantPermission(TenantPermissions.UsePromotions))
            {
                var promotions = await month.GetPromotionsAsync();
                Add("Promotions", "Active promotions", ReportCount(promotions.Count(p => IsActive(p.Status))));
                Add("Promotions", "Sales using a promotion", ReportCount(sales.Count(s => s.PromotionId != null)));
                Add("Promotions", "Promotion discounts given", FormatPeso(sales.Sum(s => s.DiscountAmount)));

                foreach (var usage in (await month.GetPromotionUsageAsync()).Where(p => p.Value > 0).Take(5))
                {
                    Add("Promotions", "Used: " + usage.Label, Plural((int)usage.Value, "time", "times"));
                }
            }

            if (HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("feedback")))
            {
                var feedback = await month.GetFeedbackAsync();
                int resolved = feedback.Count(f => IsStatus(f.Status, ResolvedStatus));
                Add("Feedback", "Feedback received", ReportCount(feedback.Count));
                Add("Feedback", "Complaints", ReportCount(feedback.Count(IsComplaint)));
                Add("Feedback", "Resolved", $"{ReportCount(resolved)} ({Rate(resolved, feedback.Count)}%)");
            }

            if (HttpContext.HasTenantPermission(TenantPermissions.ReportPermission("inquiries")))
            {
                var inquiries = await month.GetInquiriesAsync();
                int responded = inquiries.Count(i => i.RespondedAt != null);
                Add("Inquiries", "Inquiries received", ReportCount(inquiries.Count));
                Add("Inquiries", "Responded", $"{ReportCount(responded)} ({Rate(responded, inquiries.Count)}%)");
            }

            if (previous > 0)
            {
                Add("Insights", "Sales trend", total >= previous
                    ? $"Sales were up {FormatPeso(total - previous)} on {month.PreviousLabel}."
                    : $"Sales were down {FormatPeso(previous - total)} on {month.PreviousLabel}.");
            }

            if (topProducts.Count > 0)
            {
                Add("Insights", "Best seller", $"{topProducts[0].Label} led product sales with {ReportCount((int)topProducts[0].Value)} units.");
            }

            if (purchasing > 0)
            {
                Add("Insights", "Customer mix", Rate(returningCount, purchasing) >= 50
                    ? "Most purchasing customers had bought before: repeat engagement is healthy."
                    : "Most purchasing customers were buying for the first time: consider a follow-up or loyalty incentive.");
            }

            return report;
        }

        // Overview / Insights / Charts: PREMIUM MANAGER / STAFF see their branch's purchase activity (NoBranch when not
        // assigned = no data); ADMIN and non-branching plans stay company-wide.
        private const int NoBranch = -1;

        private async Task<int?> GetReportBranchAsync(TenantCrmDbContext tenantDb)
        {
            var scope = await BranchStock.GetScopeAsync(HttpContext, tenantDb);
            return scope.Restricted ? scope.BranchId ?? NoBranch : null;
        }

        private sealed record BranchRow(int BranchId, string BranchName, string Status);

        private sealed record BranchSaleRow(int? BranchId, int CustomerId, decimal FinalAmount, DateTime Date);

        private sealed class BranchBiRow
        {
            public int? BranchId { get; set; }
            public string BranchName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            // Sales recorded before branching (no BranchId). Shown separately, never attributed to a branch.
            public bool IsHistorical { get; set; }
            public decimal Revenue { get; set; }
            public int SalesCount { get; set; }
            public decimal AverageSale { get; set; }
            public int? PurchasingCustomers { get; set; }
            public int? ReturningCustomers { get; set; }
            public int? NewCustomers { get; set; }
            public double? RetentionRatePercent { get; set; }
            public decimal TodayRevenue { get; set; }
            public int TodaySalesCount { get; set; }
            public DateTime? LastSaleAt { get; set; }
            public int? UnitsOnHand { get; set; }
            public int? LowStockItems { get; set; }
            public int? AssignedAccounts { get; set; }
        }

        // PREMIUM Branch BI. ADMIN: every active branch of this company, plus sales recorded before branching
        // as a separate row. MANAGER: the assigned branch only; no assignment returns an empty "NotAssigned" result.
        // Sales follow the dashboard rules (Completed and not deleted). A customer is "returning" at a branch when
        // they had an earlier completed sale at that same branch.
        [HttpGet("branches")]
        [RequireTenantPermission(TenantPermissions.ViewBranchBI)]
        public async Task<IActionResult> GetBranchDashboard(
            int companyId,
            [FromServices] ISubscriptionService subscriptions,
            int? year = null,
            int? month = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (!TryGetReportRange(year, month, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            bool isAdmin = BranchStock.IsAdmin(HttpContext);
            string period = new MonthReport(tenantDb, start, end).Label;

            List<BranchRow> branches;

            if (isAdmin)
            {
                branches = await tenantDb.Branches
                    .AsNoTracking()
                    .Where(b => b.Status == "Active")
                    .OrderBy(b => b.BranchName)
                    .Select(b => new BranchRow(b.BranchId, b.BranchName, b.Status))
                    .ToListAsync();
            }
            else
            {
                var assigned = await BranchStock.GetAssignedBranchAsync(tenantDb, User);

                if (assigned == null)
                {
                    return Ok(new
                    {
                        Scope = "NotAssigned",
                        Period = period,
                        ActiveBranches = (int?)null,
                        MaxBranches = (int?)null,
                        Branches = new List<BranchBiRow>()
                    });
                }

                branches = new List<BranchRow> { new(assigned.BranchId, assigned.BranchName, assigned.Status) };
            }

            var branchIds = branches.Select(b => b.BranchId).ToList();

            DateTime today = DateTime.Now.Date;
            DateTime tomorrow = today.AddDays(1);

            var periodSales = await LoadBranchSalesAsync(tenantDb, branchIds, isAdmin, start, end);
            var todaySales = await LoadBranchSalesAsync(tenantDb, branchIds, isAdmin, today, tomorrow);

            var periodCustomerIds = periodSales
                .Where(s => s.BranchId != null)
                .Select(s => s.CustomerId)
                .Distinct()
                .ToList();

            var earlierPurchases = periodCustomerIds.Count == 0
                ? new HashSet<(int BranchId, int CustomerId)>()
                : (await tenantDb.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate < start &&
                                x.BranchId != null &&
                                branchIds.Contains(x.BranchId.Value) &&
                                periodCustomerIds.Contains(x.CustomerId))
                    .Select(x => new { BranchId = x.BranchId!.Value, x.CustomerId })
                    .Distinct()
                    .ToListAsync())
                    .Select(x => (x.BranchId, x.CustomerId))
                    .ToHashSet();

            var lastSales = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.BranchId != null &&
                            branchIds.Contains(x.BranchId.Value))
                .GroupBy(x => x.BranchId!.Value)
                .Select(g => new { BranchId = g.Key, Last = g.Max(x => x.TransactionDate) })
                .ToDictionaryAsync(x => x.BranchId, x => x.Last);

            var activeProducts = await tenantDb.Products
                .AsNoTracking()
                .Where(p => p.Status == "Active")
                .Select(p => new { p.ProductId, p.ReorderLevel })
                .ToListAsync();

            var inventory = await tenantDb.BranchInventories
                .AsNoTracking()
                .Where(i => branchIds.Contains(i.BranchId))
                .Select(i => new { i.BranchId, i.ProductId, i.Quantity })
                .ToListAsync();

            var assignedAccounts = await tenantDb.BranchAssignments
                .AsNoTracking()
                .Where(a => a.BranchId != null && branchIds.Contains(a.BranchId.Value))
                .GroupBy(a => a.BranchId!.Value)
                .Select(g => new { BranchId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BranchId, x => x.Count);

            var rows = new List<BranchBiRow>();

            foreach (var branch in branches)
            {
                var sales = periodSales.Where(s => s.BranchId == branch.BranchId).ToList();
                var todays = todaySales.Where(s => s.BranchId == branch.BranchId).ToList();
                var customers = sales.Select(s => s.CustomerId).Distinct().ToList();
                int returning = customers.Count(c => earlierPurchases.Contains((branch.BranchId, c)));

                var stock = inventory
                    .Where(i => i.BranchId == branch.BranchId)
                    .ToDictionary(i => i.ProductId, i => i.Quantity);

                rows.Add(new BranchBiRow
                {
                    BranchId = branch.BranchId,
                    BranchName = branch.BranchName,
                    Status = branch.Status,
                    Revenue = sales.Sum(s => s.FinalAmount),
                    SalesCount = sales.Count,
                    AverageSale = sales.Count > 0 ? Math.Round(sales.Sum(s => s.FinalAmount) / sales.Count, 2) : 0m,
                    PurchasingCustomers = customers.Count,
                    ReturningCustomers = returning,
                    NewCustomers = customers.Count - returning,
                    RetentionRatePercent = customers.Count > 0 ? Math.Round(returning * 100.0 / customers.Count, 1) : null,
                    TodayRevenue = todays.Sum(s => s.FinalAmount),
                    TodaySalesCount = todays.Count,
                    LastSaleAt = lastSales.TryGetValue(branch.BranchId, out var last) ? last : null,
                    UnitsOnHand = activeProducts.Sum(p => stock.TryGetValue(p.ProductId, out var q) ? q : 0),
                    LowStockItems = activeProducts.Count(p => (stock.TryGetValue(p.ProductId, out var q) ? q : 0) <= p.ReorderLevel),
                    AssignedAccounts = assignedAccounts.TryGetValue(branch.BranchId, out var count) ? count : 0
                });
            }

            if (isAdmin)
            {
                var historical = periodSales.Where(s => s.BranchId == null).ToList();
                var historicalToday = todaySales.Where(s => s.BranchId == null).ToList();

                if (historical.Count > 0 || historicalToday.Count > 0)
                {
                    rows.Add(new BranchBiRow
                    {
                        BranchId = null,
                        BranchName = "No branch (before branching)",
                        Status = string.Empty,
                        IsHistorical = true,
                        Revenue = historical.Sum(s => s.FinalAmount),
                        SalesCount = historical.Count,
                        AverageSale = historical.Count > 0 ? Math.Round(historical.Sum(s => s.FinalAmount) / historical.Count, 2) : 0m,
                        TodayRevenue = historicalToday.Sum(s => s.FinalAmount),
                        TodaySalesCount = historicalToday.Count
                    });
                }
            }

            var subscription = isAdmin ? await subscriptions.GetCurrentAsync(companyId) : null;

            return Ok(new
            {
                Scope = isAdmin ? "Company" : "Branch",
                Period = period,
                ActiveBranches = isAdmin ? branches.Count : (int?)null,
                MaxBranches = subscription?.MaxBranches,
                Branches = rows
            });
        }

        // Completed, non-deleted sales of the given branches; ADMIN also gets the sales recorded before branching.
        private static async Task<List<BranchSaleRow>> LoadBranchSalesAsync(
            TenantCrmDbContext tenantDb,
            List<int> branchIds,
            bool includeHistorical,
            DateTime start,
            DateTime end)
        {
            return await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end &&
                            ((x.BranchId == null && includeHistorical) ||
                             (x.BranchId != null && branchIds.Contains(x.BranchId.Value))))
                .Select(x => new BranchSaleRow(x.BranchId, x.CustomerId, x.FinalAmount, x.TransactionDate))
                .ToListAsync();
        }

        #region Generated report types and builders

        private const string UnknownReportTypeMessage = "Unknown report type.";

        private static readonly string[] GeneratedReportTypes =
        {
            "sales",
            "customers",
            "inventory",
            "product-sales",
            "loyalty",
            "promotions",
            "discounts",
            "feedback",
            "inquiries",
            "business-summary"
        };

        private sealed class GeneratedReportColumn
        {
            public string Header { get; set; } = string.Empty;
            public string Align { get; set; } = "Left";
        }

        private sealed class GeneratedReport
        {
            public string ReportType { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Period { get; set; } = string.Empty;
            public DateTime GeneratedAt { get; set; } = DateTime.Now;
            public List<GeneratedReportColumn> Columns { get; set; } = new();
            public List<List<string>> Rows { get; set; } = new();
            public List<string> Totals { get; set; } = new();
        }

        private record SalesReportRow(
            int TransactionId,
            DateTime Date,
            string CustomerCode,
            string CustomerName,
            string PaymentMethod,
            decimal TotalAmount,
            string? PromotionName,
            decimal DiscountAmount,
            decimal CustomerDiscountAmount,
            decimal FinalAmount,
            int PointsEarned,
            int PointsUsed);

        private record CustomerReportRow(
            int CustomerId,
            string CustomerCode,
            string FirstName,
            string LastName,
            string Email,
            string ContactNo,
            string Status,
            DateTime CreatedAt,
            int LoyaltyPoints);

        private record CustomerSpendRow(int CustomerId, int Purchases, decimal AmountSpent);

        private record InventoryReportRow(
            string ProductCode,
            string ProductName,
            string Category,
            decimal Price,
            int Quantity,
            int ReorderLevel,
            string Status);

        private record ProductSaleItemRow(
            int ProductId,
            int TransactionId,
            string ProductCode,
            string ProductName,
            string Category,
            int Quantity,
            decimal Subtotal);

        private record LoyaltyReportRow(
            DateTime Date,
            string CustomerCode,
            string CustomerName,
            string TransactionType,
            int PointsEarned,
            int PointsUsed,
            int? SalesTransactionId);

        private record PromotionReportRow(
            int PromotionId,
            string Name,
            string DiscountType,
            decimal DiscountValue,
            decimal MinimumPurchase,
            int RequiredLoyaltyPoints,
            DateTime StartDate,
            DateTime EndDate,
            string Status);

        private record PromotionUsageRow(int PromotionId, int TimesUsed, decimal TotalDiscount, decimal SalesGenerated);

        private record FeedbackReportRow(
            int FeedbackId,
            DateTime Date,
            string CustomerName,
            string Type,
            string Category,
            string Status,
            string Comment);

        private record InquiryReportRow(
            int InquiryId,
            DateTime Date,
            string CustomerName,
            string Type,
            string Source,
            string Subject,
            string Status,
            DateTime? RespondedAt,
            string RespondedBy);

        private static string ReportPeriodLabel(DateTime start, DateTime end)
        {
            DateTime last = end.AddDays(-1).Date;
            string first = start.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

            return start.Date == last
                ? first
                : first + " - " + last.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        private static string ReportDate(DateTime value)
        {
            return value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        private static string ReportDateTime(DateTime? value)
        {
            return value == null
                ? string.Empty
                : value.Value.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
        }

        private static string ReportAmount(decimal value)
        {
            return value.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string ReportCount(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string ReportText(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string ReportName(string? first, string? last)
        {
            string name = ((first ?? string.Empty) + " " + (last ?? string.Empty)).Trim();
            return name.Length == 0 ? "Unknown" : name;
        }

        private static string ReportCustomer(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim();
        }

        private static GeneratedReportColumn Col(string header, bool right = false)
        {
            return new GeneratedReportColumn { Header = header, Align = right ? "Right" : "Left" };
        }

        private static GeneratedReport NewReport(string reportType, string title, string period, params GeneratedReportColumn[] columns)
        {
            return new GeneratedReport
            {
                ReportType = reportType,
                Title = title,
                Period = period,
                GeneratedAt = DateTime.Now,
                Columns = columns.ToList()
            };
        }

        private static List<string> TotalsRow(int columnCount, params (int Index, string Value)[] cells)
        {
            var row = Enumerable.Repeat(string.Empty, columnCount).ToList();

            foreach (var cell in cells)
            {
                row[cell.Index] = cell.Value;
            }

            return row;
        }

        private static string StockStatus(int quantity, int reorderLevel)
        {
            if (quantity <= 0)
            {
                return "Out of Stock";
            }

            return quantity <= reorderLevel ? "Low Stock" : "In Stock";
        }

        private static GeneratedReport BuildSalesReport(List<SalesReportRow> rows, string period)
        {
            var report = NewReport("sales", "Sales Report", period,
                Col("Txn ID", true), Col("Date"), Col("Customer Code"), Col("Customer"), Col("Payment"),
                Col("Subtotal (₱)", true), Col("Promotion"), Col("Promotion Discount (₱)", true),
                Col("Customer Discount (₱)", true), Col("Final Amount (₱)", true),
                Col("Points Earned", true), Col("Points Used", true));

            foreach (var r in rows)
            {
                report.Rows.Add(new List<string>
                {
                    r.TransactionId.ToString(CultureInfo.InvariantCulture),
                    ReportDate(r.Date),
                    ReportText(r.CustomerCode),
                    ReportCustomer(r.CustomerName),
                    ReportText(r.PaymentMethod),
                    ReportAmount(r.TotalAmount),
                    ReportText(r.PromotionName),
                    ReportAmount(r.DiscountAmount),
                    ReportAmount(r.CustomerDiscountAmount),
                    ReportAmount(r.FinalAmount),
                    ReportCount(r.PointsEarned),
                    ReportCount(r.PointsUsed)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "sale", "sales")),
                (5, ReportAmount(rows.Sum(r => r.TotalAmount))),
                (7, ReportAmount(rows.Sum(r => r.DiscountAmount))),
                (8, ReportAmount(rows.Sum(r => r.CustomerDiscountAmount))),
                (9, ReportAmount(rows.Sum(r => r.FinalAmount))),
                (10, ReportCount(rows.Sum(r => r.PointsEarned))),
                (11, ReportCount(rows.Sum(r => r.PointsUsed))));

            return report;
        }

        private static GeneratedReport BuildCustomerReport(
            List<CustomerReportRow> customers,
            List<CustomerSpendRow> spend,
            Dictionary<int, DateTime> lastPurchases,
            string period)
        {
            var report = NewReport("customers", "Customer Report", period,
                Col("Code"), Col("Name"), Col("Email"), Col("Contact No"), Col("Status"), Col("Date Registered"),
                Col("Purchases (Period)", true), Col("Amount Spent (₱)", true), Col("Last Purchase"), Col("Current Points", true));

            var spendByCustomer = spend.ToDictionary(s => s.CustomerId);
            int totalPurchases = 0;
            decimal totalSpent = 0m;

            foreach (var c in customers.OrderBy(c => c.CustomerCode, StringComparer.OrdinalIgnoreCase))
            {
                spendByCustomer.TryGetValue(c.CustomerId, out var s);
                int purchases = s?.Purchases ?? 0;
                decimal spent = s?.AmountSpent ?? 0m;
                totalPurchases += purchases;
                totalSpent += spent;

                report.Rows.Add(new List<string>
                {
                    ReportText(c.CustomerCode),
                    ReportName(c.FirstName, c.LastName),
                    ReportText(c.Email),
                    ReportText(c.ContactNo),
                    ReportText(c.Status),
                    ReportDate(c.CreatedAt),
                    ReportCount(purchases),
                    ReportAmount(spent),
                    lastPurchases.TryGetValue(c.CustomerId, out var lastPurchase) ? ReportDate(lastPurchase) : string.Empty,
                    ReportCount(c.LoyaltyPoints)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(customers.Count, "customer", "customers")),
                (6, ReportCount(totalPurchases)),
                (7, ReportAmount(totalSpent)));

            return report;
        }

        private static GeneratedReport BuildInventoryReport(List<InventoryReportRow> products, string period)
        {
            var report = NewReport("inventory", "Inventory Report", period,
                Col("Code"), Col("Product"), Col("Category"), Col("Price (₱)", true), Col("Stock", true),
                Col("Reorder Level", true), Col("Stock Status"), Col("Product Status"));

            foreach (var p in products
                         .OrderBy(p => LabelOrDefault(p.Category, "Uncategorized"), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase))
            {
                report.Rows.Add(new List<string>
                {
                    ReportText(p.ProductCode),
                    ReportText(p.ProductName),
                    LabelOrDefault(p.Category, "Uncategorized"),
                    ReportAmount(p.Price),
                    ReportCount(p.Quantity),
                    ReportCount(p.ReorderLevel),
                    StockStatus(p.Quantity, p.ReorderLevel),
                    ReportText(p.Status)
                });
            }

            int outOfStock = products.Count(p => p.Quantity <= 0);
            int lowStock = products.Count(p => p.Quantity > 0 && p.Quantity <= p.ReorderLevel);

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(products.Count, "product", "products")),
                (4, ReportCount(products.Sum(p => p.Quantity))),
                (6, $"{outOfStock} out / {lowStock} low"));

            return report;
        }

        private static GeneratedReport BuildProductSalesReport(List<ProductSaleItemRow> items, string period)
        {
            var report = NewReport("product-sales", "Product Sales Report", period,
                Col("Code"), Col("Product"), Col("Category"), Col("Qty Sold", true),
                Col("Sales Amount (₱)", true), Col("Transactions", true));

            var grouped = items
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    Code = ReportText(g.First().ProductCode),
                    Name = LabelOrDefault(g.First().ProductName, $"Product #{g.Key}"),
                    Category = LabelOrDefault(g.First().Category, "Uncategorized"),
                    Quantity = g.Sum(i => i.Quantity),
                    Amount = g.Sum(i => i.Subtotal),
                    Transactions = g.Select(i => i.TransactionId).Distinct().Count()
                })
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var x in grouped)
            {
                report.Rows.Add(new List<string>
                {
                    x.Code,
                    x.Name,
                    x.Category,
                    ReportCount(x.Quantity),
                    ReportAmount(x.Amount),
                    ReportCount(x.Transactions)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(grouped.Count, "product", "products")),
                (3, ReportCount(grouped.Sum(x => x.Quantity))),
                (4, ReportAmount(grouped.Sum(x => x.Amount))));

            return report;
        }

        private static GeneratedReport BuildLoyaltyReport(List<LoyaltyReportRow> rows, string period)
        {
            var report = NewReport("loyalty", "Loyalty Report", period,
                Col("Date"), Col("Customer Code"), Col("Customer"), Col("Type"),
                Col("Points Earned", true), Col("Points Used", true), Col("Sale Txn ID", true));

            foreach (var r in rows)
            {
                report.Rows.Add(new List<string>
                {
                    ReportDate(r.Date),
                    ReportText(r.CustomerCode),
                    ReportCustomer(r.CustomerName),
                    ReportText(r.TransactionType),
                    ReportCount(r.PointsEarned),
                    ReportCount(r.PointsUsed),
                    r.SalesTransactionId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "entry", "entries")),
                (4, ReportCount(rows.Sum(r => r.PointsEarned))),
                (5, ReportCount(rows.Sum(r => r.PointsUsed))));

            return report;
        }

        private static GeneratedReport BuildPromotionReport(
            List<PromotionReportRow> promotions,
            List<PromotionUsageRow> usage,
            string period)
        {
            var report = NewReport("promotions", "Promotion Report", period,
                Col("Promotion"), Col("Type"), Col("Value", true), Col("Min Purchase (₱)", true),
                Col("Required Points", true), Col("Valid From"), Col("Valid To"), Col("Status"),
                Col("Times Used", true), Col("Total Discount (₱)", true), Col("Sales Generated (₱)", true));

            var usageById = usage.ToDictionary(u => u.PromotionId);

            var rows = promotions
                .Select(p =>
                {
                    usageById.TryGetValue(p.PromotionId, out var u);
                    return new { Promotion = p, Usage = u };
                })
                .OrderByDescending(x => x.Usage?.TimesUsed ?? 0)
                .ThenBy(x => x.Promotion.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var x in rows)
            {
                var p = x.Promotion;
                string value = string.Equals(p.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? p.DiscountValue.ToString("0.##", CultureInfo.InvariantCulture) + "%"
                    : ReportAmount(p.DiscountValue);

                report.Rows.Add(new List<string>
                {
                    ReportText(p.Name),
                    ReportText(p.DiscountType),
                    value,
                    ReportAmount(p.MinimumPurchase),
                    ReportCount(p.RequiredLoyaltyPoints),
                    ReportDate(p.StartDate),
                    ReportDate(p.EndDate),
                    ReportText(p.Status),
                    ReportCount(x.Usage?.TimesUsed ?? 0),
                    ReportAmount(x.Usage?.TotalDiscount ?? 0m),
                    ReportAmount(x.Usage?.SalesGenerated ?? 0m)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "promotion", "promotions")),
                (8, ReportCount(rows.Sum(x => x.Usage?.TimesUsed ?? 0))),
                (9, ReportAmount(rows.Sum(x => x.Usage?.TotalDiscount ?? 0m))),
                (10, ReportAmount(rows.Sum(x => x.Usage?.SalesGenerated ?? 0m))));

            return report;
        }

        private static GeneratedReport BuildDiscountReport(List<SalesReportRow> rows, string period)
        {
            var report = NewReport("discounts", "Discount Report", period,
                Col("Date"), Col("Txn ID", true), Col("Customer"), Col("Promotion"),
                Col("Subtotal (₱)", true), Col("Promotion Discount (₱)", true),
                Col("Customer Discount (₱)", true), Col("Total Discount (₱)", true),
                Col("Final Amount (₱)", true));

            foreach (var r in rows)
            {
                report.Rows.Add(new List<string>
                {
                    ReportDate(r.Date),
                    r.TransactionId.ToString(CultureInfo.InvariantCulture),
                    ReportCustomer(r.CustomerName),
                    ReportText(r.PromotionName),
                    ReportAmount(r.TotalAmount),
                    ReportAmount(r.DiscountAmount),
                    ReportAmount(r.CustomerDiscountAmount),
                    ReportAmount(r.DiscountAmount + r.CustomerDiscountAmount),
                    ReportAmount(r.FinalAmount)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "sale", "sales")),
                (4, ReportAmount(rows.Sum(r => r.TotalAmount))),
                (5, ReportAmount(rows.Sum(r => r.DiscountAmount))),
                (6, ReportAmount(rows.Sum(r => r.CustomerDiscountAmount))),
                (7, ReportAmount(rows.Sum(r => r.DiscountAmount + r.CustomerDiscountAmount))),
                (8, ReportAmount(rows.Sum(r => r.FinalAmount))));

            return report;
        }

        private static GeneratedReport BuildFeedbackReport(List<FeedbackReportRow> rows, string period)
        {
            var report = NewReport("feedback", "Feedback and Complaints Report", period,
                Col("ID", true), Col("Date"), Col("Customer"), Col("Type"), Col("Category"), Col("Status"), Col("Comment"));

            foreach (var r in rows)
            {
                report.Rows.Add(new List<string>
                {
                    r.FeedbackId.ToString(CultureInfo.InvariantCulture),
                    ReportDate(r.Date),
                    ReportCustomer(r.CustomerName),
                    ReportText(r.Type),
                    ReportText(r.Category),
                    ReportText(r.Status),
                    ReportText(r.Comment)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "record", "records")));

            return report;
        }

        private static GeneratedReport BuildInquiryReport(List<InquiryReportRow> rows, string period)
        {
            var report = NewReport("inquiries", "Inquiry Report", period,
                Col("ID", true), Col("Date"), Col("Customer"), Col("Type"), Col("Source"),
                Col("Subject"), Col("Status"), Col("Responded At"), Col("Responded By"));

            foreach (var r in rows)
            {
                report.Rows.Add(new List<string>
                {
                    r.InquiryId.ToString(CultureInfo.InvariantCulture),
                    ReportDate(r.Date),
                    ReportCustomer(r.CustomerName),
                    ReportText(r.Type),
                    ReportText(r.Source),
                    ReportText(r.Subject),
                    ReportText(r.Status),
                    ReportDateTime(r.RespondedAt),
                    ReportText(r.RespondedBy)
                });
            }

            report.Totals = TotalsRow(report.Columns.Count,
                (0, "TOTAL"),
                (1, Plural(rows.Count, "inquiry", "inquiries")));

            return report;
        }

        #endregion

        #region Generated report endpoint

        [HttpGet("generate")]
        [RequireTenantPermission(TenantPermissions.GenerateReports)]
        public async Task<IActionResult> GenerateReport(int companyId, string? reportType = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            string key = (reportType ?? string.Empty).Trim().ToLowerInvariant();

            if (!GeneratedReportTypes.Contains(key))
            {
                return BadRequest(UnknownReportTypeMessage);
            }

            if (!HttpContext.HasTenantPermission(TenantPermissions.ReportPermission(key)))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "ReportNotAllowed",
                    message = "Your subscription plan or role does not allow this report."
                });
            }

            // Inventory is a current-stock snapshot, so it is the only report without a date range.
            bool usesDateRange = key != "inventory";
            DateTime start = default;
            DateTime end = default;
            string period;

            if (usesDateRange)
            {
                if (startDate == null || endDate == null)
                {
                    return BadRequest(InvalidDateRangeMessage);
                }

                if (!TryGetReportRange(null, null, startDate, endDate, out start, out end, out var rangeError))
                {
                    return BadRequest(rangeError);
                }

                period = ReportPeriodLabel(start, end);
            }
            else
            {
                period = "As of " + DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            // PREMIUM (Branching): ADMIN reports stay company-wide. A MANAGER's sales, stock, discount, promotion-usage,
            // customer-spend and loyalty figures cover only the assigned branch; without an active assigned branch
            // every report is returned empty instead of company-wide.
            int? branchScope = null;
            bool noData = false;

            if (BranchStock.IsBranchingCompany(HttpContext) && !BranchStock.IsAdmin(HttpContext))
            {
                var assigned = await BranchStock.GetAssignedBranchAsync(tenantDb, User);

                if (assigned == null)
                {
                    noData = true;
                    period += " | Not assigned to a branch - ask your administrator";
                }
                else
                {
                    branchScope = assigned.BranchId;
                    period += " | Branch: " + assigned.BranchName;
                }
            }

            GeneratedReport report;

            switch (key)
            {
                case "sales":
                    report = BuildSalesReport(
                        noData ? new() : await GetSalesReportRowsAsync(tenantDb, start, end, false, branchScope), period);
                    break;

                case "customers":
                    report = BuildCustomerReport(
                        noData ? new() : await GetCustomerReportRowsAsync(tenantDb, branchScope),
                        noData ? new() : await GetCustomerSpendRowsAsync(tenantDb, start, end, branchScope),
                        noData ? new() : await GetLastPurchasesAsync(tenantDb, end, branchScope),
                        period);
                    break;

                case "inventory":
                    report = BuildInventoryReport(
                        noData ? new() : await GetInventoryReportRowsAsync(tenantDb, branchScope), period);
                    break;

                case "product-sales":
                    report = BuildProductSalesReport(
                        noData ? new() : await GetProductSaleItemRowsAsync(tenantDb, start, end, branchScope), period);
                    break;

                case "loyalty":
                    report = BuildLoyaltyReport(
                        noData ? new() : await GetLoyaltyReportRowsAsync(tenantDb, start, end, branchScope), period);
                    break;

                case "promotions":
                    report = BuildPromotionReport(
                        noData ? new() : await GetPromotionReportRowsAsync(tenantDb, branchScope),
                        noData ? new() : await GetPromotionUsageRowsAsync(tenantDb, start, end, branchScope),
                        period);
                    break;

                case "discounts":
                    report = BuildDiscountReport(
                        noData ? new() : await GetSalesReportRowsAsync(tenantDb, start, end, true, branchScope), period);
                    break;

                case "feedback":
                    report = BuildFeedbackReport(
                        noData ? new() : await GetFeedbackReportRowsAsync(tenantDb, start, end, branchScope), period);
                    break;

                case "business-summary":
                    report = noData
                        ? NewReport("business-summary", "Business Summary Report", period, Col("Section"), Col("Metric"), Col("Value"))
                        : await BuildBusinessSummaryReportAsync(tenantDb, start, end, branchScope, period);
                    break;

                default:
                    report = BuildInquiryReport(
                        noData ? new() : await GetInquiryReportRowsAsync(tenantDb, start, end, branchScope), period);
                    break;
            }

            if (!HttpContext.HasTenantPermission(TenantPermissions.UsePromotions))
            {
                RemoveColumns(report, "Promotion", "Promotion Discount (₱)", "Points Earned", "Points Used", "Current Points");
            }

            return Ok(report);
        }

        private static void RemoveColumns(GeneratedReport report, params string[] headers)
        {
            foreach (var header in headers)
            {
                int index = report.Columns.FindIndex(c => c.Header == header);

                if (index < 0)
                {
                    continue;
                }

                report.Columns.RemoveAt(index);

                foreach (var row in report.Rows)
                {
                    row.RemoveAt(index);
                }

                if (report.Totals.Count > index)
                {
                    report.Totals.RemoveAt(index);
                }
            }
        }

        private static async Task<List<SalesReportRow>> GetSalesReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, bool discountedOnly, int? branchId = null)
        {
            // Same rule as the BI figures: completed, non-deleted sales only.
            var query = db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end);

            if (discountedOnly)
            {
                query = query.Where(x => x.DiscountAmount > 0 || x.CustomerDiscountAmount > 0);
            }

            if (branchId != null)
            {
                query = query.Where(x => x.BranchId == branchId);
            }

            return await query
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.TransactionId)
                .Select(x => new SalesReportRow(
                    x.TransactionId,
                    x.TransactionDate,
                    x.Customer != null ? x.Customer.CustomerCode : string.Empty,
                    x.Customer != null ? x.Customer.FirstName + " " + x.Customer.LastName : string.Empty,
                    x.PaymentMethod,
                    x.TotalAmount,
                    x.Promotion != null ? x.Promotion.PromotionName : null,
                    x.DiscountAmount,
                    x.CustomerDiscountAmount,
                    x.FinalAmount,
                    x.PointsEarned,
                    x.PointsUsed))
                .ToListAsync();
        }

        // Branch scope: customers associated with that branch (registered or bought there).
        private static async Task<List<CustomerReportRow>> GetCustomerReportRowsAsync(TenantCrmDbContext db, int? branchId = null)
        {
            var customers = db.Customers.AsNoTracking();

            if (branchId != null)
            {
                customers = BranchStock.AssociatedWith(customers, db, branchId.Value);
            }

            return await customers
                .Select(c => new CustomerReportRow(
                    c.CustomerId,
                    c.CustomerCode,
                    c.FirstName,
                    c.LastName,
                    c.Email,
                    c.ContactNo,
                    c.Status,
                    c.CreatedAt,
                    c.LoyaltyPoints))
                .ToListAsync();
        }

        // Latest completed purchase before the end of the report period (branch scope: at that branch only),
        // so a Manager can see who has not bought from their branch recently.
        private static async Task<Dictionary<int, DateTime>> GetLastPurchasesAsync(TenantCrmDbContext db, DateTime end, int? branchId = null)
        {
            return await db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate < end &&
                            (branchId == null || x.BranchId == branchId))
                .GroupBy(x => x.CustomerId)
                .Select(g => new { CustomerId = g.Key, Last = g.Max(x => x.TransactionDate) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Last);
        }

        private static async Task<List<CustomerSpendRow>> GetCustomerSpendRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            var totals = await db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end &&
                            (branchId == null || x.BranchId == branchId))
                .GroupBy(x => x.CustomerId)
                .Select(g => new { CustomerId = g.Key, Purchases = g.Count(), Amount = g.Sum(x => x.FinalAmount) })
                .ToListAsync();

            return totals
                .Select(t => new CustomerSpendRow(t.CustomerId, t.Purchases, t.Amount))
                .ToList();
        }

        // Branch scope: the stock held at that branch (BranchInventory); a product the branch never stocked shows 0.
        private static async Task<List<InventoryReportRow>> GetInventoryReportRowsAsync(TenantCrmDbContext db, int? branchId = null)
        {
            if (branchId != null)
            {
                int scope = branchId.Value;

                var branchStock = await db.BranchInventories
                    .AsNoTracking()
                    .Where(i => i.BranchId == scope)
                    .ToDictionaryAsync(i => i.ProductId, i => i.Quantity);

                var products = await db.Products
                    .AsNoTracking()
                    .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.Category, p.Price, p.ReorderLevel, p.Status })
                    .ToListAsync();

                return products
                    .Select(p => new InventoryReportRow(
                        p.ProductCode,
                        p.ProductName,
                        p.Category,
                        p.Price,
                        branchStock.TryGetValue(p.ProductId, out var quantity) ? quantity : 0,
                        p.ReorderLevel,
                        p.Status))
                    .ToList();
            }

            return await db.Products
                .AsNoTracking()
                .Select(p => new InventoryReportRow(
                    p.ProductCode,
                    p.ProductName,
                    p.Category,
                    p.Price,
                    p.Quantity,
                    p.ReorderLevel,
                    p.Status))
                .ToListAsync();
        }

        private static async Task<List<ProductSaleItemRow>> GetProductSaleItemRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            return await db.TransactionItems
                .AsNoTracking()
                .Where(ti => ti.SalesTransaction != null &&
                             !ti.SalesTransaction.IsDeleted &&
                             ti.SalesTransaction.Status == "Completed" &&
                             ti.SalesTransaction.TransactionDate >= start &&
                             ti.SalesTransaction.TransactionDate < end &&
                             (branchId == null || ti.SalesTransaction.BranchId == branchId))
                .Select(ti => new ProductSaleItemRow(
                    ti.ProductId,
                    ti.TransactionId,
                    ti.Product != null ? ti.Product.ProductCode : string.Empty,
                    ti.Product != null ? ti.Product.ProductName : string.Empty,
                    ti.Product != null ? ti.Product.Category : string.Empty,
                    ti.Quantity,
                    ti.Subtotal))
                .ToListAsync();
        }

        // Branch scope: only points earned/used on that branch's sales (manual adjustments have no branch).
        private static async Task<List<LoyaltyReportRow>> GetLoyaltyReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            // Same rule as MonthReport.GetPointsAsync, so report totals match the dashboard points KPIs.
            return await db.LoyaltyTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Date >= start &&
                            x.Date < end &&
                            x.TransactionType != CancellationType &&
                            (x.SalesTransactionId == null ||
                             (x.SalesTransaction != null &&
                              !x.SalesTransaction.IsDeleted &&
                              x.SalesTransaction.Status == "Completed")) &&
                            (branchId == null ||
                             (x.SalesTransaction != null && x.SalesTransaction.BranchId == branchId)))
                .OrderBy(x => x.Date)
                .ThenBy(x => x.LoyaltyTransactionId)
                .Select(x => new LoyaltyReportRow(
                    x.Date,
                    x.Customer != null ? x.Customer.CustomerCode : string.Empty,
                    x.Customer != null ? x.Customer.FirstName + " " + x.Customer.LastName : string.Empty,
                    x.TransactionType,
                    x.PointsEarned,
                    x.PointsUsed,
                    x.SalesTransactionId))
                .ToListAsync();
        }

        // Branch scope: company-wide promotions plus that branch's own promotions.
        private static async Task<List<PromotionReportRow>> GetPromotionReportRowsAsync(TenantCrmDbContext db, int? branchId = null)
        {
            return await db.Promotions
                .AsNoTracking()
                .Where(p => branchId == null || p.BranchId == null || p.BranchId == branchId)
                .Select(p => new PromotionReportRow(
                    p.PromotionId,
                    p.PromotionName,
                    p.DiscountType,
                    p.DiscountValue,
                    p.MinimumPurchase,
                    p.RequiredLoyaltyPoints,
                    p.StartDate,
                    p.EndDate,
                    p.Status))
                .ToListAsync();
        }

        private static async Task<List<PromotionUsageRow>> GetPromotionUsageRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            var usage = await db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.PromotionId != null &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end &&
                            (branchId == null || x.BranchId == branchId))
                .GroupBy(x => x.PromotionId)
                .Select(g => new
                {
                    PromotionId = g.Key,
                    TimesUsed = g.Count(),
                    Discount = g.Sum(x => x.DiscountAmount),
                    Sales = g.Sum(x => x.FinalAmount)
                })
                .ToListAsync();

            return usage
                .Where(u => u.PromotionId != null)
                .Select(u => new PromotionUsageRow(u.PromotionId!.Value, u.TimesUsed, u.Discount, u.Sales))
                .ToList();
        }

        private static async Task<List<FeedbackReportRow>> GetFeedbackReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            return await db.Feedbacks
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end &&
                            (branchId == null || x.BranchId == branchId))
                .OrderBy(x => x.DateSubmitted)
                .ThenBy(x => x.FeedbackId)
                .Select(x => new FeedbackReportRow(
                    x.FeedbackId,
                    x.DateSubmitted,
                    x.Customer != null ? x.Customer.FirstName + " " + x.Customer.LastName : string.Empty,
                    x.Type,
                    x.Category,
                    x.Status,
                    x.Comment))
                .ToListAsync();
        }

        private static async Task<List<InquiryReportRow>> GetInquiryReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, int? branchId = null)
        {
            return await db.Inquiries
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end &&
                            (branchId == null || x.BranchId == branchId))
                .OrderBy(x => x.DateSubmitted)
                .ThenBy(x => x.InquiryId)
                .Select(x => new InquiryReportRow(
                    x.InquiryId,
                    x.DateSubmitted,
                    x.Customer != null ? x.Customer.FirstName + " " + x.Customer.LastName : string.Empty,
                    x.Type,
                    x.Source,
                    x.Subject,
                    x.Status,
                    x.RespondedAt,
                    x.RespondedBy))
                .ToListAsync();
        }

        #endregion
    }
}