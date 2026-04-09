using System.Numerics;
using System.Runtime.InteropServices;

namespace FCRevolution.Rendering.Abstractions;

public sealed class MotionTextureData
{
    private readonly ushort[] _packedVectors;

    public MotionTextureData(int width, int height, ushort[] packedVectors)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        ArgumentNullException.ThrowIfNull(packedVectors);

        int expectedLength = checked(width * height * 2);
        if (packedVectors.Length != expectedLength)
            throw new ArgumentException($"Expected {expectedLength} packed half-floats for a {width}x{height} RG16F texture.", nameof(packedVectors));

        Width = width;
        Height = height;
        _packedVectors = packedVectors;
    }

    public int Width { get; }

    public int Height { get; }

    public ushort[] PackedVectors => _packedVectors;

    public ReadOnlySpan<byte> PackedBytes => MemoryMarshal.AsBytes<ushort>(_packedVectors);

    public Vector2 GetVector(int x, int y)
    {
        if ((uint)x >= (uint)Width)
            throw new ArgumentOutOfRangeException(nameof(x));

        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y));

        int componentIndex = ((y * Width) + x) * 2;
        return UnpackVector(_packedVectors, componentIndex);
    }

    public static void WriteVector(ushort[] packedVectors, int componentIndex, Vector2 vector)
    {
        packedVectors[componentIndex] = BitConverter.HalfToUInt16Bits((Half)vector.X);
        packedVectors[componentIndex + 1] = BitConverter.HalfToUInt16Bits((Half)vector.Y);
    }

    public static Vector2 UnpackVector(ReadOnlySpan<ushort> packedVectors, int componentIndex)
    {
        return new Vector2(
            (float)BitConverter.UInt16BitsToHalf(packedVectors[componentIndex]),
            (float)BitConverter.UInt16BitsToHalf(packedVectors[componentIndex + 1]));
    }
}
