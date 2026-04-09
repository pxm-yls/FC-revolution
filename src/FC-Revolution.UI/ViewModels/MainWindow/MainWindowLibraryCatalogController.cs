using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct MainWindowLibraryCatalogSnapshot(
    IReadOnlyList<RomLibraryItem> AllItems,
    int RomFileCount);

internal readonly record struct MainWindowLibraryVisibleSelection(
    IReadOnlyList<RomLibraryItem> VisibleItems,
    RomLibraryItem? PreferredRom);

internal readonly record struct MainWindowLibraryEmptyState(
    string CurrentRomName,
    string CurrentRomPathText,
    string PreviewStatusText,
    string StatusText);

internal sealed class MainWindowLibraryCatalogController
{
    private readonly MainWindowCatalogLayoutController _catalogLayoutController;

    public MainWindowLibraryCatalogController(MainWindowCatalogLayoutController catalogLayoutController)
    {
        _catalogLayoutController = catalogLayoutController;
    }

    public MainWindowLibraryCatalogSnapshot CaptureCatalogSnapshot(
        string romDirectory,
        Func<string, string> resolvePreviewPlaybackPath,
        bool isInitialStartupRefresh,
        Action<int>? onRomFilesScanned = null,
        Action<int, int, string>? onRomProcessing = null,
        Action<string>? onRomDiscovered = null)
    {
        var romFiles = Directory.EnumerateFiles(romDirectory, "*.nes", SearchOption.AllDirectories).ToList();
        onRomFilesScanned?.Invoke(romFiles.Count);

        var romItems = new List<RomLibraryItem>(romFiles.Count);
        for (var index = 0; index < romFiles.Count; index++)
        {
            var romPath = romFiles[index];
            if (isInitialStartupRefresh || index < 5 || index == romFiles.Count - 1)
                onRomProcessing?.Invoke(index + 1, romFiles.Count, romPath);

            var previewPath = resolvePreviewPlaybackPath(romPath);
            var fileInfo = new FileInfo(romPath);
            var importedAtUtc = fileInfo.CreationTimeUtc == DateTime.MinValue
                ? fileInfo.LastWriteTimeUtc
                : fileInfo.CreationTimeUtc;

            var romProfile = RomConfigProfile.EnsureResourceManifest(romPath);
            var cachedResources = romProfile.Resources;
            var item = new RomLibraryItem(
                Path.GetFileName(romPath),
                romPath,
                previewPath,
                File.Exists(previewPath),
                fileInfo.Length,
                importedAtUtc,
                cachedResources?.PreviewIsAnimated,
                cachedResources?.PreviewIntervalMs ?? 0,
                cachedResources?.PreviewFrameCount ?? 0);

            onRomDiscovered?.Invoke(romPath);
            romItems.Add(item);
        }

        return new MainWindowLibraryCatalogSnapshot(romItems, romFiles.Count);
    }

    public MainWindowLibraryVisibleSelection BuildVisibleSelection(
        IReadOnlyList<RomLibraryItem> allRomLibrary,
        string librarySearchText,
        RomSortField sortField,
        bool sortDescending,
        string? preferredPath,
        string? currentRomPath,
        string? fallbackRomPath,
        Func<string, string, bool> pathsEqual)
    {
        var visibleItems = _catalogLayoutController.BuildVisibleItems(
            allRomLibrary,
            librarySearchText,
            sortField,
            sortDescending);

        var preferredRom = SelectPreferredVisibleRom(
            visibleItems,
            preferredPath,
            currentRomPath,
            fallbackRomPath,
            pathsEqual);

        return new MainWindowLibraryVisibleSelection(visibleItems, preferredRom);
    }

    public RomLibraryItem? SelectPreferredVisibleRom(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        string? preferredPath,
        string? currentRomPath,
        string? fallbackRomPath,
        Func<string, string, bool> pathsEqual)
    {
        if (visibleRomLibrary.Count == 0)
            return null;

        var effectivePreferredPath = preferredPath ?? currentRomPath ?? fallbackRomPath;
        if (string.IsNullOrWhiteSpace(effectivePreferredPath))
            return visibleRomLibrary[0];

        return visibleRomLibrary.FirstOrDefault(item => pathsEqual(item.Path, effectivePreferredPath))
            ?? visibleRomLibrary[0];
    }

    public string BuildLibrarySummary(
        string romDirectory,
        int totalRomCount,
        int visibleRomCount,
        bool hasLibrarySearchText,
        string sortDescription)
    {
        if (totalRomCount == 0)
            return $"当前目录 `{romDirectory}` 下没有找到 ROM";

        return hasLibrarySearchText
            ? $"已扫描 `{romDirectory}`，共 {totalRomCount} 个 ROM，筛选后显示 {visibleRomCount} 个，当前排序 {sortDescription}"
            : $"已扫描 `{romDirectory}`，共 {visibleRomCount} 个 ROM，当前排序 {sortDescription}";
    }

    public MainWindowLibraryEmptyState BuildEmptyLibraryState(
        string romDirectory,
        int totalRomCount,
        string librarySearchText)
    {
        if (totalRomCount == 0)
        {
            return new MainWindowLibraryEmptyState(
                "没有可展示的 ROM",
                $"把 `.nes` 文件放到 `{romDirectory}` 后点击刷新",
                "等待生成预览",
                "未发现 ROM");
        }

        return new MainWindowLibraryEmptyState(
            "没有匹配的 ROM",
            $"搜索“{librarySearchText.Trim()}”没有命中，可尝试标题片段、文件名或清空搜索",
            "当前筛选下没有可用预览",
            $"当前搜索无结果，共 {totalRomCount} 个游戏可用");
    }

    public string BuildNonEmptyStatusText(int visibleRomCount, bool hasLibrarySearchText)
    {
        return hasLibrarySearchText
            ? $"搜索到 {visibleRomCount} 个游戏"
            : $"ROM 库已更新，共 {visibleRomCount} 个游戏";
    }
}
