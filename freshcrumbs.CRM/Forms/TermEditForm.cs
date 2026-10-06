using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public enum TermsFormMode
    {
        Create,
        Edit,
        View
    }

    public class TermsEditForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly TermsFormMode _mode;
        private readonly int? _termsVersionId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private Label _metaLabel = null!;
        private TextBox _titleBox = null!;
        private TextBox _contentBox = null!;
        private DataGridView _acceptanceGrid = null!;
        private Label _acceptanceTitle = null!;
        private Button _saveButton = null!;
        private Label _errorLabel = null!;

        public TermsEditForm(TermsFormMode mode, int? termsVersionId)
        {
            _mode = mode;
            _termsVersionId = termsVersionId;

            Text = mode switch
            {
                TermsFormMode.Create => "New Terms Draft",
                TermsFormMode.Edit => "Edit Draft",
                _ => "Terms Version"
            };
            Width = 780;
            Height = 780;
            MinimumSize = new Size(700, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeControls();

            if (mode != TermsFormMode.Create)
            {
                Load += async (s, e) => await LoadVersionAsync();
            }
        }

        private void InitializeControls()
        {
            Controls.Add(new Label
            {
                Text = Text,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(25, 18),
                AutoSize = true
            });

            _metaLabel = new Label
            {
                Location = new Point(25, 55),
                Size = new Size(710, 22),
                ForeColor = LabelGray,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_metaLabel);

            Controls.Add(new Label { Text = "Title *", Location = new Point(25, 85), AutoSize = true, ForeColor = LabelGray });
            _titleBox = new TextBox
            {
                Location = new Point(25, 107),
                Width = 710,
                MaxLength = 200,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = _mode == TermsFormMode.View,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_titleBox);

            Controls.Add(new Label { Text = "Content *", Location = new Point(25, 145), AutoSize = true, ForeColor = LabelGray });

            bool view = _mode == TermsFormMode.View;

            _contentBox = new TextBox
            {
                Location = new Point(25, 167),
                Size = new Size(710, view ? 270 : 440),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                MaxLength = 100000,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = view,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | (view ? AnchorStyles.None : AnchorStyles.Bottom)
            };
            Controls.Add(_contentBox);

            if (view)
            {
                _acceptanceTitle = new Label
                {
                    Text = "Company acceptances",
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = TextDark,
                    Location = new Point(25, 450),
                    AutoSize = true,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left
                };
                Controls.Add(_acceptanceTitle);

                _acceptanceGrid = new DataGridView
                {
                    Location = new Point(25, 475),
                    Size = new Size(710, 150),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AutoGenerateColumns = false,
                    RowHeadersVisible = false,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };
                _acceptanceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyName", HeaderText = "Company", FillWeight = 35 });
                _acceptanceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyCode", HeaderText = "Code", FillWeight = 15 });
                _acceptanceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UserName", HeaderText = "Accepted By", FillWeight = 22 });
                _acceptanceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AcceptedText", HeaderText = "Accepted At", FillWeight = 28 });
                Controls.Add(_acceptanceGrid);
            }

            _errorLabel = new Label
            {
                Location = new Point(25, 640),
                Size = new Size(480, 40),
                ForeColor = Color.Firebrick,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_errorLabel);

            if (!view)
            {
                _saveButton = CreateButton("Save Draft", 25, 690, 120, AccentColor, Color.White);
                _saveButton.Click += SaveButton_Click;
            }

            var closeButton = CreateButton(view ? "Close" : "Cancel", view ? 25 : 155, 690, 110, Color.White, LabelGray);
            closeButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            if (_mode == TermsFormMode.Create)
            {
                _metaLabel.Text = "A new draft gets the next version number automatically. Drafts can be edited until they are published.";
            }
        }

        private Button CreateButton(string text, int x, int y, int width, Color back, Color fore)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = width,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            button.FlatAppearance.BorderSize = back == AccentColor ? 0 : 1;
            button.FlatAppearance.BorderColor = BorderColor;
            Controls.Add(button);
            return button;
        }

        private async Task LoadVersionAsync()
        {
            _errorLabel.ForeColor = AdminUi.InfoColor;
            _errorLabel.Text = "Loading...";

            try
            {
                var terms = await _apiService.GetTermsVersionAsync(_termsVersionId!.Value);

                _titleBox.Text = terms.Title;
                _contentBox.Text = terms.Content.Replace("\r\n", "\n").Replace("\n", "\r\n");

                _metaLabel.Text =
                    $"{terms.VersionText}  |  {terms.Status}  |  Created {terms.CreatedText} by {terms.CreatedBy}" +
                    (terms.PublishedAt != null ? $"  |  Published {terms.PublishedText}" : "") +
                    (terms.Status != "Draft" ? (terms.RequiresAcceptance ? "  |  Acceptance required" : "  |  Acceptance not required") : "");

                if (_mode == TermsFormMode.View && terms.Status != "Draft")
                {
                    var rows = await _apiService.GetTermsAcceptancesAsync(terms.TermsVersionId);
                    _acceptanceGrid.DataSource = rows;
                    _acceptanceTitle.Text = rows.Count == 0
                        ? "Company acceptances: none yet"
                        : $"Company acceptances ({rows.Count})";
                }
                else if (_mode == TermsFormMode.View)
                {
                    _acceptanceTitle.Text = "This draft has not been published, so no company has accepted it.";
                    _acceptanceGrid.Visible = false;
                }

                _errorLabel.Text = "";
            }
            catch (Exception ex)
            {
                _errorLabel.ForeColor = AdminUi.ErrorColor;
                _errorLabel.Text = AdminUi.GetMessage(ex);
                if (_saveButton != null)
                {
                    _saveButton.Enabled = false;
                }
            }
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.ForeColor = AdminUi.ErrorColor;
            _errorLabel.Text = "";

            string title = _titleBox.Text.Trim();
            string content = _contentBox.Text.Trim();

            if (title.Length == 0)
            {
                _errorLabel.Text = "Title is required.";
                _titleBox.Focus();
                return;
            }

            if (content.Length == 0)
            {
                _errorLabel.Text = "Content is required.";
                _contentBox.Focus();
                return;
            }

            _saveButton.Enabled = false;

            try
            {
                if (_mode == TermsFormMode.Create)
                {
                    await _apiService.CreateTermsDraftAsync(title, content);
                }
                else
                {
                    await _apiService.UpdateTermsDraftAsync(_termsVersionId!.Value, title, content);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = AdminUi.GetMessage(ex);
                _saveButton.Enabled = true;
            }
        }
    }
}