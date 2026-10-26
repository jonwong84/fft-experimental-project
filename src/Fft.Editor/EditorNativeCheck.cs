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
            form.SearchBox.Text = "missing-job";
            Require(form.Jobs.Items.Count == 0, "empty search");
            form.SearchBox.Clear();
            Require(form.Jobs.Items.Count > 0, "search restored");
            form.PackageIdBox.Text = "editor.demo:package/job-copy";
            form.CopyButton.PerformClick();
            Require(document.HasDraft && document.IsDirty, "copy");
            Capture(form, "copy");
            form.MoveBox.Text = "invalid";
            form.ValidateButton.PerformClick();
            Require(form.HasFieldError(form.MoveBox), "field-specific Move error");
            Capture(form, "invalid");
            form.MoveBox.Text = "2"; form.JumpBox.Text = "4";
            if (form.Abilities.Items.Count > 0) form.JpBox.Text = "123";
            form.ValidateButton.PerformClick(); form.PreviewButton.PerformClick();
            Require(document.Validate().Jobs[0].BaseMove == 2, "valid Move");
            Require(document.Preview().AuthoredReachable.Length == 12, "exact authored range");
            Capture(form, "preview");
            string output = Path.Combine(directory, "authored-job.json");
            form.ExportTo(output);
            Require(!document.IsDirty && File.Exists(output), "export");
            string expected = File.ReadAllText(output);
            form.OpenCopy(output);
            Require(!document.IsDirty && document.Validate().ToJson() == expected, "reopen");
            Capture(form, "reopened");
            form.MoveBox.Text = "1";
            using (var busy = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.None)) form.ExportTo(output);
            Require(document.IsDirty && File.ReadAllText(output) == expected, "failed export preserves output and draft");
            Capture(form, "export-error");
            form.CheckDialog = dialog => { Capture(dialog, "unsaved-cancel"); dialog.CancelAction.PerformClick(); };
            form.Close();
            Require(!form.IsDisposed && document.IsDirty, "close canceled");
            form.ClientSize = new(1000, 880); form.PerformLayout();
            Capture(form, "resized");
            Require(Hash(document.SourcePath!) == hash && hash == document.SourceSha256, "source preserved");
            form.CheckDialog = dialog => { Capture(dialog, "unsaved-discard"); dialog.DiscardAction.PerformClick(); };
            Require(watch.Elapsed < TimeSpan.FromSeconds(30), "finite time budget");
            form.Close();
            Require(form.IsDisposed || !form.Visible, "discard closes editor");
            File.WriteAllText(Path.Combine(directory, "trace.json"), JsonSerializer.Serialize(new
            {
                format = "fft.job-editor-native-check", schemaVersion = 1,
                sourceSha256 = hash, sourcePackage = document.Source.PackageId,
                steps = trace, sourceMove = document.SourceJob!.BaseMove,
                authoredMove = document.Validate().Jobs[0].BaseMove,
                savedMove = 2, savedJump = 4, savedJp = form.Abilities.Items.Count > 0 ? 123 : (int?)null,
                sourceUnchanged = true, exportedRoundTrip = true, failedExportPreserved = true,
                closeCancelPreserved = true, closeDiscardAccepted = true,
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
