using Fft.Content.Definitions;
using Xunit;

namespace Fft.Content.Tests.Definitions;

public sealed class DefinitionTests
{
    private static readonly JobId Recruit = new("sample:job/recruit");
    private static readonly SkillsetId Practice = new("sample:skillset/practice");
    private static readonly AbilityId Focus = new("sample:ability/focus");

    [Theory]
    [InlineData("")]
    [InlineData("unqualified")]
    [InlineData("Sample:job/recruit")]
    [InlineData("sample:job/../recruit")]
    [InlineData("sample:job//recruit")]
    [InlineData("sample:job/recruit:second")]
    [InlineData("sample:job/recruït")]
    public void Typed_ids_reject_invalid_identity_without_normalizing(string value)
    {
        Assert.Contains("Job ID", Assert.Throws<ArgumentException>(() => new JobId(value)).Message);
        Assert.Contains("Skillset ID", Assert.Throws<ArgumentException>(() => new SkillsetId(value)).Message);
        Assert.Contains("Ability ID", Assert.Throws<ArgumentException>(() => new AbilityId(value)).Message);
    }

    [Fact]
    public void Typed_ids_have_ordinal_value_identity_and_distinct_types()
    {
        var same = new JobId("sample:job/recruit");
        Assert.Equal(Recruit, same);
        Assert.NotEqual(Recruit, new JobId("sample:job/other"));
        Assert.False(Recruit.Equals((object)new AbilityId(Recruit.Value)));
        Assert.Equal("sample:job/recruit", Recruit.ToString());
        Assert.Equal(42, new Dictionary<JobId, int> { [Recruit] = 42 }[same]);
    }

    [Fact]
    public void Definitions_reject_default_ids_in_identity_and_references()
    {
        Assert.Contains("non-default", Assert.Throws<ArgumentException>(
            () => new JobDefinition(default, Practice, 3, 2, false, [])).Message);
        Assert.Throws<ArgumentException>(() => new JobDefinition(Recruit, default, 3, 2, false, []));
        Assert.Throws<ArgumentException>(() => Job([default]));
        Assert.Throws<ArgumentException>(() => new SkillsetDefinition(default, []));
        Assert.Throws<ArgumentException>(() => new SkillsetSlot(0, default(AbilityId)));
        Assert.Throws<ArgumentException>(() => new AbilityDefinition(default, 25, 0, 0x41, [0, 0, 0, 0]));
    }

    [Theory]
    [InlineData(-1, 2, "baseMove")]
    [InlineData(256, 2, "baseMove")]
    [InlineData(3, -1, "baseJump")]
    [InlineData(3, 128, "baseJump")]
    public void Job_validates_source_traced_value_widths(int move, int jump, string parameter)
    {
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobDefinition(Recruit, Practice, move, jump, false, [])).ParamName);
    }

    [Fact]
    public void Job_preserves_maximum_base_values_and_copies_innate_sequence()
    {
        var innates = new List<AbilityId> { Focus, Focus };
        var job = new JobDefinition(Recruit, Practice, 255, 127, true, innates);
        innates.Clear();
        Assert.Equal(255, job.BaseMove);
        Assert.Equal(127, job.BaseJump);
        Assert.True(job.SteppingStone);
        Assert.Equal(new[] { Focus, Focus }, job.InnateAbilityIds);
    }

    [Fact]
    public void Skillset_keeps_explicit_slots_empty_slots_and_duplicate_references_in_supplied_order()
    {
        var slots = new List<SkillsetSlot> { new(9, Focus), new(0, null), new(5, Focus) };
        var skillset = new SkillsetDefinition(Practice, slots);
        slots.Reverse();
        slots.Clear();
        Assert.Equal(new[] { 9, 0, 5 }, skillset.Slots.Select(slot => slot.Slot));
        Assert.Null(skillset.Slots[1].AbilityId);
        Assert.Equal(Focus, skillset.Slots[0].AbilityId);
        Assert.Equal(Focus, skillset.Slots[2].AbilityId);
    }

    [Fact]
    public void Skillset_rejects_duplicate_and_negative_slot_numbers()
    {
        string error = Assert.Throws<ArgumentException>(() => new SkillsetDefinition(
            Practice, [new(7, Focus), new(7, null)])).Message;
        Assert.Contains(Practice.Value, error);
        Assert.Contains("duplicate slot 7", error);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillsetSlot(-1, null));
    }

    [Theory]
    [InlineData(0x01, 1, false, false, true, 0)]
    [InlineData(0x61, 1, true, true, true, 0)]
    [InlineData(0xff, 15, true, true, false, 0x10)]
    [InlineData(0x90, 0, false, false, false, 0x10)]
    public void Ability_projects_confirmed_flags_without_losing_unknown_bits(
        byte raw, int serializedClass, bool learnOnHit, bool showName, bool canLearn, byte unknown)
    {
        var definition = new AbilityDefinition(Focus, 65535, 255, raw, [0xff, 0x80, 0x01, 0x42]);
        Assert.Equal(raw, definition.TypeFlagsRaw);
        Assert.Equal(serializedClass, definition.SerializedClassRaw);
        Assert.Equal(learnOnHit, definition.LearnOnHit);
        Assert.Equal(showName, definition.ShowName);
        Assert.Equal(canLearn, definition.CanLearnWithJp);
        Assert.Equal(unknown, definition.UnknownTypeFlags);
        Assert.Equal((AbilityLearningFlags)(raw & 0xe0), definition.LearningFlags);
        Assert.Equal(65535, definition.JpCost);
        Assert.Equal(255, definition.LearnOnHitRateRaw);
    }

    [Theory]
    [InlineData(-1, 0, "jpCost")]
    [InlineData(65536, 0, "jpCost")]
    [InlineData(25, -1, "learnOnHitRateRaw")]
    [InlineData(25, 256, "learnOnHitRateRaw")]
    public void Ability_rejects_out_of_width_values(int cost, int rate, string parameter)
    {
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(
            () => new AbilityDefinition(Focus, cost, rate, 1, [0, 0, 0, 0])).ParamName);
    }

    [Fact]
    public void Ability_copies_exactly_four_opaque_AI_bytes()
    {
        byte[] flags = [1, 2, 3, 255];
        var definition = new AbilityDefinition(Focus, 0, 0, 1, flags);
        flags[3] = 0;
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, definition.AiFlagsRaw);
        Assert.Contains("four", Assert.Throws<ArgumentException>(
            () => new AbilityDefinition(Focus, 0, 0, 1, [0, 0, 0])).Message);
        Assert.Throws<ArgumentException>(() => new AbilityDefinition(Focus, 0, 0, 1, [0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Catalog_resolves_typed_references_and_snapshots_its_collections()
    {
        JobDefinition job = Job([Focus]);
        var skillset = new SkillsetDefinition(Practice, [new(0, Focus), new(1, null)]);
        AbilityDefinition ability = Ability();
        var jobs = new List<JobDefinition> { job };
        var skillsets = new List<SkillsetDefinition> { skillset };
        var abilities = new List<AbilityDefinition> { ability };
        var catalog = new DefinitionCatalog(jobs, skillsets, abilities);
        jobs.Clear();
        skillsets.Clear();
        abilities.Clear();
        Assert.Same(job, Assert.Single(catalog.Jobs));
        Assert.Same(skillset, catalog.GetSkillset(job.PrimarySkillsetId));
        Assert.Same(ability, catalog.GetAbility(job.InnateAbilityIds[0]));
        Assert.Same(job, catalog.GetJob(new JobId(Recruit.Value)));
    }

    [Fact]
    public void Catalog_rejects_duplicate_ids_for_each_definition_kind()
    {
        JobDefinition job = Job([]);
        var skillset = new SkillsetDefinition(Practice, []);
        AbilityDefinition ability = Ability();
        string jobsError = Assert.Throws<ArgumentException>(() => new DefinitionCatalog(
            [job, Job([])], [skillset], [])).Message;
        string skillsetsError = Assert.Throws<ArgumentException>(() => new DefinitionCatalog(
            [], [skillset, new SkillsetDefinition(Practice, [])], [])).Message;
        string abilitiesError = Assert.Throws<ArgumentException>(() => new DefinitionCatalog(
            [], [], [ability, Ability()])).Message;
        Assert.Contains($"duplicate job ID '{Recruit}'", jobsError);
        Assert.Contains($"duplicate skillset ID '{Practice}'", skillsetsError);
        Assert.Contains($"duplicate ability ID '{Focus}'", abilitiesError);
    }

    [Fact]
    public void Catalog_reports_owner_and_unresolved_primary_skillset()
    {
        string error = Assert.Throws<ArgumentException>(() => new DefinitionCatalog([Job([])], [], [])).Message;
        Assert.Contains($"Job '{Recruit}'", error);
        Assert.Contains($"unresolved primary skillset '{Practice}'", error);
    }

    [Fact]
    public void Catalog_reports_owner_and_unresolved_innate_reference()
    {
        string error = Assert.Throws<ArgumentException>(() => new DefinitionCatalog(
            [Job([Focus])], [new SkillsetDefinition(Practice, [])], [])).Message;
        Assert.Contains($"Job '{Recruit}' innate ability 0", error);
        Assert.Contains($"unresolved ability '{Focus}'", error);
    }

    [Fact]
    public void Catalog_reports_skillset_slot_and_unresolved_ability()
    {
        string error = Assert.Throws<ArgumentException>(() => new DefinitionCatalog(
            [], [new SkillsetDefinition(Practice, [new(19, Focus)])], [])).Message;
        Assert.Contains($"Skillset '{Practice}' slot 19", error);
        Assert.Contains($"unresolved ability '{Focus}'", error);
    }

    [Fact]
    public void Catalog_supports_more_jobs_skillsets_abilities_and_slots_than_PS1_tables()
    {
        AbilityDefinition[] abilities = Enumerable.Range(0, 600).Select(index => new AbilityDefinition(
            new AbilityId($"sample:ability/{index}"), index, 0, 1, [0, 0, 0, 0])).ToArray();
        SkillsetDefinition[] skillsets = Enumerable.Range(0, 300).Select(index => new SkillsetDefinition(
            new SkillsetId($"sample:skillset/{index}"), Enumerable.Range(0, 30).Select(slot =>
                new SkillsetSlot(slot, abilities[(index + slot) % abilities.Length].Id)))).ToArray();
        JobDefinition[] jobs = Enumerable.Range(0, 200).Select(index => new JobDefinition(
            new JobId($"sample:job/{index}"), skillsets[index].Id, 3, 2, false,
            abilities.Take(5).Select(ability => ability.Id))).ToArray();
        var catalog = new DefinitionCatalog(jobs, skillsets, abilities);
        Assert.Equal(200, catalog.Jobs.Length);
        Assert.Equal(300, catalog.Skillsets.Length);
        Assert.Equal(600, catalog.Abilities.Length);
        Assert.Equal(30, catalog.GetSkillset(jobs[199].PrimarySkillsetId).Slots.Length);
        Assert.Equal(5, catalog.GetJob(jobs[199].Id).InnateAbilityIds.Length);
        Assert.Same(abilities[599], catalog.GetAbility(new AbilityId("sample:ability/599")));
    }

    [Fact]
    public void Null_collections_and_entries_fail_at_construction()
    {
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog([], null!, []));
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog([], [], null!));
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog([null!], [], []));
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog([], [null!], []));
        Assert.Throws<ArgumentNullException>(() => new DefinitionCatalog([], [], [null!]));
        Assert.Throws<ArgumentNullException>(() => new SkillsetDefinition(Practice, [null!]));
        Assert.Throws<ArgumentNullException>(() => new SkillsetDefinition(Practice, null!));
        Assert.Throws<ArgumentNullException>(() => Job(null!));
        Assert.Throws<ArgumentNullException>(() => new AbilityDefinition(Focus, 0, 0, 1, null!));
    }

    private static JobDefinition Job(IEnumerable<AbilityId> innates) => new(Recruit, Practice, 3, 2, false, innates);
    private static AbilityDefinition Ability() => new(Focus, 25, 0, 0x41, [0, 0, 0, 0]);
}
