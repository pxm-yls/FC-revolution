using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowProfileTrustHandler
{
    private readonly Func<bool> _isProfileTrustInitialized;
    private readonly Action _markProfileTrustInitialized;
    private readonly string _romPath;
    private readonly Func<string, RomConfigLoadResult> _loadValidated;
    private readonly Func<Window, string, string, Task<bool>> _showTrustDialog;
    private readonly Action<string> _trustCurrentMachine;
    private readonly Action<RomConfigLoadResult> _applySavedMemoryProfile;
    private readonly Action<string> _setStatus;

    public GameWindowProfileTrustHandler(
        Func<bool> isProfileTrustInitialized,
        Action markProfileTrustInitialized,
        string romPath,
        Func<string, RomConfigLoadResult> loadValidated,
        Func<Window, string, string, Task<bool>> showTrustDialog,
        Action<string> trustCurrentMachine,
        Action<RomConfigLoadResult> applySavedMemoryProfile,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(isProfileTrustInitialized);
        ArgumentNullException.ThrowIfNull(markProfileTrustInitialized);
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
        ArgumentNullException.ThrowIfNull(loadValidated);
        ArgumentNullException.ThrowIfNull(showTrustDialog);
        ArgumentNullException.ThrowIfNull(trustCurrentMachine);
        ArgumentNullException.ThrowIfNull(applySavedMemoryProfile);
        ArgumentNullException.ThrowIfNull(setStatus);

        _isProfileTrustInitialized = isProfileTrustInitialized;
        _markProfileTrustInitialized = markProfileTrustInitialized;
        _romPath = romPath;
        _loadValidated = loadValidated;
        _showTrustDialog = showTrustDialog;
        _trustCurrentMachine = trustCurrentMachine;
        _applySavedMemoryProfile = applySavedMemoryProfile;
        _setStatus = setStatus;
    }

    public async Task EnsureAsync(Window owner)
    {
        if (_isProfileTrustInitialized())
            return;

        _markProfileTrustInitialized();
        var loadResult = _loadValidated(_romPath);

        if (loadResult.HasProfileKindMismatch)
        {
            _setStatus($"运行中: {Path.GetFileName(_romPath)} | 警告：.fcr 类型不匹配");
            return;
        }

        if (loadResult.IsForeignMachineProfile)
        {
            var confirmed = await _showTrustDialog(
                owner,
                "检测到外部 .fcr",
                "当前游戏使用的 .fcr 来自其他设备，可能包含不适合本机的独立配置或内存修改。继续信任后，会将该 .fcr 重签名为当前设备。");

            if (!confirmed)
            {
                _setStatus($"运行中: {Path.GetFileName(_romPath)} | 已取消使用外部 .fcr");
                return;
            }

            _trustCurrentMachine(_romPath);
            loadResult = _loadValidated(_romPath);
        }

        _applySavedMemoryProfile(loadResult);
    }
}
