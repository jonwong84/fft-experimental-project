using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.AssetTool;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;

namespace Zodiac.AssetTool.Cli;

/// <summary>Offline selected ordinary spell/job/progression import; encounter setup and grants are authored.</summary>
public static class OriginalOrdinaryMagicImporter
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    private static readonly ImmutableArray<string> OriginalJobNames = ["squire", "chemist", "knight", "archer", "monk", "priest", "wizard",
        "time-mage", "summoner", "thief", "mediator", "oracle", "geomancer", "lancer", "samurai", "ninja", "calculator", "bard", "dancer", "mime"];

    public static OriginalOrdinaryMagicImport ImportVerified(ReadOnlySpan<byte> executable,
        OriginalOrdinarySpellSelection selection = OriginalOrdinarySpellSelection.Fire)
    {
        string hash = OriginalExecutableReader.Verify(executable);
        return DecodeSelection(executable, selection) with { VerifiedInputSha256 = hash };
    }

    /// <summary>Layout testing only; does not certify an edition or input hash.</summary>
    public static OriginalOrdinaryMagicImport DecodeSelection(ReadOnlySpan<byte> executable,
        OriginalOrdinarySpellSelection selection = OriginalOrdinarySpellSelection.Fire)
    {
        if (!Enum.IsDefined(selection)) throw new ArgumentOutOfRangeException(nameof(selection));
        var reader = new OriginalExecutableReader(executable);
        var spells = new List<OrdinaryMagicPreparedSpell>();
        int learningCost = 0;
        foreach (var binding in Bindings.Where(b => selection == OriginalOrdinarySpellSelection.ElementalTiers || b.Id == 16))
        {
            string id = $"fft.ps1:ability/source-{binding.Id:D4}";
            var raw = reader.Read("g_ability_range_data", 0x8005fbf0, 14, binding.Id, id);
            bool fourth = binding.Area == 2;
            byte[] expected = [4, binding.Area, binding.Vertical, 0, (byte)(fourth ? 1 : 0),
                (byte)(fourth ? 34 : 226), (byte)(fourth ? 2 : 66), (byte)binding.Element, 8, 0,
                binding.Power, 0, binding.Charge, binding.Mp];
            if (!raw.SequenceEqual(expected))
                throw new InvalidDataException($"{binding.Name} exceeds the traced ordinary empty-status binding.");
            var learning = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, binding.Id, id));
            if (learning.JpCost > 9999) throw new InvalidDataException("Unsupported ordinary spell learning data.");
            learningCost = checked(learningCost + learning.JpCost);
            spells.Add(new(id, binding.Name, binding.Id,
                new(FaithMagicAmountKind.Damage, raw[10], (MagicElement)raw[7], (raw[6] & 2) != 0),
                raw[13], raw[12], (raw[5] & 0x20) != 0,
                new(raw[0], raw[1], raw[2], CannotHitCaster: (raw[4] & 1) != 0)));
        }
        var status = reader.Read("g_status_infliction_data", 0x80063fc4, 6, 0, null);
        if (status.Any(value => value != 0)) throw new InvalidDataException("Unsupported ordinary spell status data.");
        var skillset = OriginalMetadataRecords.DecodeHumanSkillset(reader.Read("g_ability_human_skillset_abilities", 0x80064a94, 25, 11, "fft.ps1:skillset/black-magic"));
        if (spells.Any(s => skillset[s.OriginalId - 16] != s.OriginalId) ||
            reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 11, "fft.ps1:skillset/black-magic")[0] != 0)
            throw new InvalidDataException("Black Magic does not bind the traced default-menu spell slots.");

        ushort cureLearningCost = 0;
        if (selection == OriginalOrdinarySpellSelection.CureAndFire)
        {
            const string id = "fft.ps1:ability/source-0001";
            var raw = reader.Read("g_ability_range_data", 0x8005fbf0, 14, 1, id);
            byte[] expected = [4, 1, 1, 0, 0, 226, 0, 0, 12, 0, 14, 0, 4, 6];
            if (!raw.SequenceEqual(expected)) throw new InvalidDataException("Cure exceeds the traced ordinary healing binding.");
            cureLearningCost = OriginalMetadataRecords.DecodeAbility(reader.Read("g_ability_data", 0x8005ebf0, 8, 1, id)).JpCost;
            var whiteMagic = OriginalMetadataRecords.DecodeHumanSkillset(reader.Read("g_ability_human_skillset_abilities", 0x80064a94, 25, 10, "fft.ps1:skillset/white-magic"));
            if (cureLearningCost > 9999 || whiteMagic[0] != 1 ||
                reader.Read("g_command_menu_types_by_skillset", 0x80065cb4, 1, 10, "fft.ps1:skillset/white-magic")[0] != 0)
                throw new InvalidDataException("White Magic does not bind the traced default-menu Cure slot and learning data.");
            spells.Insert(0, new(id, "Cure", 1, new(FaithMagicAmountKind.Healing, raw[10], MagicElement.None, false),
                raw[13], raw[12], (raw[5] & 0x20) != 0, new(raw[0], raw[1], raw[2])));
        }

        string JobId(string name) => "fft.ps1:job/" + name;
        OrdinaryMagicPreparedJob ReadJob(byte id, string name, string skillsetId, byte expectedSkillset)
        {
            var raw = reader.Read("g_job_data", 0x800610b8, 48, id, JobId(name));
            var job = OriginalMetadataRecords.DecodeJob(raw);
            if (job.Skillset != expectedSkillset || job.Innates.Any(value => value != 0) ||
                raw.AsSpan(26, 19).ToArray().Any(value => value != 0) || (job.JumpRaw & 0x80) != 0)
                throw new InvalidDataException("Selected jobs require the admitted bare no-innate/RSM/affinity profile.");
            return new(JobId(name), id, new(raw[13], raw[15], raw[17], raw[19], raw[21]),
                new(raw[14], raw[16], raw[18], raw[20], raw[22]), job.Move, (byte)(job.JumpRaw & 127), skillsetId);
        }
        var squire = ReadJob(74, "squire", "fft.ps1:skillset/basic-skill", 5);
        var wizard = ReadJob(80, "wizard", "fft.ps1:skillset/black-magic", 11);
        var unlocks = new List<OrdinaryMagicPreparedUnlock> { new(JobId("squire"), []) };
        for (int i = 1; i < OriginalJobNames.Length; i++)
        {
            var row = reader.Read("g_job_unlock_requirements", 0x800660c4, 10, i - 1, JobId(OriginalJobNames[i]));
            var requirements = new Dictionary<string, byte>();
            for (int j = 0; j < OriginalJobNames.Length; j++)
            {
                byte value = (byte)(j % 2 == 0 ? row[j / 2] >> 4 : row[j / 2] & 15);
                if (value != 0)
                    requirements.Add(JobId(OriginalJobNames[j]), value);
            }
            unlocks.Add(new(JobId(OriginalJobNames[i]), requirements));
        }
        var thresholds = new ushort[8];
        for (int i = 0; i < thresholds.Length; i++)
            thresholds[i] = BinaryPrimitives.ReadUInt16LittleEndian(reader.Read("g_job_jp_requirements_by_level", 0x80066182, 2, i + 1, null));
        Dictionary<string, JobPointBalance> Career() => OriginalJobNames.ToDictionary(JobId, _ => new JobPointBalance(0, 0, 0));
        var mageCareer = Career();
        mageCareer[JobId("chemist")] = JobPointArithmetic.Add(200, 200, 0, thresholds.ToImmutableArray());
        if (learningCost > 9999) throw new InvalidDataException("Selected learned grants exceed the authored career budget.");
        ushort wizardTotal = (ushort)Math.Max(selection == OriginalOrdinarySpellSelection.ElementalTiers ? 5000 : 100, learningCost);
        mageCareer[JobId("wizard")] = JobPointArithmetic.Add((ushort)(wizardTotal - learningCost), wizardTotal, 0, thresholds.ToImmutableArray());
        if (selection == OriginalOrdinarySpellSelection.CureAndFire)
            mageCareer[JobId("priest")] = JobPointArithmetic.Add(0, cureLearningCost, 0, thresholds.ToImmutableArray());
        var guardCareer = Career();
        guardCareer[JobId("squire")] = JobPointArithmetic.Add(100, 100, 0, thresholds.ToImmutableArray());
        uint Raw(int value, byte multiplier) => multiplier == 0
            ? throw new InvalidDataException("The authored encounter requires nonzero copied multipliers.")
            : checked((uint)(((long)value * 1638400 + multiplier - 1) / multiplier));
        BattleRawStats Stats(OrdinaryMagicPreparedJob job, int hp, int mp, int speed, int pa, int ma) =>
            new(Raw(hp, job.Multipliers.Hp), Raw(mp, job.Multipliers.Mp), Raw(speed, job.Multipliers.Speed),
                Raw(pa, job.Multipliers.PhysicalAttack), Raw(ma, job.Multipliers.MagicAttack));
        var units = new[]
        {
            new OrdinaryMagicPreparedUnit("mage", "Mage", 0, 0, 2, SpellTargetTeam.Blue, wizard.Id, 10, 95, 70, 70,
                MagicZodiacSign.Aries, true, false, Stats(wizard, 100, 75, 10, 6, 8), new(6, 8, 10), 100, 75, mageCareer, spells.Select(s => s.Id).ToArray()),
            new OrdinaryMagicPreparedUnit("guard", "Guard", 1, 4, 2, SpellTargetTeam.Red, squire.Id, 10, 0, 70, 70,
                MagicZodiacSign.Aries, true, false, Stats(squire, 200, 50, 8, 8, 5), new(8, 5, 8),
                (ushort)(selection == OriginalOrdinarySpellSelection.CureAndFire ? 30 : 200), 50, guardCareer, [])
        };
        var document = new OrdinaryMagicPackageDocument("fft.ordinary-magic-battle", 1, "ordinary-human-bare-v1", "original-fft",
            57, 7, 5, 10, JobId("squire"), JobId("bard"), JobId("dancer"), thresholds, spells.ToArray(), [wizard, squire], unlocks.ToArray(), units);
        return new(PreparedOrdinaryMagicBattle.Create(document), null, reader.Records.ToImmutableArray());
    }

    public static string Provenance(OriginalOrdinaryMagicImport import) => JsonSerializer.Serialize(new
    {
        format = "fft.original-ordinary-magic-import", schemaVersion = 1, referenceRevision = ReferenceRevision,
        verifiedInputSha256 = import.VerifiedInputSha256,
        confidence = import.VerifiedInputSha256 is null ? "unverified-layout-decoding" : "verified-source-traced-records",
        scope = "original-ordinary-spells-selected-job-and-progression-data; authored encounter, units, learned grants and seed",
        records = import.Records,
        limitations = "No original encounter, assets/animation, generic statuses/AI, corpse scheduling or frame RNG equivalence is inferred."
    }, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";

    // Each row independently inspected against the verified executable and pinned source.
    private static readonly (short Id, string Name, byte Power, byte Area, byte Vertical, byte Charge, byte Mp, MagicElement Element)[] Bindings =
    [
        (16, "Fire", 14, 1, 1, 4, 6, MagicElement.Fire),
        (17, "Fire 2", 18, 1, 2, 5, 12, MagicElement.Fire),
        (18, "Fire 3", 24, 1, 3, 7, 24, MagicElement.Fire),
        (19, "Fire 4", 32, 2, 3, 10, 48, MagicElement.Fire),
        (20, "Bolt", 14, 1, 1, 4, 6, MagicElement.Lightning),
        (21, "Bolt 2", 18, 1, 2, 5, 10, MagicElement.Lightning),
        (22, "Bolt 3", 24, 1, 3, 7, 24, MagicElement.Lightning),
        (23, "Bolt 4", 32, 2, 3, 10, 48, MagicElement.Lightning),
        (24, "Ice", 14, 1, 1, 4, 6, MagicElement.Ice),
        (25, "Ice 2", 18, 1, 2, 5, 12, MagicElement.Ice),
        (26, "Ice 3", 24, 1, 3, 7, 24, MagicElement.Ice),
        (27, "Ice 4", 32, 2, 3, 10, 48, MagicElement.Ice)
    ];
}
