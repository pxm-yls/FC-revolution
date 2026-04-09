using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FC_Revolution.UI.Infrastructure;

internal static class LanQrCodeBitmapFactory
{
    private const int Version = 2;
    private const int Size = 25;
    private const int DataCodewordCount = 34;
    private const int EccCodewordCount = 10;
    private const int QuietZoneModules = 4;
    private const int ModuleScale = 8;
    private const string AlphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
    private static readonly byte[] ExpTable = BuildExpTable();
    private static readonly byte[] LogTable = BuildLogTable();

    public static WriteableBitmap Create(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("QR text is required.", nameof(text));

        var normalized = text.Trim().ToUpperInvariant();
        var dataCodewords = EncodeAlphanumeric(normalized);
        var eccCodewords = ComputeEcc(dataCodewords, EccCodewordCount);
        var allCodewords = dataCodewords.Concat(eccCodewords).ToArray();
        var modules = BuildMatrix(allCodewords);
        return RenderBitmap(modules);
    }

    private static byte[] EncodeAlphanumeric(string text)
    {
        if (text.Length > 47)
            throw new InvalidOperationException("Version 2-L QR code cannot encode more than 47 alphanumeric characters.");

        var bits = new List<bool>(DataCodewordCount * 8);
        AppendBits(bits, 0b0010, 4);
        AppendBits(bits, text.Length, 9);

        for (var i = 0; i < text.Length; i += 2)
        {
            var first = GetAlphanumericValue(text[i]);
            if (i + 1 < text.Length)
            {
                var second = GetAlphanumericValue(text[i + 1]);
                AppendBits(bits, first * 45 + second, 11);
            }
            else
            {
                AppendBits(bits, first, 6);
            }
        }

        var capacityBits = DataCodewordCount * 8;
        AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
        while (bits.Count % 8 != 0)
            bits.Add(false);

        var data = new List<byte>(DataCodewordCount);
        for (var i = 0; i < bits.Count; i += 8)
        {
            var value = 0;
            for (var j = 0; j < 8; j++)
                value = (value << 1) | (bits[i + j] ? 1 : 0);

            data.Add((byte)value);
        }

        var padBytes = new byte[] { 0xEC, 0x11 };
        var padIndex = 0;
        while (data.Count < DataCodewordCount)
        {
            data.Add(padBytes[padIndex % padBytes.Length]);
            padIndex++;
        }

        return data.ToArray();
    }

    private static bool[,] BuildMatrix(byte[] codewords)
    {
        var modules = new bool[Size, Size];
        var function = new bool[Size, Size];

        DrawFinderPattern(modules, function, 0, 0);
        DrawFinderPattern(modules, function, Size - 7, 0);
        DrawFinderPattern(modules, function, 0, Size - 7);
        DrawAlignmentPattern(modules, function, 18, 18);
        DrawTimingPatterns(modules, function);
        ReserveFormatAreas(function);
        SetFunctionModule(modules, function, 8, Size - 8, true);

        var dataBits = new List<bool>(codewords.Length * 8);
        foreach (var codeword in codewords)
        {
            for (var bit = 7; bit >= 0; bit--)
                dataBits.Add(((codeword >> bit) & 1) != 0);
        }

        var bitIndex = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
                right--;

            for (var vert = 0; vert < Size; vert++)
            {
                var y = (((right + 1) & 2) == 0) ? Size - 1 - vert : vert;
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    if (function[y, x])
                        continue;

                    var bit = bitIndex < dataBits.Count && dataBits[bitIndex];
                    bitIndex++;
                    modules[y, x] = bit ^ ((x + y) % 2 == 0);
                }
            }
        }

        DrawFormatBits(modules, function, 0);
        return modules;
    }

    private static void DrawFinderPattern(bool[,] modules, bool[,] function, int left, int top)
    {
        for (var dy = -1; dy <= 7; dy++)
        {
            for (var dx = -1; dx <= 7; dx++)
            {
                var x = left + dx;
                var y = top + dy;
                if (x < 0 || x >= Size || y < 0 || y >= Size)
                    continue;

                var isBorder = dx is >= 0 and <= 6 && (dy == 0 || dy == 6) ||
                               dy is >= 0 and <= 6 && (dx == 0 || dx == 6);
                var isCenter = dx is >= 2 and <= 4 && dy is >= 2 and <= 4;
                SetFunctionModule(modules, function, x, y, isBorder || isCenter);
            }
        }
    }

    private static void DrawAlignmentPattern(bool[,] modules, bool[,] function, int centerX, int centerY)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                SetFunctionModule(modules, function, centerX + dx, centerY + dy, distance != 1);
            }
        }
    }

    private static void DrawTimingPatterns(bool[,] modules, bool[,] function)
    {
        for (var i = 8; i < Size - 8; i++)
        {
            var dark = i % 2 == 0;
            SetFunctionModule(modules, function, 6, i, dark);
            SetFunctionModule(modules, function, i, 6, dark);
        }
    }

    private static void ReserveFormatAreas(bool[,] function)
    {
        for (var i = 0; i <= 8; i++)
        {
            if (i != 6)
            {
                function[i, 8] = true;
                function[8, i] = true;
            }
        }

        for (var i = 0; i < 8; i++)
            function[8, Size - 1 - i] = true;

        for (var i = 0; i < 7; i++)
            function[Size - 1 - i, 8] = true;
    }

    private static void DrawFormatBits(bool[,] modules, bool[,] function, int mask)
    {
        const int errorCorrectionLevelBits = 0b01;
        var data = (errorCorrectionLevelBits << 3) | mask;
        var bits = data << 10;
        const int generator = 0x537;
        for (var i = 14; i >= 10; i--)
        {
            if (((bits >> i) & 1) != 0)
                bits ^= generator << (i - 10);
        }

        var format = ((data << 10) | bits) ^ 0x5412;
        for (var i = 0; i <= 5; i++)
            SetFunctionModule(modules, function, 8, i, GetBit(format, i));

        SetFunctionModule(modules, function, 8, 7, GetBit(format, 6));
        SetFunctionModule(modules, function, 8, 8, GetBit(format, 7));
        SetFunctionModule(modules, function, 7, 8, GetBit(format, 8));

        for (var i = 9; i < 15; i++)
            SetFunctionModule(modules, function, 14 - i, 8, GetBit(format, i));

        for (var i = 0; i < 8; i++)
            SetFunctionModule(modules, function, Size - 1 - i, 8, GetBit(format, i));

        for (var i = 8; i < 15; i++)
            SetFunctionModule(modules, function, 8, Size - 15 + i, GetBit(format, i));
    }

    private static WriteableBitmap RenderBitmap(bool[,] modules)
    {
        var pixelSize = (Size + QuietZoneModules * 2) * ModuleScale;
        var bitmap = new WriteableBitmap(
            new PixelSize(pixelSize, pixelSize),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        var pixels = new uint[pixelSize * pixelSize];
        Array.Fill(pixels, 0xFFFFFFFFu);

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!modules[y, x])
                    continue;

                var startX = (x + QuietZoneModules) * ModuleScale;
                var startY = (y + QuietZoneModules) * ModuleScale;
                for (var py = 0; py < ModuleScale; py++)
                {
                    var row = (startY + py) * pixelSize;
                    for (var px = 0; px < ModuleScale; px++)
                        pixels[row + startX + px] = 0xFF111111u;
                }
            }
        }

        unsafe
        {
            using var locked = bitmap.Lock();
            fixed (uint* src = pixels)
            {
                Buffer.MemoryCopy(
                    src,
                    (void*)locked.Address,
                    (long)locked.RowBytes * pixelSize,
                    (long)pixels.Length * sizeof(uint));
            }
        }

        return bitmap;
    }

    private static byte[] ComputeEcc(byte[] data, int degree)
    {
        var generator = BuildGeneratorPolynomial(degree);
        var result = new byte[degree];

        foreach (var value in data)
        {
            var factor = (byte)(value ^ result[0]);
            Array.Copy(result, 1, result, 0, degree - 1);
            result[degree - 1] = 0;
            for (var i = 0; i < degree; i++)
                result[i] ^= Multiply(generator[i + 1], factor);
        }

        return result;
    }

    private static byte[] BuildGeneratorPolynomial(int degree)
    {
        var polynomial = new byte[] { 1 };
        for (var i = 0; i < degree; i++)
        {
            var next = new byte[polynomial.Length + 1];
            for (var j = 0; j < polynomial.Length; j++)
            {
                next[j] ^= polynomial[j];
                next[j + 1] ^= Multiply(polynomial[j], ExpTable[i]);
            }

            polynomial = next;
        }

        return polynomial;
    }

    private static byte Multiply(byte x, byte y)
    {
        if (x == 0 || y == 0)
            return 0;

        return ExpTable[(LogTable[x] + LogTable[y]) % 255];
    }

    private static byte[] BuildExpTable()
    {
        var table = new byte[255];
        var value = 1;
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)value;
            value <<= 1;
            if (value >= 0x100)
                value ^= 0x11D;
        }

        return table;
    }

    private static byte[] BuildLogTable()
    {
        var table = new byte[256];
        for (var i = 0; i < ExpTable.Length; i++)
            table[ExpTable[i]] = (byte)i;

        return table;
    }

    private static void SetFunctionModule(bool[,] modules, bool[,] function, int x, int y, bool dark)
    {
        modules[y, x] = dark;
        function[y, x] = true;
    }

    private static void AppendBits(List<bool> bits, int value, int length)
    {
        for (var i = length - 1; i >= 0; i--)
            bits.Add(((value >> i) & 1) != 0);
    }

    private static int GetAlphanumericValue(char c)
    {
        var index = AlphanumericCharset.IndexOf(c);
        if (index < 0)
            throw new InvalidOperationException($"Unsupported QR character: {c}");

        return index;
    }

    private static bool GetBit(int value, int bitIndex) => ((value >> bitIndex) & 1) != 0;
}
