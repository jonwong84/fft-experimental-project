using Zodiac.Content.Authoring;

namespace Fft.Editor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string? source = null, check = null;
            for (int index = 0; index < args.Length; index += 2)
            {
                if (index + 1 == args.Length) return 2;
                switch (args[index])
                {
                    case "--source" when source is null: source = args[index + 1]; break;
                    case "--check" when check is null: check = args[index + 1]; break;
                    default: return 2;
                }
            }
            if (check is not null && (source is null || Directory.Exists(check) || File.Exists(check))) return 2;
            // Reject invalid input before creating native controls or a check directory.
            JobEditorDocument? document = source is null ? null : JobEditorDocument.OpenSource(source);
            ApplicationConfiguration.Initialize();
            using var form = new JobEditorForm(document);
            if (check is not null) EditorNativeCheck.Attach(form, Path.GetFullPath(check));
            Application.Run(form);
            return form.CheckFailed ? 1 : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidDataException or System.Text.Json.JsonException or Zodiac.Content.ContentPackages.ContentPackageValidationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
