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
    internal ListBox Abilities { get; } = new() { Dock = DockStyle.Fill, IntegralHeight = false, FormattingEnabled = true };
    internal TextBox AbilitySearch { get; } = new() { PlaceholderText = "Search abilities", Dock = DockStyle.Fill };
    internal TabControl Workspaces { get; } = new() { Dock = DockStyle.Fill, Padding = new(24, 9) };
    internal TabControl JobPages { get; } = new() { Dock = DockStyle.Fill };
    internal ComboBox RecordSet { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    internal DataGridView References { get; } = new();
    internal Button OpenAbilityButton { get; } = new() { Text = "Open selected ability →", AutoSize = true };
    internal Button PackageButton { get; } = new() { Text = "Package details…", AutoSize = true };
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<Form>? CheckDetailsDialog { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<Form, Button>? CheckCopyDialog { get; set; }
    private readonly Label documentTitle = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label jobTitle = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label abilityTitle = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label jobComparison = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Label abilityComparison = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly CheckBox learnOnHitFlag = new() { Text = "Learn on hit", AutoSize = true, AutoCheck = false, TabStop = false };
    private readonly CheckBox showNameFlag = new() { Text = "Show name", AutoSize = true, AutoCheck = false, TabStop = false };
    private readonly CheckBox canLearnFlag = new() { Text = "Can learn with JP", AutoSize = true, AutoCheck = false, TabStop = false };
    private readonly Label learnRate = new() { Dock = DockStyle.Fill };
    private readonly Button diagnostic = new() { Text = "Details…", AutoSize = true, Enabled = false };
    private string? diagnosticText;
    internal bool EditingDraft => Document?.HasDraft == true && RecordSet.SelectedIndex == 1;
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
        Text = "FFT Content Editor";
        ClientSize = new(1200, 820);
        MinimumSize = new(1000, 720);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        errors.ContainerControl = this;
        BuildLayout();
        SearchBox.TextChanged += (_, _) => RefreshJobs();
        AbilitySearch.TextChanged += (_, _) => RefreshAbilities();
        Abilities.Format += (_, e) => { if (e.ListItem is string id) e.Value = RecordName(id); };
        RecordSet.SelectedIndexChanged += (_, _) =>
        {
            if (binding) return;
            if (RecordSet.SelectedIndex == 1 && Document?.HasDraft != true) { RecordSet.SelectedIndex = 0; return; }
            RefreshJobs(); RefreshAbilities(); UpdateTitle();
        };
        Jobs.SelectedIndexChanged += (_, _) => ShowSourceJob();
        CopyButton.Click += (_, _) => Run(() =>
        {
            if (SelectedJob is not { } id || !ConfirmDiscard()) return;
            using var dialog = new Form { Text = "Create authored package", ClientSize = new(530, 210), Font = Font,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20), RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new(SizeType.Absolute, 54)); layout.RowStyles.Add(new(SizeType.Absolute, 26)); layout.RowStyles.Add(new(SizeType.Absolute, 40)); layout.RowStyles.Add(new(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = $"Copy {RecordName(id.Value)} and its linked skill set and abilities.\r\nThe original package stays read-only." });
            layout.Controls.Add(new Label { AutoSize = true, Text = "New package ID (use a distinct namespace)" });
            layout.Controls.Add(PackageIdBox);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var create = new Button { Text = "Create copy", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            actions.Controls.Add(create); actions.Controls.Add(cancel); layout.Controls.Add(actions); dialog.Controls.Add(layout);
            dialog.AcceptButton = create; dialog.CancelButton = cancel;
            if (CheckCopyDialog is not null) dialog.Shown += (_, _) => dialog.BeginInvoke(() => CheckCopyDialog(dialog, create));
            if (dialog.ShowDialog(this) != DialogResult.OK) { layout.Controls.Remove(PackageIdBox); return; }
            string packageId = PackageIdBox.Text; layout.Controls.Remove(PackageIdBox);
            Document!.CreateCopy(id, packageId);
            binding = true; RecordSet.SelectedIndex = 1; SearchBox.Clear(); AbilitySearch.Clear(); binding = false;
            RefreshJobs();
            BindDraft();
            SetStatus("Authored copy created. Source records remain read-only.");
        });
        MoveBox.TextChanged += (_, _) => EditMovement();
        JumpBox.TextChanged += (_, _) => EditMovement();
        Abilities.SelectedIndexChanged += (_, _) => ShowAbility();
        JpBox.TextChanged += (_, _) =>
        {
            if (binding || !EditingDraft || SelectedAbility is not { } id) return;
            Document!.EditJp(id, JpBox.Text); DraftChanged();
        };
        ValidateButton.Click += (_, _) => ValidateDraft();
        OpenAbilityButton.Click += (_, _) => OpenReferencedAbility();
        References.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenReferencedAbility(); };
        References.SelectionChanged += (_, _) => UpdateEnabled();
        PackageButton.Click += (_, _) => ShowDetails("Package & technical details", provenance.Text +
            $"\r\n\r\nAuthored package: {(Document?.HasDraft == true ? Document.PackageId : "No copy created")}\r\nLast export: {Document?.ExportPath ?? "Not exported"}" +
            "\r\n\r\n" + sourceDetails.Text + "\r\n\r\n" + abilityDetails.Text);
        diagnostic.Click += (_, _) => ShowDetails("Validation details", diagnosticText ?? "");
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
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new(SizeType.Absolute, 55)); root.RowStyles.Add(new(SizeType.Absolute, 48));
        root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 48));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.AutoSize));
        documentTitle.Font = new(Font.FontFamily, 16, FontStyle.Bold);
        header.Controls.Add(documentTitle); header.Controls.Add(PackageButton); root.Controls.Add(header);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var open = new Button { Text = "Open baseline…", AutoSize = true };
        open.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Content metadata (*.json)|*.json", Title = "Open a prepared read-only baseline" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Run(() => { JobEditorDocument candidate = JobEditorDocument.OpenSource(dialog.FileName); if (ConfirmDiscard()) SetDocument(candidate); });
        };
        CopyButton.Text = "Create copy…"; PreviewButton.Text = "Update preview";
        toolbar.Controls.AddRange([open, CopyButton, reopen, ValidateButton, export,
            new Label { Text = "View:", AutoSize = true, Margin = new(14, 7, 3, 0) }, RecordSet]);
        RecordSet.Items.AddRange(["Original (read-only)", "Authored copy"]); RecordSet.SelectedIndex = 0;
        root.Controls.Add(toolbar);
        var jobsPage = new TabPage("Jobs") { Padding = new(12), BackColor = Color.White };
        var abilitiesPage = new TabPage("Abilities") { Padding = new(12), BackColor = Color.White };
        Workspaces.TabPages.AddRange([jobsPage, abilitiesPage]); root.Controls.Add(Workspaces);
        var jobPane = Browser(jobsPage, SearchBox, Jobs);
        var jobRoot = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        jobRoot.RowStyles.Add(new(SizeType.Absolute, 42)); jobRoot.RowStyles.Add(new(SizeType.Absolute, 36)); jobRoot.RowStyles.Add(new(SizeType.Percent, 100));
        jobTitle.Font = new(Font.FontFamily, 18, FontStyle.Bold); jobRoot.Controls.Add(jobTitle); jobRoot.Controls.Add(jobComparison); jobRoot.Controls.Add(JobPages); jobPane.Controls.Add(jobRoot);
        var properties = new TabPage("Properties") { Padding = new(14), BackColor = Color.White };
        var previewPage = new TabPage("Movement preview") { Padding = new(14), BackColor = Color.White };
        JobPages.TabPages.AddRange([properties, previewPage]);
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fields.RowStyles.Add(new(SizeType.Absolute, 120)); fields.RowStyles.Add(new(SizeType.Percent, 100));
        var movement = new GroupBox { Text = "Movement", Dock = DockStyle.Fill, Padding = new(14) };
        var movementFields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        movementFields.ColumnStyles.Add(new(SizeType.Absolute, 100)); movementFields.ColumnStyles.Add(new(SizeType.Absolute, 120));
        movementFields.ColumnStyles.Add(new(SizeType.Absolute, 100)); movementFields.ColumnStyles.Add(new(SizeType.Absolute, 120));
        MoveBox.Margin = JumpBox.Margin = new(3, 3, 24, 3);
        movementFields.Controls.Add(new Label { Text = "Base move", AutoSize = true }, 0, 0); movementFields.Controls.Add(MoveBox, 1, 0);
        movementFields.Controls.Add(new Label { Text = "Base jump", AutoSize = true }, 2, 0); movementFields.Controls.Add(JumpBox, 3, 0);
        var movementHint = new Label { Dock = DockStyle.Fill, Text = "Base values only. Equipment, statuses and growth are outside this slice." };
        movementFields.Controls.Add(movementHint, 0, 1); movementFields.SetColumnSpan(movementHint, 4); movement.Controls.Add(movementFields); fields.Controls.Add(movement);
        var related = new GroupBox { Text = "Skill set and innate abilities", Dock = DockStyle.Fill, Padding = new(12) };
        var relatedLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        relatedLayout.RowStyles.Add(new(SizeType.Percent, 100)); relatedLayout.RowStyles.Add(new(SizeType.Absolute, 40));
        References.Dock = DockStyle.Fill; References.ReadOnly = true; References.AllowUserToAddRows = References.AllowUserToDeleteRows = false;
        References.RowHeadersVisible = false; References.MultiSelect = false; References.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        References.BackgroundColor = Color.White; References.BorderStyle = BorderStyle.FixedSingle; References.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        References.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "Reference", FillWeight = 27, SortMode = DataGridViewColumnSortMode.NotSortable });
        References.Columns.Add(new DataGridViewTextBoxColumn { Name = "Slot", HeaderText = "Slot", FillWeight = 13, SortMode = DataGridViewColumnSortMode.NotSortable });
        References.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ability", HeaderText = "Ability", FillWeight = 60, SortMode = DataGridViewColumnSortMode.NotSortable });
        References.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        relatedLayout.Controls.Add(References); relatedLayout.Controls.Add(OpenAbilityButton); related.Controls.Add(relatedLayout); fields.Controls.Add(related); properties.Controls.Add(fields);
        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        previewLayout.RowStyles.Add(new(SizeType.Absolute, 42)); previewLayout.RowStyles.Add(new(SizeType.Percent, 100)); previewLayout.RowStyles.Add(new(SizeType.Absolute, 42));
        previewLayout.Controls.Add(PreviewButton); previewLayout.Controls.Add(preview);
        previewLayout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Flat 9 × 9 comparison through the existing simulator. Jump has no effect on this map." }); previewPage.Controls.Add(previewLayout);
        var abilityPane = Browser(abilitiesPage, AbilitySearch, Abilities);
        var abilityRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new(8, 0, 8, 0) };
        foreach (int height in new[] { 42, 36, 125, 165 }) abilityRoot.RowStyles.Add(new(SizeType.Absolute, height));
        abilityRoot.RowStyles.Add(new(SizeType.Percent, 100));
        abilityTitle.Font = new(Font.FontFamily, 18, FontStyle.Bold); abilityRoot.Controls.Add(abilityTitle); abilityRoot.Controls.Add(abilityComparison);
        var learning = new GroupBox { Text = "Learning", Dock = DockStyle.Fill, Padding = new(14) };
        var learningFields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        learningFields.ColumnStyles.Add(new(SizeType.Absolute, 100)); learningFields.ColumnStyles.Add(new(SizeType.Absolute, 150));
        learningFields.Controls.Add(new Label { Text = "JP cost", AutoSize = true }, 0, 0); JpBox.Margin = new(3, 3, 24, 3); learningFields.Controls.Add(JpBox, 1, 0);
        var note = new Label { Text = "Learning metadata only. This does not implement an ability effect.", Dock = DockStyle.Fill };
        learningFields.Controls.Add(note, 0, 1); learningFields.SetColumnSpan(note, 2); learning.Controls.Add(learningFields); abilityRoot.Controls.Add(learning);
        var flags = new GroupBox { Text = "Learning options (read-only)", Dock = DockStyle.Fill, Padding = new(14) };
        var flagFields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        flagFields.Controls.Add(learnOnHitFlag); flagFields.Controls.Add(showNameFlag); flagFields.Controls.Add(canLearnFlag); flagFields.Controls.Add(learnRate); flags.Controls.Add(flagFields);
        abilityRoot.Controls.Add(flags); abilityRoot.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Select another ability to inspect or edit it. Job movement remains in the Jobs workspace." }); abilityPane.Controls.Add(abilityRoot);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize)); footer.Controls.Add(Status); footer.Controls.Add(diagnostic); root.Controls.Add(footer);
        Controls.Add(root);
        SearchBox.AccessibleName = "Search jobs"; Jobs.AccessibleName = "Jobs"; AbilitySearch.AccessibleName = "Search abilities"; Abilities.AccessibleName = "Abilities";
        MoveBox.AccessibleName = "Base move"; JumpBox.AccessibleName = "Base jump"; JpBox.AccessibleName = "JP cost";
        foreach (TextBox numeric in new[] { MoveBox, JumpBox, JpBox }) { numeric.Dock = DockStyle.None; numeric.Width = 90; numeric.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
    }
    private static Panel Browser(TabPage page, TextBox search, ListBox list)
    {
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        split.ColumnStyles.Add(new(SizeType.Absolute, 255)); split.ColumnStyles.Add(new(SizeType.Percent, 100));
        var browser = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(0, 0, 16, 0) };
        browser.RowStyles.Add(new(SizeType.Absolute, 24)); browser.RowStyles.Add(new(SizeType.Absolute, 40)); browser.RowStyles.Add(new(SizeType.Percent, 100));
        browser.Controls.Add(new Label { Text = search.AccessibleName ?? search.PlaceholderText, AutoSize = true }); browser.Controls.Add(search); browser.Controls.Add(list);
        var panel = new Panel { Dock = DockStyle.Fill }; split.Controls.Add(browser); split.Controls.Add(panel); page.Controls.Add(split); return panel;
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
        binding = true; RecordSet.SelectedIndex = 0; SearchBox.Clear(); AbilitySearch.Clear(); binding = false;
        RefreshJobs(); BindDraft(); SetStatus("Original records are read-only. Create a copy of the selected job to start editing.");
    }

    private void RefreshJobs()
    {
        JobId? previous = SelectedJob;
        Jobs.BeginUpdate(); Jobs.Items.Clear();
        if (Document is not null)
        {
            IEnumerable<JobDefinition> source = EditingDraft ? Document.SourceJob is { } draftJob ? [draftJob] : [] : Document.Source.Jobs;
            foreach (JobDefinition job in source.Where(job => job.Id.Value.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase) || RecordName(job.Id.Value).Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                Jobs.Items.Add(new JobRow(job));
        }
        int selected = Enumerable.Range(0, Jobs.Items.Count).FirstOrDefault(index => ((JobRow)Jobs.Items[index]).Job.Id == previous, -1);
        Jobs.SelectedIndex = selected >= 0 ? selected : Jobs.Items.Count > 0 ? 0 : -1;
        Jobs.EndUpdate(); ShowSourceJob();
    }

    private void ShowSourceJob()
    {
        References.Rows.Clear();
        if (Document is null || SelectedJob is not { } id)
        {
            sourceDetails.Clear(); jobTitle.Text = "No matching job"; jobComparison.Text = "";
            bool previous = binding; binding = true; MoveBox.Clear(); JumpBox.Clear(); binding = previous;
            UpdateEnabled(); return;
        }
        JobDefinition job = Document.Source.Jobs.Single(value => value.Id == id);
        SkillsetDefinition skillset = Document.Source.Skillsets.Single(value => value.Id == job.PrimarySkillsetId);
        jobTitle.Text = RecordName(id.Value);
        jobComparison.Text = $"{(EditingDraft ? "Authored copy" : "Original • read-only")}  |  Original Move {job.BaseMove} / Jump {job.BaseJump}  |  {RecordName(skillset.Id.Value)}";
        bool wasBinding = binding; binding = true;
        MoveBox.Text = EditingDraft ? Document.MoveText : job.BaseMove.ToString(System.Globalization.CultureInfo.InvariantCulture);
        JumpBox.Text = EditingDraft ? Document.JumpText : job.BaseJump.ToString(System.Globalization.CultureInfo.InvariantCulture);
        binding = wasBinding;
        string CurrentId(string value) => EditingDraft ? Document.PackageId.Split(':')[0] + value[value.IndexOf(':')..] : value;
        foreach (SkillsetSlot slot in skillset.Slots)
            References.Rows.Add("Skill set", slot.Slot, slot.AbilityId is { } reference ? RecordName(reference.Value) : "(empty)", slot.AbilityId is { } ability ? CurrentId(ability.Value) : "");
        foreach (AbilityId ability in job.InnateAbilityIds) References.Rows.Add("Innate", "—", RecordName(ability.Value), CurrentId(ability.Value));
        sourceDetails.Text = $"SOURCE • {job.Id}  |  Move {job.BaseMove} / Jump {job.BaseJump} / Stepping stone {job.SteppingStone}\r\nSkillset {skillset.Id}\r\n" +
            string.Join("; ", skillset.Slots.Select(slot => $"{slot.Slot}: {slot.AbilityId?.Value ?? "empty"}")) +
            $"\r\nInnates: {string.Join(", ", job.InnateAbilityIds)}";
        UpdateEnabled();
    }

    private void RefreshAbilities()
    {
        string? previous = SelectedAbility?.Split(':', 2)[1];
        bool wasBinding = binding; binding = true;
        Abilities.BeginUpdate(); Abilities.Items.Clear();
        if (Document is not null)
            foreach (EditorAbility ability in Document.BrowseAbilities("", EditingDraft).Where(ability => ability.Id.Contains(AbilitySearch.Text.Trim(), StringComparison.OrdinalIgnoreCase) || RecordName(ability.Id).Contains(AbilitySearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                Abilities.Items.Add(ability.Id);
        int selected = Enumerable.Range(0, Abilities.Items.Count).FirstOrDefault(index => ((string)Abilities.Items[index]).Split(':', 2)[1] == previous, -1);
        Abilities.SelectedIndex = selected >= 0 ? selected : Abilities.Items.Count > 0 ? 0 : -1;
        Abilities.EndUpdate(); binding = wasBinding; ShowAbility();
    }

    internal void OpenReferencedAbility()
    {
        if (References.CurrentRow?.Cells[3].Value is not string id || id.Length == 0) return;
        AbilitySearch.Clear(); RefreshAbilities(); Abilities.SelectedItem = id;
        Workspaces.SelectedIndex = 1; Abilities.Focus();
    }

    private void BindDraft()
    {
        ShowSourceJob(); RefreshAbilities();
        errors.Clear(); preview.Value = null; preview.Invalidate(); UpdateEnabled(); UpdateTitle();
    }

    private void ShowAbility()
    {
        bool previous = binding; binding = true;
        try
        {
            JpBox.Text = Document?.BrowseAbilities("", EditingDraft).FirstOrDefault(value => value.Id == SelectedAbility)?.JpCost ?? "";
            if (Document is not null && SelectedAbility is { } id)
            {
                // Use immutable source metadata even when the current draft has invalid numeric text.
                string sourceId = EditingDraft ? Document.Source.PackageId.Split(':')[0] + id[id.IndexOf(':')..] : id;
                AbilityDefinition ability = Document.Source.Abilities.Single(value => value.Id.Value == sourceId);
                abilityTitle.Text = RecordName(id);
                abilityComparison.Text = $"{(EditingDraft ? "Authored copy" : "Original • read-only")}  |  Original: {ability.JpCost} JP";
                abilityDetails.Text = $"Read-only learning flags\r\nLearn on hit: {ability.LearnOnHit}   •   Show name: {ability.ShowName}   •   Can learn with JP: {ability.CanLearnWithJp}\r\nLearn-on-hit rate (raw): {ability.LearnOnHitRateRaw}";
                abilityDetails.Tag = $"{id}\r\nType 0x{ability.TypeFlagsRaw:X2}; unknown 0x{ability.UnknownTypeFlags:X2}; AI {string.Join(" ", ability.AiFlagsRaw.Select(value => value.ToString("X2")))}";
                learnOnHitFlag.Checked = ability.LearnOnHit; showNameFlag.Checked = ability.ShowName; canLearnFlag.Checked = ability.CanLearnWithJp;
                learnRate.Text = $"Learn-on-hit rate (raw): {ability.LearnOnHitRateRaw}";
            }
            else { abilityDetails.Clear(); abilityDetails.Tag = null; abilityTitle.Text = "No matching ability"; abilityComparison.Text = "";
                learnOnHitFlag.Checked = showNameFlag.Checked = canLearnFlag.Checked = false; learnRate.Text = ""; }
        }
        finally { binding = previous; }
        UpdateEnabled();
    }

    private void EditMovement()
    {
        if (binding || !EditingDraft || SelectedJob is null) return;
        Document!.EditMovement(MoveBox.Text, JumpBox.Text); DraftChanged();
    }
    private void DraftChanged()
    {
        errors.Clear(); diagnostic.Enabled = false; diagnosticText = null; preview.Value = null; preview.Invalidate(); UpdateTitle();
        SetStatus("Draft changed. Validate before export; movement preview needs updating.");
    }
    private void UpdateTitle()
    {
        draftTitle.Text = Document?.HasDraft == true ? $"AUTHORED • {Document.JobId} {(Document.IsDirty ? "• Unsaved" : "• Exported")}" : "Create an authored copy to edit.";
        Text = $"{(Document?.IsDirty == true ? "* " : "")}FFT Content Editor";
        documentTitle.Text = Document?.HasDraft == true ? $"{RecordName(Document.SourceJob!.Id.Value)} · authored package{(Document.IsDirty ? "  •  Unsaved" : "")}" : "FFT Content Editor";
    }
    private void UpdateEnabled()
    {
        bool source = Document is not null && SelectedJob.HasValue, draft = Document?.HasDraft == true;
        CopyButton.Enabled = source; reopen.Enabled = source;
        MoveBox.Enabled = JumpBox.Enabled = source; MoveBox.ReadOnly = JumpBox.ReadOnly = !EditingDraft;
        ValidateButton.Enabled = export.Enabled = draft; PreviewButton.Enabled = EditingDraft && source;
        Abilities.Enabled = Abilities.Items.Count > 0;
        JpBox.Enabled = Abilities.Items.Count > 0; JpBox.ReadOnly = !EditingDraft;
        OpenAbilityButton.Enabled = References.CurrentRow?.Cells[3].Value is string value && value.Length > 0;
        PackageButton.Enabled = Document is not null;
    }
    internal void ValidateDraft() => Run(() => { Document!.Validate(); SetStatus("Valid: all fields and references resolve against the selected baseline."); });
    internal void ExportTo(string path) => Run(() =>
    {
        Document!.Export(path); UpdateTitle(); SetStatus($"Exported {Path.GetFileName(path)}. Source records are unchanged.");
    });
    internal void OpenCopy(string path) => Run(() =>
    {
        if (SelectedJob is not { } id || !ConfirmDiscard()) return;
        Document!.OpenCopy(id, path);
        binding = true; RecordSet.SelectedIndex = 1; SearchBox.Clear(); AbilitySearch.Clear(); binding = false;
        RefreshJobs(); BindDraft(); SetStatus("Authored add-on reopened and composed against its baseline.");
    });
    private void Run(Action action)
    {
        errors.Clear();
        diagnostic.Enabled = false; diagnosticText = null;
        try { action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or NotSupportedException or InvalidDataException or KeyNotFoundException or ContentPackageValidationException)
        {
            if (exception is ContentPackageValidationException validation)
            {
                if (Document?.HasDraft == true && validation.FieldPath.EndsWith(".jpCost", StringComparison.Ordinal))
                {
                    RecordSet.SelectedIndex = 1; AbilitySearch.Clear(); RefreshAbilities(); Workspaces.SelectedIndex = 1;
                    Abilities.SelectedItem = validation.RecordId;
                }
                else if (Document?.HasDraft == true && (validation.FieldPath.EndsWith(".baseMove", StringComparison.Ordinal) || validation.FieldPath.EndsWith(".baseJump", StringComparison.Ordinal)))
                {
                    RecordSet.SelectedIndex = 1; SearchBox.Clear(); RefreshJobs(); Workspaces.SelectedIndex = 0; JobPages.SelectedIndex = 0;
                }
                Control target = validation.FieldPath.EndsWith(".baseMove", StringComparison.Ordinal) ? MoveBox :
                    validation.FieldPath.EndsWith(".baseJump", StringComparison.Ordinal) ? JumpBox :
                    validation.FieldPath.EndsWith(".jpCost", StringComparison.Ordinal) ? JpBox : PackageIdBox;
                errors.SetError(target, validation.Message);
                SetStatus(validation.Message.Split(" (package:", StringSplitOptions.None)[0], failure: true);
            }
            else SetStatus(exception.Message, failure: true);
            diagnosticText = exception.Message; diagnostic.Enabled = true;
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
    private void ShowDetails(string title, string text)
    {
        using var dialog = new Form { Text = title, ClientSize = new(760, 440), MinimumSize = new(600, 340), Font = Font,
            StartPosition = FormStartPosition.CenterParent, MinimizeBox = false };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 42));
        layout.Controls.Add(new TextBox { Text = text + "\r\n" + abilityDetails.Tag, Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false });
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right };
        layout.Controls.Add(close); dialog.Controls.Add(layout); dialog.AcceptButton = close; dialog.CancelButton = close;
        if (CheckDetailsDialog is not null) dialog.Shown += (_, _) => dialog.BeginInvoke(() => CheckDetailsDialog(dialog));
        dialog.ShowDialog(this);
    }
    internal void PermitCheckedClose() { discardApproved = true; Close(); }
    protected override void Dispose(bool disposing) { if (disposing) errors.Dispose(); base.Dispose(disposing); }
    private sealed record JobRow(JobDefinition Job) { public override string ToString() => RecordName(Job.Id.Value); }
    internal static string RecordName(string id)
    {
        string part = id[(id.LastIndexOf('/') + 1)..];
        if (part.StartsWith("source-", StringComparison.Ordinal)) return "Ability " + part[7..];
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.Replace('-', ' '));
    }
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
