using freshcrumbs.CRM.winforms.Forms;

namespace freshcrumbs.CRM.winforms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TenantSelectionForm());
        }
    }
}