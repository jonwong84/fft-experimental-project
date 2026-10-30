using System.Text;
using System.Text.Json;
using Zodiac.Content.Definitions;

namespace Zodiac.Content.ContentPackages;

public sealed partial class ContentPackage
{
    /// <summary>Exports validated metadata as deterministic UTF-8-compatible JSON with a final LF.</summary>
    public string ToJson()
    {
        using var stream = new PackageWriteBuffer(this);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("packageId", PackageId);
            writer.WriteString("profileId", ProfileId);
            writer.WriteString("kind", Kind == ContentPackageKind.Baseline ? "baseline" : "addon");
            writer.WriteString("requiredBaselinePackageId", RequiredBaselinePackageId);
            writer.WriteStartArray("jobs");
            foreach (JobDefinition job in Jobs.OrderBy(job => job.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", job.Id.Value);
                writer.WriteString("primarySkillsetId", job.PrimarySkillsetId.Value);
                writer.WriteNumber("baseMove", job.BaseMove);
                writer.WriteNumber("baseJump", job.BaseJump);
                writer.WriteBoolean("steppingStone", job.SteppingStone);
                writer.WriteStartArray("innateAbilityIds");
                foreach (AbilityId id in job.InnateAbilityIds) writer.WriteStringValue(id.Value);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("skillsets");
            foreach (SkillsetDefinition skillset in Skillsets.OrderBy(skillset => skillset.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", skillset.Id.Value);
                writer.WriteStartArray("slots");
                foreach (SkillsetSlot slot in skillset.Slots)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("index", slot.Slot);
                    writer.WriteString("abilityId", slot.AbilityId?.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("abilities");
            foreach (AbilityDefinition ability in Abilities.OrderBy(ability => ability.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", ability.Id.Value);
                writer.WriteNumber("jpCost", ability.JpCost);
                writer.WriteNumber("learnOnHitRateRaw", ability.LearnOnHitRateRaw);
                writer.WriteNumber("serializedClassRaw", ability.SerializedClassRaw);
                writer.WriteStartArray("flags");
                if (ability.LearnOnHit) writer.WriteStringValue("learn-on-hit");
                if (ability.ShowName) writer.WriteStringValue("show-name");
                if (!ability.CanLearnWithJp) writer.WriteStringValue("cannot-learn-with-jp");
                writer.WriteEndArray();
                writer.WriteNumber("unknownTypeBitsRaw", ability.UnknownTypeFlags);
                writer.WriteStartArray("aiFlagsRaw");
                foreach (byte flag in ability.AiFlagsRaw) writer.WriteNumberValue(flag);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    private sealed class PackageWriteBuffer(ContentPackage owner) : MemoryStream
    {
        private void Check(int count)
        {
            if (Position + count > MaxJsonBytes) throw owner.Error(null, "$", $"Export exceeds {MaxJsonBytes} UTF-8 bytes.");
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }
        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }
    }
}
