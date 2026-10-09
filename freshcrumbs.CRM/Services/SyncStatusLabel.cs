using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    // Small Online / Offline / Syncing indicator for the top bar. Click it to sync right now.
    // It only reads the status of the local service; it hides itself when the API runs in cloud mode.
    public class SyncStatusLabel : Label
    {
        private readonly SyncStatusClient _client = new();
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 5000 };
        private readonly ToolTip _tip = new();
        private bool _busy;

        public SyncStatusLabel()
        {
            AutoSize = false;
            Width = 340;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(16, 0, 0, 0);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Text = "Checking connection...";
            ForeColor = Color.Gray;

            Click += async (_, _) => await SyncNowAsync();

            _timer.Tick += async (_, _) => await RefreshAsync();
            _timer.Start();

            HandleCreated += async (_, _) => await RefreshAsync();
        }

        private async Task SyncNowAsync()
        {
            try
            {
                await _client.SyncNowAsync();
            }
            catch
            {
                // the next refresh shows the real state
            }
        }

        private async Task RefreshAsync()
        {
            if (_busy || IsDisposed)
            {
                return;
            }

            _busy = true;

            try
            {
                var status = await _client.GetStatusAsync();

                if (IsDisposed)
                {
                    return;
                }

                if (status == null)
                {
                    Show("Local service not responding", Color.Firebrick, null);
                    return;
                }

                if (string.Equals(status.Mode, "Cloud", StringComparison.OrdinalIgnoreCase))
                {
                    Visible = false;
                    return;
                }

                Visible = true;

                var pending = status.Pending > 0 ? $" · {status.Pending} pending" : string.Empty;
                var attention = status.Rejected > 0 ? $" · {status.Rejected} need attention" : string.Empty;

                var tip = status.LastSyncUtc == null
                    ? "Not synced yet."
                    : $"Last sync: {status.LastSyncUtc.Value.ToLocalTime():g}";

                if (status.GraceDaysLeft != null)
                {
                    tip += $"{Environment.NewLine}Offline access valid for {status.GraceDaysLeft:0.#} more day(s) without an online check.";
                }

                if (!string.IsNullOrWhiteSpace(status.LastError))
                {
                    tip += $"{Environment.NewLine}{status.LastError}";
                }

                switch (status.State)
                {
                    case "Syncing":
                        Show("● Syncing..." + pending, Color.SteelBlue, tip);
                        break;

                    case "Online":
                        Show(status.Pending == 0
                            ? "● Online · All synced"
                            : "● Online" + pending + attention,
                            Color.SeaGreen, tip);
                        break;

                    case "Offline":
                        Show("● Offline · working locally" + pending + attention, Color.DarkOrange, tip);
                        break;

                    case "NeedsSignIn":
                        Show("● Sign in online to sync" + pending + attention, Color.DarkOrange, tip);
                        break;

                    default:
                        Show("● Sync problem" + pending + attention, Color.Firebrick, tip);
                        break;
                }
            }
            catch
            {
                if (!IsDisposed)
                {
                    Show("Local service not responding", Color.Firebrick, null);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private void Show(string text, Color color, string? tip)
        {
            Text = text;
            ForeColor = color;
            _tip.SetToolTip(this, tip ?? string.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _tip.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}