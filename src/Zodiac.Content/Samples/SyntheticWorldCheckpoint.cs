using Zodiac.Content.Saves;
using Zodiac.Framework;
using Zodiac.Framework.Campaign;

namespace Zodiac.Content.Samples;

/// <summary>Invented data only; not a retail campaign, item catalog or travel rule.</summary>
public static class SyntheticWorldCheckpoint
{
    public static AssetId Harbor => new("sample:location/harbor");
    public static AssetId Hill => new("sample:location/hill");
    public static WorldCheckpointSaveProfile CreateProfile()
    {
        AssetId blade = new("sample:item/training-blade"), ration = new("sample:item/rations"), job = new("sample:job/courier");
        var definition = new WorldCheckpointDefinition(new("sample:route/world-idle"), new("sample:progress/bridge-open"),
            [new("sample:party/ada"), new("sample:party/borin")], [new(job, [blade])],
            [new(blade, true, 4), new(ration, false, 99)], [Harbor, Hill]);
        return new("sample:profile/world-checkpoint", 1, 1, definition);
    }
    public static WorldCheckpointState Start(WorldCheckpointSaveProfile profile) => profile.Definition.Restore(profile.Definition.RouteId, Harbor,
        [new(new("sample:party/ada"), new("sample:job/courier"), new AssetId("sample:item/training-blade")),
         new(new("sample:party/borin"), new("sample:job/courier"), null)],
        [new(new("sample:item/training-blade"), 2), new(new("sample:item/rations"), 5)],
        100, new(3, 14), profile.Definition.ProgressFlagId, true, 42);
}
