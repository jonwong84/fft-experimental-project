namespace Fft.AssetTool;

/// <summary>Decodes the compressed attack image at the end of a unit SPR file.</summary>
/// <remarks>
/// The original game expands this high-nibble-first stream into a 256 by 200
/// 4bpp image. Unlike the original routine, this API requires the actual source
/// length and reports truncated input instead of reading past it.
/// </remarks>
public static class SpriteAttackImageDecoder
{
    public const int ImageWidth = 256;
    public const int ImageHeight = 200;
    public const int DecodedByteLength = ImageWidth * ImageHeight / 2;

    /// <summary>Decodes one complete compressed attack image into packed 4bpp pixels.</summary>
    /// <param name="source">The compressed nibble stream, beginning at SPR offset 0x9200.</param>
    /// <returns>The decoded image, with the first pixel in each byte's high nibble.</returns>
    /// <exception cref="InvalidDataException">The source ends before the image is complete.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> source)
    {
        var destination = new byte[DecodedByteLength];
        var sourceNibblePosition = 0;
        var outputPixelPosition = 0;
        var outputPixelCount = DecodedByteLength * 2;

        while (outputPixelPosition < outputPixelCount)
        {
            var value = ReadNibble(source, ref sourceNibblePosition);
            if (value == 0)
            {
                var runCode = ReadNibble(source, ref sourceNibblePosition);
                var runLength = runCode;
                switch (runCode)
                {
                    case 0:
                        runLength = ReadNibble(source, ref sourceNibblePosition);
                        break;
                    case 7:
                        runLength = ReadNibble(source, ref sourceNibblePosition);
                        runLength |= ReadNibble(source, ref sourceNibblePosition) << 4;
                        break;
                    case 8:
                        runLength = ReadNibble(source, ref sourceNibblePosition);
                        runLength |= ReadNibble(source, ref sourceNibblePosition) << 4;
                        runLength |= ReadNibble(source, ref sourceNibblePosition) << 8;
                        break;
                }

                // Transparent pixels are already zero in the new output array.
                outputPixelPosition += Math.Min(runLength, outputPixelCount - outputPixelPosition);
                continue;
            }

            var bytePosition = outputPixelPosition >> 1;
            if ((outputPixelPosition & 1) == 0)
            {
                destination[bytePosition] = (byte)(value << 4);
            }
            else
            {
                destination[bytePosition] |= (byte)value;
            }

            outputPixelPosition++;
        }

        return destination;
    }

    private static int ReadNibble(ReadOnlySpan<byte> source, ref int nibblePosition)
    {
        var bytePosition = nibblePosition >> 1;
        if ((uint)bytePosition >= (uint)source.Length)
        {
            throw new InvalidDataException("Compressed attack image ended before the decoded image was complete.");
        }

        var value = source[bytePosition];
        nibblePosition++;
        return (nibblePosition & 1) == 1 ? value >> 4 : value & 0x0f;
    }
}
