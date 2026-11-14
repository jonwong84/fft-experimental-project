namespace Zodiac.Framework;

/// <summary>Original eight-bit element encoding for import/export boundaries; combat rules use boolean sets.</summary>
public static class OriginalElementEncoding
{
    public static ElementToggles FromByte(byte value) => new()
    {
        Dark = (value & 0x01) != 0,
        Holy = (value & 0x02) != 0,
        Water = (value & 0x04) != 0,
        Earth = (value & 0x08) != 0,
        Wind = (value & 0x10) != 0,
        Ice = (value & 0x20) != 0,
        Lightning = (value & 0x40) != 0,
        Fire = (value & 0x80) != 0
    };

    public static byte ToByte(ElementToggles elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.HasCustomElements)
            throw new NotSupportedException("Custom elements cannot be encoded in the original eight-bit element field.");
        return (byte)((elements.Dark ? 0x01 : 0) | (elements.Holy ? 0x02 : 0) |
            (elements.Water ? 0x04 : 0) | (elements.Earth ? 0x08 : 0) |
            (elements.Wind ? 0x10 : 0) | (elements.Ice ? 0x20 : 0) |
            (elements.Lightning ? 0x40 : 0) | (elements.Fire ? 0x80 : 0));
    }

    public static string ToNames(ElementToggles elements)
    {
        _ = ToByte(elements); // Reject custom elements rather than losing them in a legacy field.
        var names = new List<string>();
        if (elements.Dark) names.Add(nameof(elements.Dark));
        if (elements.Holy) names.Add(nameof(elements.Holy));
        if (elements.Water) names.Add(nameof(elements.Water));
        if (elements.Earth) names.Add(nameof(elements.Earth));
        if (elements.Wind) names.Add(nameof(elements.Wind));
        if (elements.Ice) names.Add(nameof(elements.Ice));
        if (elements.Lightning) names.Add(nameof(elements.Lightning));
        if (elements.Fire) names.Add(nameof(elements.Fire));
        return names.Count == 0 ? "None" : string.Join(", ", names);
    }

    public static ElementToggles FromNames(string names)
    {
        ArgumentNullException.ThrowIfNull(names);
        byte value = 0;
        foreach (string name in names.Split(','))
        {
            value |= name.Trim().ToUpperInvariant() switch
            {
                "NONE" => (byte)0,
                "DARK" => (byte)1,
                "HOLY" => (byte)2,
                "WATER" => (byte)4,
                "EARTH" => (byte)8,
                "WIND" => (byte)16,
                "ICE" => (byte)32,
                "LIGHTNING" => (byte)64,
                "FIRE" => (byte)128,
                _ => throw new FormatException("Expected original element names; custom elements need a separate format/profile.")
            };
        }
        return FromByte(value);
    }
}
