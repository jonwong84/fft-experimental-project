namespace Fft.Client.Presentation;

public sealed record BannerPreviewOptions(string ContentRoot, string? CheckRoot, bool FiveBit, bool? Dither)
{
    public const string Usage = "Usage: --general-banner --content-root <prepared-directory> [--precision rgb8|five-bit --dither off|on] [--check <fresh-scratch-directory>]";

    public static BannerPreviewOptions Parse(string[] args)
    {
        if (args.Length < 3 || args[0] != "--general-banner" || args.Length % 2 != 1) throw new ArgumentException(Usage);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
        {
            if (args[i] is not ("--content-root" or "--check" or "--precision" or "--dither") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException(Usage);
        }
        if (!values.TryGetValue("--content-root", out var root)) throw new ArgumentException(Usage);
        string precision = values.GetValueOrDefault("--precision", "rgb8");
        string? dither = values.GetValueOrDefault("--dither");
        if (precision is not ("rgb8" or "five-bit") ||
            (precision == "five-bit" ? dither is not ("off" or "on") : dither is not null)) throw new ArgumentException(Usage);
        return new(root, values.GetValueOrDefault("--check"), precision == "five-bit", dither is null ? null : dither == "on");
    }
}
