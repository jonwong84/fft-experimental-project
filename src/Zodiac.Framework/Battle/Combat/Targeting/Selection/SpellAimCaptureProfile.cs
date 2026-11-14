namespace Zodiac.Framework;

/// <summary>Caller certifies the unchanged selection/prompt and the source-preferred cursor occupant, including known empty.</summary>
public sealed record SpellAimCaptureProfile(bool SelectionContextUnchangedKnown = false,
    bool AnswerOfferedByPromptKnown = false, bool CursorOccupantSelectionKnown = false);
