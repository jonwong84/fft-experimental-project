using Zodiac.Content.Authoring;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;

namespace Fft.Editor;

internal sealed partial class JobEditorForm : Form
{
    internal JobEditorDocument? Document { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool CheckFailed { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<DiscardDialog>? CheckDialog { get; set; }
    internal TextBox SearchBox { get; } = new() { PlaceholderText = "Search job IDs", Dock = DockStyle.Fill };
    internal ListBox Jobs { get; } = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    internal TextBox PackageIdBox { get; } = new() { Text = "authored:package/my-job", Dock = DockStyle.Fill };
    internal TextBox MoveBox { get; } = new() { Dock = DockStyle.Fill };
    internal TextBox JumpBox { get; } = new() { Dock = DockStyle.Fill };
    internal TextBox JpBox { get; } = new() { Dock = DockStyle.Fill };
    internal ComboBox Abilities { get; } = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    internal Button CopyButton { get; } = new() { Text = "&Create authored copy", AutoSize = true };
    internal Button ValidateButton { get; } = new() { Text = "&Validate", AutoSize = true };
    internal Button PreviewButton { get; } = new() { Text = "&Preview movement", AutoSize = true };
    private readonly Button reopen = new() { Text = "&Reopen add-on…", AutoSize = true };
    private readonly Button export = new() { Text = "&Export add-on…", AutoSize = true };
    private readonly TextBox provenance = ReadOnlyBox();
    private readonly TextBox sourceDetails = ReadOnlyBox();
    private readonly TextBox abilityDetails = ReadOnlyBox();
    private readonly Label draftTitle = new() { AutoSize = true, Text = "Create an authored copy to edit." };
    internal Label Status { get; } = new() { AutoSize = false, Dock = DockStyle.Fill, Text = "Open a prepared baseline package to begin." };
    private readonly ErrorProvider errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly MovementPreviewPanel preview = new() { Dock = DockStyle.Fill };
    private bool binding;
    private bool discardApproved;
    private JobId? SelectedJob => Jobs.SelectedItem is JobRow row ? row.Job.Id : null;
    private string? SelectedAbility => Abilities.SelectedItem as string;

    internal JobEditorForm(JobEditorDocument? document)
    {
        Text = "FFT Content • Job & ability editor";
        ClientSize = new(1160, 940);
        MinimumSize = new(1000, 880);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        errors.ContainerControl = this;
        BuildLayout();
        SearchBox.TextChanged += (_, _) => RefreshJobs();
        Jobs.SelectedIndexChanged += (_, _) => ShowSourceJob();
        CopyButton.Click += (_, _) => Run(() =>
        {
            if (SelectedJob is not { } id || !ConfirmDiscard()) return;
            Document!.CreateCopy(id, PackageIdBox.Text);
            BindDraft();
            SetStatus("Authored copy created. Source records remain read-only.");
        });
        MoveBox.TextChanged += (_, _) => EditMovement();
        JumpBox.TextChanged += (_, _) => EditMovement();
        Abilities.SelectedIndexChanged += (_, _) => ShowAbility();
        JpBox.TextChanged += (_, _) =>
        {
            if (binding || Document?.HasDraft != true || SelectedAbility is not { } id) return;
            Document.EditJp(id, JpBox.Text); DraftChanged();
        };
        ValidateButton.Click += (_, _) => ValidateDraft();
        PreviewButton.Click += (_, _) => Run(() =>
        {
            preview.Value = Document!.Preview(); preview.Invalidate();
            SetStatus("Movement preview updated through the existing battle simulator. JP is metadata only.");
        });
        export.Click += (_, _) =>
        {
            if (Document?.HasDraft != true) return;
            using var dialog = new SaveFileDialog { Filter = "Content metadata (*.json)|*.json", FileName = "authored-job.json",
                Title = "Export into a separate authored directory", OverwritePrompt = true, AddExtension = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) ExportTo(dialog.FileName);
        };
        reopen.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Content metadata (*.json)|*.json", Title = "Reopen a supported authored add-on" };
            if (dialog.ShowDialog(this) == DialogResult.OK) OpenCopy(dialog.FileName);
        };
        FormClosing += (_, e) => { if (!discardApproved && !ConfirmDiscard()) e.Cancel = true; };
        if (document is not null) SetDocument(document);
        else UpdateEnabled();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new(SizeType.Absolute, 42)); root.RowStyles.Add(new(SizeType.Absolute, 92));
        root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 70));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new(SizeType.Percent, 100)); heading.ColumnStyles.Add(new(SizeType.AutoSize));
        heading.Controls.Add(new Label { Text = "Job & ability editor", AutoSize = true, Font = new(Font, FontStyle.Bold), Margin = new(0, 5, 0, 0) });
        var open = new Button { Text = "&Open baseline…", AutoSize = true };
        open.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Content metadata (*.json)|*.json", Title = "Open a prepared read-only baseline" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Run(() =>
            {
                JobEditorDocument candidate = JobEditorDocument.OpenSource(dialog.FileName);
                if (ConfirmDiscard()) SetDocument(candidate);
            });
        };
        heading.Controls.Add(open); root.Controls.Add(heading, 0, 0); root.Controls.Add(provenance, 0, 1);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new(SizeType.Absolute, 280)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(0, 8, 14, 0) };
        sidebar.RowStyles.Add(new(SizeType.Absolute, 28)); sidebar.RowStyles.Add(new(SizeType.Absolute, 38)); sidebar.RowStyles.Add(new(SizeType.Percent, 100));
        sidebar.Controls.Add(new Label { Text = "READ-ONLY SOURCE JOBS", AutoSize = true }); sidebar.Controls.Add(SearchBox); sidebar.Controls.Add(Jobs);
        body.Controls.Add(sidebar, 0, 0);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new(4, 8, 8, 0) };
        foreach (int height in new[] { 100, 38, 38, 34, 38, 85, 38 }) right.RowStyles.Add(new(SizeType.Absolute, height));
        right.RowStyles.Add(new(SizeType.Percent, 100));
        right.Controls.Add(sourceDetails);
        var identity = Row("Add-on package", PackageIdBox); right.Controls.Add(identity);
        var draftActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        draftActions.Controls.Add(CopyButton); draftActions.Controls.Add(reopen); right.Controls.Add(draftActions);
        right.Controls.Add(draftTitle);
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        fields.ColumnStyles.Add(new(SizeType.Absolute, 60)); fields.ColumnStyles.Add(new(SizeType.Absolute, 80));
        fields.ColumnStyles.Add(new(SizeType.Absolute, 60)); fields.ColumnStyles.Add(new(SizeType.Absolute, 80));
        fields.ColumnStyles.Add(new(SizeType.Absolute, 36)); fields.ColumnStyles.Add(new(SizeType.Percent, 100));
        MoveBox.Margin = JumpBox.Margin = JpBox.Margin = new(3, 3, 22, 3);
        fields.Controls.Add(new Label { Text = "Move", AutoSize = true }); fields.Controls.Add(MoveBox);
        fields.Controls.Add(new Label { Text = "Jump", AutoSize = true }); fields.Controls.Add(JumpBox);
        fields.Controls.Add(new Label { Text = "JP", AutoSize = true }); fields.Controls.Add(JpBox);
        right.Controls.Add(fields);
        var linked = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        linked.RowStyles.Add(new(SizeType.Absolute, 36)); linked.RowStyles.Add(new(SizeType.Percent, 100));
        linked.Controls.Add(Abilities); linked.Controls.Add(abilityDetails); right.Controls.Add(linked);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        actions.Controls.Add(ValidateButton); actions.Controls.Add(PreviewButton); actions.Controls.Add(export); right.Controls.Add(actions);
        right.Controls.Add(preview); body.Controls.Add(right, 1, 0); root.Controls.Add(body, 0, 2);
        root.Controls.Add(Status, 0, 3); Controls.Add(root);
        provenance.AccessibleName = "Read-only source provenance"; sourceDetails.AccessibleName = "Source job and skillset";
        SearchBox.AccessibleName = "Search source jobs"; Jobs.AccessibleName = "Source jobs";
        PackageIdBox.AccessibleName = "Authored package ID"; MoveBox.AccessibleName = "Authored base Move";
        JumpBox.AccessibleName = "Authored base Jump"; JpBox.AccessibleName = "Authored ability JP cost";
        Abilities.AccessibleName = "Copied linked abilities";
    }

    private static TextBox ReadOnlyBox() => new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
    private static TableLayoutPanel Row(string title, Control control)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new(SizeType.Absolute, 130)); row.ColumnStyles.Add(new(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = title, AutoSize = true }); row.Controls.Add(control); return row;
    }

    private void SetDocument(JobEditorDocument document)
    {
        Document = document;
        provenance.Text = $"READ-ONLY • {document.SourcePath ?? "in-memory baseline"}\r\n{document.Source.PackageId}  |  {document.Source.ProfileId}\r\nSHA-256 {document.SourceSha256}";
        SearchBox.Clear(); RefreshJobs(); BindDraft(); SetStatus("Select a source job, then create an authored copy or reopen an add-on.");
    }

    private void RefreshJobs()
    {
        JobId? previous = SelectedJob;
        Jobs.BeginUpdate(); Jobs.Items.Clear();
        if (Document is not null) foreach (JobDefinition job in Document.Search(SearchBox.Text)) Jobs.Items.Add(new JobRow(job));
        int selected = Enumerable.Range(0, Jobs.Items.Count).FirstOrDefault(index => ((JobRow)Jobs.Items[index]).Job.Id == previous, -1);
        Jobs.SelectedIndex = selected >= 0 ? selected : Jobs.Items.Count > 0 ? 0 : -1;
        Jobs.EndUpdate(); ShowSourceJob();
    }

    private void ShowSourceJob()
    {
        if (Document is null || SelectedJob is not { } id) { sourceDetails.Clear(); UpdateEnabled(); return; }
        JobDefinition job = Document.Source.Jobs.Single(value => value.Id == id);
        SkillsetDefinition skillset = Document.Source.Skillsets.Single(value => value.Id == job.PrimarySkillsetId);
        sourceDetails.Text = $"SOURCE • {job.Id}  |  Move {job.BaseMove} / Jump {job.BaseJump} / Stepping stone {job.SteppingStone}\r\nSkillset {skillset.Id}\r\n" +
            string.Join("; ", skillset.Slots.Select(slot => $"{slot.Slot}: {slot.AbilityId?.Value ?? "empty"}")) +
            $"\r\nInnates: {string.Join(", ", job.InnateAbilityIds)}";
        if (!Document.HasDraft)
        {
            Abilities.Items.Clear();
            foreach (AbilityId ability in skillset.Slots.Where(slot => slot.AbilityId.HasValue).Select(slot => slot.AbilityId!.Value)
                .Concat(job.InnateAbilityIds).Distinct()) Abilities.Items.Add(ability.Value);
            if (Abilities.Items.Count > 0) Abilities.SelectedIndex = 0;
            ShowAbility();
        }
        UpdateEnabled();
    }

    private void BindDraft()
    {
        binding = true;
        try
        {
            MoveBox.Text = Document?.MoveText ?? ""; JumpBox.Text = Document?.JumpText ?? "";
            Abilities.Items.Clear();
            if (Document?.HasDraft == true)
            {
                PackageIdBox.Text = Document.PackageId;
                foreach (EditorAbility ability in Document.Abilities) Abilities.Items.Add(ability.Id);
            }
            if (Abilities.Items.Count > 0) Abilities.SelectedIndex = 0;
            ShowAbility();
        }
        finally { binding = false; }
        errors.Clear(); preview.Value = null; preview.Invalidate(); UpdateEnabled(); UpdateTitle();
        if (Document?.HasDraft != true) ShowSourceJob();
    }

    private void ShowAbility()
    {
        bool previous = binding; binding = true;
        try
        {
            JpBox.Text = Document?.Abilities.FirstOrDefault(value => value.Id == SelectedAbility)?.JpCost ?? "";
            if (Document is not null && SelectedAbility is { } id)
            {
                // Use immutable source metadata even when the current draft has invalid numeric text.
                string sourceId = Document.HasDraft ? Document.Source.PackageId.Split(':')[0] + id[id.IndexOf(':')..] : id;
                AbilityDefinition ability = Document.Source.Abilities.Single(value => value.Id.Value == sourceId);
                if (!Document.HasDraft) JpBox.Text = ability.JpCost.ToString(System.Globalization.CultureInfo.InvariantCulture);
                abilityDetails.Text = $"Learning metadata only • source JP {ability.JpCost}\r\nType 0x{ability.TypeFlagsRaw:X2}; learn rate {ability.LearnOnHitRateRaw}; AI {string.Join(" ", ability.AiFlagsRaw.Select(value => value.ToString("X2")))}";
            }
            else abilityDetails.Clear();
        }
        finally { binding = previous; }
        UpdateEnabled();
    }

    private void EditMovement()
    {
        if (binding || Document?.HasDraft != true) return;
        Document.EditMovement(MoveBox.Text, JumpBox.Text); DraftChanged();
    }
    private void DraftChanged()
    {
        errors.Clear(); preview.Value = null; preview.Invalidate(); UpdateTitle();
        SetStatus("Draft changed. Validate before export; movement preview needs updating.");
    }
    private void UpdateTitle()
    {
        draftTitle.Text = Document?.HasDraft == true ? $"AUTHORED • {Document.JobId} {(Document.IsDirty ? "• Unsaved" : "• Exported")}" : "Create an authored copy to edit.";
        Text = $"{(Document?.IsDirty == true ? "* " : "")}FFT Content • Job & ability editor";
    }
    private void UpdateEnabled()
    {
        bool source = Document is not null && SelectedJob.HasValue, draft = Document?.HasDraft == true;
        CopyButton.Enabled = source; reopen.Enabled = source;
        MoveBox.Enabled = JumpBox.Enabled = ValidateButton.Enabled = PreviewButton.Enabled = export.Enabled = draft;
        Abilities.Enabled = Abilities.Items.Count > 0;
        JpBox.Enabled = draft && Abilities.Items.Count > 0;
    }
    internal void ValidateDraft() => Run(() => { Document!.Validate(); SetStatus("Valid: all fields and references resolve against the selected baseline."); });
    internal void ExportTo(string path) => Run(() =>
    {
        Document!.Export(path); UpdateTitle(); SetStatus($"Exported • {Document.ExportPath}");
    });
    internal void OpenCopy(string path) => Run(() =>
    {
        if (SelectedJob is not { } id || !ConfirmDiscard()) return;
        Document!.OpenCopy(id, path); BindDraft(); SetStatus("Authored add-on reopened and composed against its baseline.");
    });
    private void Run(Action action)
    {
        errors.Clear();
        try { action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or NotSupportedException or InvalidDataException or KeyNotFoundException or ContentPackageValidationException)
        {
            if (exception is ContentPackageValidationException validation)
            {
                if (validation.FieldPath.EndsWith(".jpCost", StringComparison.Ordinal) && Abilities.Items.Contains(validation.RecordId))
                    Abilities.SelectedItem = validation.RecordId;
                Control target = validation.FieldPath.EndsWith(".baseMove", StringComparison.Ordinal) ? MoveBox :
                    validation.FieldPath.EndsWith(".baseJump", StringComparison.Ordinal) ? JumpBox :
                    validation.FieldPath.EndsWith(".jpCost", StringComparison.Ordinal) ? JpBox : PackageIdBox;
                errors.SetError(target, validation.Message);
                SetStatus($"{validation.FieldPath}: {validation.Message}", failure: true);
            }
            else SetStatus(exception.Message, failure: true);
        }
    }
    private void SetStatus(string message, bool failure = false)
    {
        Status.Text = message; Status.ForeColor = failure ? Color.Firebrick : Color.FromArgb(30, 60, 80);
    }
    private bool ConfirmDiscard()
    {
        if (Document?.IsDirty != true) return true;
        using var dialog = new DiscardDialog();
        if (CheckDialog is not null) dialog.Shown += (_, _) => dialog.BeginInvoke(() => CheckDialog(dialog));
        return dialog.ShowDialog(this) == DialogResult.Yes;
    }
    internal bool HasFieldError(Control control) => errors.GetError(control).Length > 0;
    internal void PermitCheckedClose() { discardApproved = true; Close(); }
    protected override void Dispose(bool disposing) { if (disposing) errors.Dispose(); base.Dispose(disposing); }
    private sealed record JobRow(JobDefinition Job) { public override string ToString() => Job.Id.Value; }
}

internal sealed class DiscardDialog : Form
{
    internal Button CancelAction { get; } = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
    internal Button DiscardAction { get; } = new() { Text = "Discard changes", DialogResult = DialogResult.Yes, AutoSize = true };
    internal DiscardDialog()
    {
        Text = "Unsaved authored changes"; ClientSize = new(470, 155); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        Font = new("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20), RowCount = 2 };
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 38));
        layout.Controls.Add(new Label { Text = "This draft has changes that have not been exported.\r\nCancel to keep editing, or discard them explicitly.", AutoSize = true });
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.Add(DiscardAction); actions.Controls.Add(CancelAction); layout.Controls.Add(actions); Controls.Add(layout);
        AcceptButton = CancelAction; CancelButton = CancelAction; ActiveControl = CancelAction;
    }
}
