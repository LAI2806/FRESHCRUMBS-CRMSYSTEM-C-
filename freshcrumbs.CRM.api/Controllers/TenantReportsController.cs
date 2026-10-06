using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            public MonthReport(TenantCrmDbContext db, DateTime start, DateTime end)
            {
                _db = db;
                Start = start;
                End = end;
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

                return _sales ??= await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate >= start &&
                                x.TransactionDate < end)
                    .Select(x => new SaleRow(x.CustomerId, x.PromotionId, x.TransactionDate, x.FinalAmount, x.DiscountAmount))
                    .ToListAsync();
            }

            public async Task<List<ItemRow>> GetItemsAsync()
            {
                var start = Start;
                var end = End;

                return _items ??= await _db.TransactionItems
                    .AsNoTracking()
                    .Where(ti => ti.SalesTransaction != null &&
                                 !ti.SalesTransaction.IsDeleted &&
                                 ti.SalesTransaction.Status == "Completed" &&
                                 ti.SalesTransaction.TransactionDate >= start &&
                                 ti.SalesTransaction.TransactionDate < end)
                    .Select(ti => new ItemRow(
                        ti.ProductId,
                        ti.Product != null ? ti.Product.ProductName : string.Empty,
                        ti.Product != null ? ti.Product.Category : string.Empty,
                        ti.Quantity,
                        ti.Subtotal))
                    .ToListAsync();
            }

            public async Task<List<PromotionRow>> GetPromotionsAsync()
            {
                return _promotions ??= await _db.Promotions
                    .AsNoTracking()
                    .Select(p => new PromotionRow(p.PromotionId, p.PromotionName, p.Status))
                    .ToListAsync();
            }

            public async Task<List<FeedbackRow>> GetFeedbackAsync()
            {
                var start = Start;
                var end = End;

                return _feedback ??= await _db.Feedbacks
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end)
                    .Select(x => new FeedbackRow(x.Type, x.Category, x.Status, x.DateSubmitted))
                    .ToListAsync();
            }

            public async Task<List<InquiryRow>> GetInquiriesAsync()
            {
                var start = Start;
                var end = End;

                return _inquiries ??= await _db.Inquiries
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end)
                    .Select(x => new InquiryRow(x.Type, x.Status, x.RespondedAt, x.DateSubmitted))
                    .ToListAsync();
            }

            public async Task<List<StockRow>> GetActiveProductsAsync()
            {
                return _activeProducts ??= await _db.Products
                    .AsNoTracking()
                    .Where(p => p.Status == "Active")
                    .Select(p => new StockRow(p.ProductId, p.ProductName, p.Quantity, p.ReorderLevel))
                    .ToListAsync();
            }

            public async Task<(int Earned, int Used)> GetPointsAsync()
            {
                var start = Start;
                var end = End;

                var totals = await _db.LoyaltyTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Date >= start &&
                                x.Date < end &&
                                x.TransactionType != CancellationType &&
                                (x.SalesTransactionId == null ||
                                 (x.SalesTransaction != null &&
                                  !x.SalesTransaction.IsDeleted &&
                                  x.SalesTransaction.Status == "Completed")))
                    .GroupBy(x => 1)
                    .Select(g => new { Earned = g.Sum(x => x.PointsEarned), Used = g.Sum(x => x.PointsUsed) })
                    .FirstOrDefaultAsync();

                return (totals?.Earned ?? 0, totals?.Used ?? 0);
            }

            public async Task<decimal> GetPreviousMonthSalesAsync()
            {
                var previousStart = PreviousStart;
                var start = Start;

                return await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate >= previousStart &&
                                x.TransactionDate < start)
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

                var returningIds = await _db.SalesTransactions
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted &&
                                x.Status == "Completed" &&
                                x.TransactionDate < start &&
                                customerIds.Contains(x.CustomerId))
                    .Select(x => x.CustomerId)
                    .Distinct()
                    .ToListAsync();

                return (customerIds.Count - returningIds.Count, returningIds.Count);
            }

            public async Task<List<ChartPoint>> GetCustomerGrowthAsync()
            {
                var start = Start;
                var end = End;

                int running = await _db.Customers
                    .AsNoTracking()
                    .CountAsync(c => c.CreatedAt < start);

                var createdInMonth = await _db.Customers
                    .AsNoTracking()
                    .Where(c => c.CreatedAt >= start && c.CreatedAt < end)
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

            public async Task<List<ChartPoint>> GetTopLoyalCustomersAsync()
            {
                var rows = await _db.Customers
                    .AsNoTracking()
                    .Where(c => c.Status == "Active" && c.LoyaltyPoints > 0)
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

            var report = new MonthReport(tenantDb, start, end);

            var sales = await report.GetSalesAsync();
            decimal totalSales = sales.Sum(x => x.FinalAmount);
            int transactionCount = sales.Count;
            decimal averageTransaction = transactionCount > 0 ? totalSales / transactionCount : 0;

            var items = await report.GetItemsAsync();
            int totalProductsSold = items.Sum(x => x.Quantity);

            int totalCustomers = await tenantDb.Customers
                .AsNoTracking()
                .CountAsync(x => x.Status == "Active");

            int newCustomers = await tenantDb.Customers
                .AsNoTracking()
                .CountAsync(x => x.CreatedAt >= DateTime.UtcNow.AddDays(-30));

            var customerTransactionCounts = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status == "Completed")
                .GroupBy(x => x.CustomerId)
                .Select(g => g.Count())
                .ToListAsync();

            int customersWithPurchases = customerTransactionCounts.Count;
            int repeatCustomers = customerTransactionCounts.Count(c => c > 1);
            double repeatRate = customersWithPurchases > 0
                ? Math.Round(repeatCustomers * 100.0 / customersWithPurchases, 1)
                : 0;

            var promotions = await report.GetPromotionsAsync();
            int totalActivePromotions = promotions.Count(p => IsActive(p.Status));

            var (earnedPoints, usedPoints) = await report.GetPointsAsync();

            decimal previousPeriodSales = await report.GetPreviousMonthSalesAsync();

            var feedback = await report.GetFeedbackAsync();
            int resolvedFeedback = feedback.Count(f => !IsComplaint(f) && IsStatus(f.Status, ResolvedStatus));
            int resolvedComplaints = feedback.Count(f => IsComplaint(f) && IsStatus(f.Status, ResolvedStatus));
            double resolutionRate = Rate(resolvedFeedback + resolvedComplaints, feedback.Count);

            var inquiries = await report.GetInquiriesAsync();
            int respondedInquiries = inquiries.Count(i => i.RespondedAt != null);
            double responseRate = Rate(respondedInquiries, inquiries.Count);

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
        public async Task<IActionResult> GetInsights(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!TryGetReportRange(year, month, startDate, endDate, out var start, out var end, out var rangeError))
            {
                return BadRequest(rangeError);
            }

            await using var tenantDb = await _tenantFactory.CreateAsync(companyId);

            var report = new MonthReport(tenantDb, start, end);
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

            var report = new MonthReport(tenantDb, start, end);

            var customerGrowth = await report.GetCustomerGrowthAsync();
            var (newCustomers, returningCustomers) = await report.GetNewVsReturningAsync();
            var newVsReturning = new List<ChartPoint>
            {
                new ChartPoint { Label = "New Customers", Value = newCustomers },
                new ChartPoint { Label = "Returning Customers", Value = returningCustomers }
            };
            var topLoyalCustomers = await report.GetTopLoyalCustomersAsync();

            var productPerformance = await report.GetTopProductsAsync();
            var salesByCategory = await report.GetSalesByCategoryAsync();
            var productStock = await report.GetProductStockAsync();

            var salesTrend = await report.GetSalesTrendAsync();
            var salesByPromotion = await report.GetSalesByPromotionAsync();
            var discountByPromotion = await report.GetDiscountByPromotionAsync();

            var promotionStatus = await report.GetPromotionStatusAsync();
            var promotionUsage = await report.GetPromotionUsageAsync();

            var feedback = await report.GetFeedbackAsync();
            var complaintsByCategory = CountByCategory(feedback.Where(IsComplaint));
            var feedbackByCategory = CountByCategory(feedback.Where(f => !IsComplaint(f)));
            var feedbackTrend = report.GetDailyCounts(feedback.Select(f => f.Date));

            var inquiries = await report.GetInquiriesAsync();
            var inquiryTrend = report.GetDailyCounts(inquiries.Select(i => i.Date));
            var inquiryTypes = inquiries
                .GroupBy(i => LabelOrDefault(i.Type, "Unspecified"), StringComparer.OrdinalIgnoreCase)
                .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                .OrderByDescending(p => p.Value)
                .ToList();

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

            int activeCustomers = await tenantDb.Customers
                .AsNoTracking()
                .CountAsync(c => c.Status == "Active");

            int activeProducts = await tenantDb.Products
                .AsNoTracking()
                .CountAsync(p => p.Status == "Active");

            decimal todaysSales = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= today &&
                            x.TransactionDate < tomorrow)
                .SumAsync(x => (decimal?)x.FinalAmount) ?? 0m;

            var lowStockProducts = await tenantDb.Products
                .AsNoTracking()
                .Where(p => p.Status == "Active" && p.Quantity <= p.ReorderLevel)
                .OrderBy(p => p.Quantity)
                .Select(p => new { p.ProductName, p.Quantity, p.ReorderLevel })
                .ToListAsync();

            var recentSalesRaw = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status == "Completed")
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
                            x.TransactionDate < tomorrow)
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

            int newCustomersThisMonth = await tenantDb.Customers
                .AsNoTracking()
                .CountAsync(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd);

            int activeCustomersThisMonth = await tenantDb.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= monthStart &&
                            x.TransactionDate < monthEnd)
                .Select(x => x.CustomerId)
                .Distinct()
                .CountAsync();

            int loyaltyActivityThisMonth = await tenantDb.LoyaltyTransactions
                .AsNoTracking()
                .CountAsync(x => !x.IsDeleted && x.Date >= monthStart && x.Date < monthEnd);

            return Ok(new
            {
                ActiveCustomers = activeCustomers,
                ActiveProducts = activeProducts,
                TodaysSales = todaysSales,
                LowStockCount = lowStockProducts.Count,
                RecentSales = recentSales,
                LowStockProducts = lowStockProducts,
                SalesOverview = salesOverview,
                NewCustomersThisMonth = newCustomersThisMonth,
                ActiveCustomersThisMonth = activeCustomersThisMonth,
                LoyaltyActivityThisMonth = loyaltyActivityThisMonth
            });
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
            "inquiries"
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
            string period)
        {
            var report = NewReport("customers", "Customer Report", period,
                Col("Code"), Col("Name"), Col("Email"), Col("Contact No"), Col("Status"), Col("Date Registered"),
                Col("Purchases (Period)", true), Col("Amount Spent (₱)", true), Col("Current Points", true));

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
        public async Task<IActionResult> GenerateReport(int companyId, string? reportType = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            string key = (reportType ?? string.Empty).Trim().ToLowerInvariant();

            if (!GeneratedReportTypes.Contains(key))
            {
                return BadRequest(UnknownReportTypeMessage);
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

            GeneratedReport report;

            switch (key)
            {
                case "sales":
                    report = BuildSalesReport(await GetSalesReportRowsAsync(tenantDb, start, end, false), period);
                    break;

                case "customers":
                    report = BuildCustomerReport(
                        await GetCustomerReportRowsAsync(tenantDb),
                        await GetCustomerSpendRowsAsync(tenantDb, start, end),
                        period);
                    break;

                case "inventory":
                    report = BuildInventoryReport(await GetInventoryReportRowsAsync(tenantDb), period);
                    break;

                case "product-sales":
                    report = BuildProductSalesReport(await GetProductSaleItemRowsAsync(tenantDb, start, end), period);
                    break;

                case "loyalty":
                    report = BuildLoyaltyReport(await GetLoyaltyReportRowsAsync(tenantDb, start, end), period);
                    break;

                case "promotions":
                    report = BuildPromotionReport(
                        await GetPromotionReportRowsAsync(tenantDb),
                        await GetPromotionUsageRowsAsync(tenantDb, start, end),
                        period);
                    break;

                case "discounts":
                    report = BuildDiscountReport(await GetSalesReportRowsAsync(tenantDb, start, end, true), period);
                    break;

                case "feedback":
                    report = BuildFeedbackReport(await GetFeedbackReportRowsAsync(tenantDb, start, end), period);
                    break;

                default:
                    report = BuildInquiryReport(await GetInquiryReportRowsAsync(tenantDb, start, end), period);
                    break;
            }

            return Ok(report);
        }

        private static async Task<List<SalesReportRow>> GetSalesReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end, bool discountedOnly)
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

        private static async Task<List<CustomerReportRow>> GetCustomerReportRowsAsync(TenantCrmDbContext db)
        {
            return await db.Customers
                .AsNoTracking()
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

        private static async Task<List<CustomerSpendRow>> GetCustomerSpendRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end)
        {
            var totals = await db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end)
                .GroupBy(x => x.CustomerId)
                .Select(g => new { CustomerId = g.Key, Purchases = g.Count(), Amount = g.Sum(x => x.FinalAmount) })
                .ToListAsync();

            return totals
                .Select(t => new CustomerSpendRow(t.CustomerId, t.Purchases, t.Amount))
                .ToList();
        }

        private static async Task<List<InventoryReportRow>> GetInventoryReportRowsAsync(TenantCrmDbContext db)
        {
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
            TenantCrmDbContext db, DateTime start, DateTime end)
        {
            return await db.TransactionItems
                .AsNoTracking()
                .Where(ti => ti.SalesTransaction != null &&
                             !ti.SalesTransaction.IsDeleted &&
                             ti.SalesTransaction.Status == "Completed" &&
                             ti.SalesTransaction.TransactionDate >= start &&
                             ti.SalesTransaction.TransactionDate < end)
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

        private static async Task<List<LoyaltyReportRow>> GetLoyaltyReportRowsAsync(
            TenantCrmDbContext db, DateTime start, DateTime end)
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
                              x.SalesTransaction.Status == "Completed")))
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

        private static async Task<List<PromotionReportRow>> GetPromotionReportRowsAsync(TenantCrmDbContext db)
        {
            return await db.Promotions
                .AsNoTracking()
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
            TenantCrmDbContext db, DateTime start, DateTime end)
        {
            var usage = await db.SalesTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted &&
                            x.Status == "Completed" &&
                            x.PromotionId != null &&
                            x.TransactionDate >= start &&
                            x.TransactionDate < end)
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
            TenantCrmDbContext db, DateTime start, DateTime end)
        {
            return await db.Feedbacks
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end)
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
            TenantCrmDbContext db, DateTime start, DateTime end)
        {
            return await db.Inquiries
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.DateSubmitted >= start && x.DateSubmitted < end)
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