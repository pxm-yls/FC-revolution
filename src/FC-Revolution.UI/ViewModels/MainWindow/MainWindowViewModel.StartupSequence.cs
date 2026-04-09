using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    public Task OnHostWindowOpenedAsync()
    {
        if (_isWindowOpened)
            return Task.CompletedTask;

        _isWindowOpened = true;
        LogStartup("host window opened; scheduling deferred startup sequence");
        _ = RunStartupSequenceAsync();
        return Task.CompletedTask;
    }

    private async Task RunStartupSequenceAsync()
    {
        try
        {
            await Task.Yield();
            IsStartupProgressVisible = true;

            if (!_hasLoadedStartupContent)
            {
                UpdateStartupStep(
                    "主界面已显示，正在加载游戏列表。",
                    gameListStatus: "加载中",
                    previewStatus: "等待游戏列表完成",
                    lanStatus: IsLanArcadeEnabled ? "等待预览阶段完成" : "已跳过（已关闭）",
                    isVisible: true);
                var gameListWatch = Stopwatch.StartNew();
                LogStartup("startup stage begin: loading game list");
                RefreshRomLibrary();
                _hasLoadedStartupContent = true;
                RefreshStartupDiagnosticsSnapshot();
                UpdateStartupStep(
                    _romLibrary.Count == 0
                        ? "游戏列表加载完成，当前未找到可用游戏。"
                        : $"游戏列表加载完成，共 {_romLibrary.Count} 个游戏。",
                    gameListStatus: _romLibrary.Count == 0 ? "完成（空列表）" : $"完成（{_romLibrary.Count} 个）",
                    previewStatus: "准备开始",
                    lanStatus: IsLanArcadeEnabled ? "等待预览阶段完成" : "已跳过（已关闭）");
                LogStartup($"startup stage complete: game list in {gameListWatch.ElapsedMilliseconds} ms");
                await Task.Yield();
            }
            else
            {
                UpdateStartupStep(
                    "主界面已显示，继续执行剩余启动步骤。",
                    gameListStatus: _romLibrary.Count == 0 ? "已就绪（空列表）" : $"已就绪（{_romLibrary.Count} 个）",
                    previewStatus: "准备开始",
                    lanStatus: IsLanArcadeEnabled ? "等待预览阶段完成" : "已跳过（已关闭）",
                    isVisible: true);
            }

            var warmupStartState = _previewStartupController.BuildWarmupStartState(CurrentRom?.DisplayName);
            UpdateStartupStep(
                warmupStartState.CurrentStep,
                previewStatus: warmupStartState.PreviewStatus);
            var previewWatch = Stopwatch.StartNew();
            LogStartup("startup stage begin: warming preview frames");
            _canWarmPreviews = true;
            await WarmPreviewFramesAsync(_romLibrary.ToList(), CurrentRom);
            var warmedPreviewCount = _romLibrary.Count(item => item.HasLoadedPreview);
            var warmupCompletionState = _previewStartupController.BuildWarmupCompletionState(warmedPreviewCount, IsLanArcadeEnabled);
            UpdateStartupStep(
                warmupCompletionState.CurrentStep,
                previewStatus: warmupCompletionState.PreviewStatus,
                lanStatus: warmupCompletionState.LanStatus);
            LogStartup($"startup stage complete: preview warmup in {previewWatch.ElapsedMilliseconds} ms; warmed={warmedPreviewCount}");
            await Task.Yield();

            if (!IsLanArcadeEnabled)
            {
                UpdateStartupStep(
                    "启动完成，局域网后台已关闭。",
                    lanStatus: "已跳过（已关闭）");
                LogStartup("startup stage skipped: LAN arcade disabled");
                await HideStartupProgressAsync();
                return;
            }

            UpdateStartupStep(
                "正在检测局域网功能并启动后台服务。",
                lanStatus: "启动中");
            var lanWatch = Stopwatch.StartNew();
            LogStartup("startup stage begin: LAN firewall probe");
            RefreshLanFirewallStatus();
            LogStartup("startup stage continue: applying LAN arcade server state");
            await ApplyLanArcadeServerStateAsync();

            if (_lanArcadeService.IsRunning)
            {
                UpdateStartupStep(
                    "启动完成，局域网后台已就绪。",
                    lanStatus: $"完成（端口 {LanArcadePort}）");
                LogStartup($"startup stage complete: LAN ready in {lanWatch.ElapsedMilliseconds} ms");
                await HideStartupProgressAsync();
            }
            else
            {
                UpdateStartupStep(
                    "主界面已就绪，但局域网后台启动失败，请检查消息提醒。",
                    lanStatus: "失败");
                LogStartup($"startup stage complete with LAN failure in {lanWatch.ElapsedMilliseconds} ms");
            }
        }
        catch (Exception ex)
        {
            UpdateStartupStep(
                $"启动失败: {ex.Message}",
                lanStatus: "失败",
                isVisible: true);
            LogStartup($"startup initialization failed: {ex}");
            StatusText = $"启动初始化失败: {ex.Message}";
            PreviewStatusText = "初始化未完成，请检查 ROM 目录和局域网服务设置。";
        }
    }

    private void EnsureStartupContentLoaded()
    {
        if (_hasLoadedStartupContent)
        {
            LogStartup("startup content already loaded; skipping");
            return;
        }

        var watch = Stopwatch.StartNew();
        LogStartup("startup content load begin");
        RefreshLanFirewallStatus();
        LogStartup("refreshing ROM library");
        RefreshRomLibrary();
        _canWarmPreviews = true;
        _hasLoadedStartupContent = true;
        RefreshStartupDiagnosticsSnapshot();
        LogStartup($"startup content load complete in {watch.ElapsedMilliseconds} ms");
    }
}
