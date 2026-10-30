using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Zodiac.Content.Authoring;

namespace Fft.Editor;

/// <summary>Finite opt-in developer check. File writes are confined to a newly created output directory.</summary>
internal static class EditorNativeCheck
{
    internal static void Attach(JobEditorForm form, string directory)
    {
        form.Shown += (_, _) => form.BeginInvoke(() => Execute(form, directory));
    }

    private static void Execute(JobEditorForm form, string directory)
    {
        var watch = Stopwatch.StartNew();
        var trace = new List<object>();
        try
        {
            Directory.CreateDirectory(directory);
            var document = form.Document!;
            string hash = Hash(document.SourcePath!);
            Capture(form, "source");
            Require(form.MoveBox.ReadOnly, "source job read-only");
            form.Workspaces.SelectedIndex = 1;
            Require(form.JpBox.ReadOnly && form.Abilities.Items.Count == document.Source.Abilities.Length, "source ability catalog read-only");
            Capture(form, "source-abilities"); form.Workspaces.SelectedIndex = 0;
            form.SearchBox.Text = "missing-job";
            Require(form.Jobs.Items.Count == 0, "empty search");
            form.SearchBox.Clear();
            Require(form.Jobs.Items.Count > 0, "search restored");
            form.PackageIdBox.Text = "editor.demo:package/job-copy";
            form.CheckCopyDialog = (dialog, create) => { Capture(dialog, "create-copy"); create.PerformClick(); };
            form.CopyButton.PerformClick();
            Require(document.HasDraft && document.IsDirty, "copy");
            Capture(form, "copy");
            form.MoveBox.Text = "invalid";
            form.Workspaces.SelectedIndex = 1;
            form.ValidateButton.PerformClick();
            Require(form.HasFieldError(form.MoveBox) && form.Workspaces.SelectedIndex == 0, "field-specific Move error");
            Capture(form, "invalid");
            form.MoveBox.Text = "2"; form.JumpBox.Text = "4";
            form.Workspaces.SelectedIndex = 1;
            Require(!form.MoveBox.Visible && form.JpBox.Visible, "separate workspaces");
            form.SearchBox.Text = "missing-job";
            form.JpBox.Text = "invalid"; form.ValidateButton.PerformClick();
            Require(form.HasFieldError(form.JpBox) && form.Workspaces.SelectedIndex == 1 && form.SearchBox.Text == "missing-job", "ability error routes independently");
            Capture(form, "ability-error");
            if (form.Abilities.Items.Count > 0) form.JpBox.Text = "123";
            form.AbilitySearch.Text = "missing-ability";
            Require(form.Abilities.Items.Count == 0 && document.MoveText == "2", "independent filters preserve job edits");
            form.AbilitySearch.Clear();
            Require(form.JpBox.Text == "123", "ability filter preserves JP edit");
            Capture(form, "abilities");
            form.RecordSet.SelectedIndex = 0;
            Require(form.JpBox.ReadOnly && form.JpBox.Text != "123", "original view remains unchanged");
            form.RecordSet.SelectedIndex = 1;
            Require(!form.JpBox.ReadOnly && form.JpBox.Text == "123", "view switch retains draft");
            form.SearchBox.Clear(); form.Workspaces.SelectedIndex = 0; form.OpenAbilityButton.PerformClick();
            Require(form.Workspaces.SelectedIndex == 1 && ((string)form.Abilities.SelectedItem!).StartsWith("editor.demo:", StringComparison.Ordinal), "job reference navigates to ability");
            Capture(form, "reference"); form.Workspaces.SelectedIndex = 0; form.JobPages.SelectedIndex = 1;
            form.ValidateButton.PerformClick(); form.PreviewButton.PerformClick();
            Require(document.Validate().Jobs[0].BaseMove == 2, "valid Move");
            Require(document.Preview().AuthoredReachable.Length == 12, "exact authored range");
            Capture(form, "preview");
            form.CheckDetailsDialog = dialog => { Capture(dialog, "package-details"); dialog.Close(); };
            form.PackageButton.PerformClick();
            string output = Path.Combine(directory, "authored-job.json");
            form.ExportTo(output);
            Require(!document.IsDirty && File.Exists(output), "export");
            string expected = File.ReadAllText(output);
            form.OpenCopy(output);
            Require(!document.IsDirty && document.Validate().ToJson() == expected, "reopen");
            form.JobPages.SelectedIndex = 0; Capture(form, "reopened");
            form.MoveBox.Text = "1";
            using (var busy = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.None)) form.ExportTo(output);
            Require(document.IsDirty && File.ReadAllText(output) == expected, "failed export preserves output and draft");
            Capture(form, "export-error");
            form.CheckDialog = dialog => { Capture(dialog, "unsaved-cancel"); dialog.CancelAction.PerformClick(); };
            form.Close();
            Require(!form.IsDisposed && document.IsDirty, "close canceled");
            form.ClientSize = new(1000, 720); form.PerformLayout();
            Capture(form, "resized");
            form.Workspaces.SelectedIndex = 1; Capture(form, "resized-abilities");
            Require(Hash(document.SourcePath!) == hash && hash == document.SourceSha256, "source preserved");
            form.CheckDialog = dialog => { Capture(dialog, "unsaved-discard"); dialog.DiscardAction.PerformClick(); };
            Require(watch.Elapsed < TimeSpan.FromSeconds(30), "finite time budget");
            form.Close();
            Require(form.IsDisposed || !form.Visible, "discard closes editor");
            File.WriteAllText(Path.Combine(directory, "trace.json"), JsonSerializer.Serialize(new
            {
                format = "fft.job-editor-native-check", schemaVersion = 2,
                sourceSha256 = hash, sourcePackage = document.Source.PackageId,
                steps = trace, sourceMove = document.SourceJob!.BaseMove,
                authoredMove = document.Validate().Jobs[0].BaseMove,
                savedMove = 2, savedJump = 4, savedJp = document.Abilities.Length > 0 ? 123 : (int?)null,
                sourceUnchanged = true, exportedRoundTrip = true, failedExportPreserved = true,
                closeCancelPreserved = true, closeDiscardAccepted = true,
                separateWorkspaces = true, sourceFieldsReadOnly = true, independentFiltersPreservedEdits = true,
                jobReferenceNavigatesToAbility = true, validationSelectsWorkspace = true, viewSwitchPreservesDraft = true,
                captureKind = "native DrawToBitmap; not desktop scanout or physical input"
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        catch (Exception exception)
        {
            form.CheckFailed = true;
            File.WriteAllText(Path.Combine(directory, "failure.txt"), exception.ToString());
            form.PermitCheckedClose();
        }
        void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            trace.Add(new { action = name, passed = true });
        }
        void Capture(Form target, string name)
        {
            target.Refresh(); target.PerformLayout();
            using var bitmap = new Bitmap(target.Width, target.Height);
            target.DrawToBitmap(bitmap, new(Point.Empty, target.Size));
            bitmap.Save(Path.Combine(directory, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    private static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(input)); }
}
