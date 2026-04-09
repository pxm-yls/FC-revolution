using FCRevolution.Core;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;
using FCRevolution.Rendering.Diagnostics;

namespace FC_Revolution.Rendering.Tests;

public sealed class RenderRegressionTests
{
    [SkippableTheory]
    [InlineData("Super Mario Bros", 90, 0.01f)]
    [InlineData("冒险岛3", 90, 0.01f)]
    [InlineData("忍者神龟2", 120, 0.01f)]
    public void LayeredRenderer_MatchesPpuFrameBuffer_WithinTolerance(
        string romNameFragment,
        int frameIndex,
        float maxDiffRatio)
    {
        string? romPath = FindRomPath(romNameFragment);
        Skip.If(string.IsNullOrWhiteSpace(romPath), $"ROM containing '{romNameFragment}' not found");

        var nes = new NesConsole();
        nes.LoadRom(romPath!);

        var extractor = new RenderDataExtractor();
        FrameMetadata? previous = null;
        for (int i = 0; i < frameIndex; i++)
            nes.RunFrame();

        FrameMetadata metadata = extractor.Extract(RenderSnapshotTestAdapter.FromPpu(nes.Ppu.CaptureRenderStateSnapshot()), previous);
        uint[] reconstructed = ReferenceFrameRenderer.Render(metadata);
        float diff = PixelDiff.Compare(nes.Ppu.FrameBuffer, reconstructed);

        Assert.True(diff <= maxDiffRatio, $"{romNameFragment} frame {frameIndex}: diff {diff:P2} exceeds {maxDiffRatio:P2}");
    }

    private static string? FindRomPath(string romNameFragment)
    {
        string repoRoot = GetRepositoryRoot();
        string romRoot = Path.Combine(repoRoot, "src", "FC-Revolution.UI", "bin");
        if (!Directory.Exists(romRoot))
            return null;

        return Directory.EnumerateFiles(romRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".nes", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(path => Path.GetFileName(path).Contains(romNameFragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var directory = new DirectoryInfo(current);
            if (directory.Exists && File.Exists(Path.Combine(directory.FullName, "FC-Revolution.slnx")))
                return directory.FullName;

            if (directory.Parent == null)
                break;

            current = directory.Parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
