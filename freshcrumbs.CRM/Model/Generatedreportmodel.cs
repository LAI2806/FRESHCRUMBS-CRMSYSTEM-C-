namespace freshcrumbs.CRM.winforms.Models
{
    public class GeneratedReportColumnModel
    {
        public string Header { get; set; } = string.Empty;
        public string Align { get; set; } = "Left";
    }

    public class GeneratedReportModel
    {
        public string ReportType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public List<GeneratedReportColumnModel> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
        public List<string> Totals { get; set; } = new();
    }
}