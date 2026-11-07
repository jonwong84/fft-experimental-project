using System.Collections.Immutable;
using Zodiac.Content.BattleSetup;

namespace Zodiac.AssetTool.Cli;

public sealed record OriginalOrdinaryMagicImport(PreparedOrdinaryMagicBattle Package, string? VerifiedInputSha256,
    ImmutableArray<OriginalMetadataSourceRecord> Records);
