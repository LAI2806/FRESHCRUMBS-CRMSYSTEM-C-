namespace freshcrumbs.CRM.winforms.Models
{
    /// <summary>
    /// Describes what the user clicked in Reports (a KPI card, a chart point or an insight)
    /// so the destination module can open pre-filtered.
    /// </summary>
    public class ReportDrilldown
    {
        /// <summary>Navigation key: customers, products, sales, loyalty, promotions, feedback, inquiries.</summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>Human-readable origin, e.g. "KPI: Total Sales" or "Chart: Sales Trend".</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Inclusive start of the report period. Null when the metric is not period-bound.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Inclusive end of the report period. Null when the metric is not period-bound.</summary>
        public DateTime? EndDate { get; set; }

        public string? Status { get; set; }

        public string? Type { get; set; }

        public string? Category { get; set; }

        public string? Promotion { get; set; }

        /// <summary>Free-text match, e.g. a product, promotion or customer name from a chart bar.</summary>
        public string? SearchText { get; set; }
    }

    /// <summary>
    /// Implement on a module control (ProductControl, SalesControl, CustomerControl, ...) so that
    /// Reports can open it already filtered. Called right after the control is added to the
    /// content panel, so implementations should store the filter and apply it once their data loads.
    /// </summary>
    public interface IReportDrilldownTarget
    {
        void ApplyReportDrilldown(ReportDrilldown drilldown);
    }
}