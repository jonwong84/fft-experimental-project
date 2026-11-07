using Zodiac.Content;
using Zodiac.Content.Events;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

/// <summary>Prepared-files-only composition. Construction validates every resource before a scene can be started.</summary>
public sealed class OrderedBannerSession
{
    public PreparedOrderedBannerEvent Definition { get; }
    public LoadedGeneralBanner Artwork { get; }
    private readonly OrderedBannerProgram program;
    private OrderedBannerSession(PreparedOrderedBannerEvent definition, LoadedGeneralBanner artwork)
    {
        Definition = definition; Artwork = artwork; program = definition.ToExecutionDefinition();
    }

    public static OrderedBannerSession Load(LooseAssetStore events, AssetId eventId, LooseAssetStore artwork)
    {
        var definition = PreparedOrderedBannerEvent.Load(events, eventId);
        var loaded = PreparedGeneralBanner.Load(artwork, definition.ArtworkId);
        return new(definition, loaded);
    }

    public OrderedBannerResult Start(AssetId sceneRunId, AssetId consumerId, ConditionalEventVariables variables = default)
        => OrderedBannerExecutor.RunUntilBlocked(program.Start(variables, SceneTaskState.Create(sceneRunId), consumerId,
            new(Definition.DefinitionId, new(Artwork.Definition.Id), Definition.OwnerId)), ConditionalEventProgram.MaxInstructions);
}

public sealed record OrderedBannerPreviewOptions(string EventRoot, AssetId EventId, BannerPreviewOptions Presentation)
{
    public const string Usage = "--ordered-banner --event-root <directory> --event-id <id> --artwork-root <directory> [--precision rgb8|five-bit --dither off|on] [--check <fresh-scratch-directory>]";
    public static OrderedBannerPreviewOptions Parse(string[] args)
    {
        if (args.Length < 7 || args[0] != "--ordered-banner" || args.Length % 2 != 1) throw new ArgumentException(Usage);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (args[i] is not ("--event-root" or "--event-id" or "--artwork-root" or "--precision" or "--dither" or "--check") ||
                string.IsNullOrWhiteSpace(args[i + 1]) || !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Usage);
        if (!values.ContainsKey("--event-root") || !values.ContainsKey("--event-id") || !values.ContainsKey("--artwork-root")) throw new ArgumentException(Usage);
        var presentationArgs = new List<string> { "--general-banner", "--content-root", values["--artwork-root"] };
        foreach (var key in new[] { "--precision", "--dither", "--check" })
            if (values.TryGetValue(key, out var value)) presentationArgs.AddRange([key, value]);
        return new(values["--event-root"], new(values["--event-id"]), BannerPreviewOptions.Parse(presentationArgs.ToArray()));
    }
}

public static class OrderedBannerPreview
{
    public static int Run(string[] args)
    {
        try
        {
            var options = OrderedBannerPreviewOptions.Parse(args);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Ordered banner window supports Windows only.");
            var session = OrderedBannerSession.Load(LooseAssetStore.Load(options.EventRoot), options.EventId,
                LooseAssetStore.Load(options.Presentation.ContentRoot));
            string? check = GeneralBannerPreview.CheckPath(options.Presentation.CheckRoot);
            using var window = new GeneralBannerWindow(session.Artwork, check, options.Presentation, session);
            window.Run();
            if (check is not null && !window.CheckCompleted) throw new InvalidOperationException("Ordered banner check did not finish.");
            return 0;
        }
        catch (Exception e)
        { Console.Error.WriteLine("Ordered banner preview failed: " + e.Message); return 1; }
    }
}
