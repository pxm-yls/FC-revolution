using System;
using System.Collections.ObjectModel;
using System.IO;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSaveStateWorkflowControllerTests : IDisposable
{
    private readonly string _quickSavePath = Path.Combine(Path.GetTempPath(), $"game-window-save-state-{Guid.NewGuid():N}.fcs");

    [Fact]
    public void QuickSave_WritesSerializedStateFile()
    {
        var expectedState = new CoreStateBlob
        {
            Format = "core/test",
            Data = [0x01, 0x02, 0x03],
            Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["slot"] = "quick"
            })
        };
        var controller = new GameWindowSaveStateWorkflowController(
            _quickSavePath,
            () => expectedState,
            _ => throw new InvalidOperationException("restore should not be called"));

        var result = controller.QuickSave();

        Assert.True(File.Exists(_quickSavePath));
        Assert.Equal("已快速存档", result.ToastText);
        Assert.Contains("快速存档成功", result.StatusText);

        var persistedState = CoreStateBlobFileCodec.Deserialize(File.ReadAllBytes(_quickSavePath), "legacy/nes-state");
        Assert.Equal(expectedState.Format, persistedState.Format);
        Assert.Equal(expectedState.Data, persistedState.Data);
        Assert.Equal(expectedState.Metadata["slot"], persistedState.Metadata["slot"]);
    }

    [Fact]
    public void QuickLoad_WithoutSaveFile_ReturnsMissingSaveMessage()
    {
        var restoreCalls = 0;
        var controller = new GameWindowSaveStateWorkflowController(
            _quickSavePath,
            () => throw new InvalidOperationException("capture should not be called"),
            _ => restoreCalls++);

        var result = controller.QuickLoad();

        Assert.False(result.StateRestored);
        Assert.Equal("没有可用快速存档", result.ToastText);
        Assert.Equal("当前游戏还没有快速存档", result.StatusText);
        Assert.Equal(0, restoreCalls);
    }

    [Fact]
    public void QuickLoad_WithSerializedState_RestoresCapturedState()
    {
        var expectedState = new CoreStateBlob
        {
            Format = "core/test",
            Data = [0x0A, 0x0B],
            Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["mapper"] = "000"
            })
        };
        File.WriteAllBytes(_quickSavePath, CoreStateBlobFileCodec.Serialize(expectedState));

        CoreStateBlob? restoredState = null;
        var controller = new GameWindowSaveStateWorkflowController(
            _quickSavePath,
            () => throw new InvalidOperationException("capture should not be called"),
            state => restoredState = state);

        var result = controller.QuickLoad();

        Assert.True(result.StateRestored);
        Assert.Equal("已快速读档", result.ToastText);
        Assert.Contains("快速读档成功", result.StatusText);
        Assert.NotNull(restoredState);
        Assert.Equal(expectedState.Format, restoredState!.Format);
        Assert.Equal(expectedState.Data, restoredState.Data);
        Assert.Equal(expectedState.Metadata["mapper"], restoredState.Metadata["mapper"]);
    }

    [Fact]
    public void QuickLoad_WithSerializedState_InvokesPostRestoreCallback()
    {
        var expectedState = new CoreStateBlob
        {
            Format = "core/test",
            Data = [0x0A, 0x0B]
        };
        File.WriteAllBytes(_quickSavePath, CoreStateBlobFileCodec.Serialize(expectedState));

        var callbackCalls = 0;
        var controller = new GameWindowSaveStateWorkflowController(
            _quickSavePath,
            () => throw new InvalidOperationException("capture should not be called"),
            _ => { },
            () => callbackCalls++);

        var result = controller.QuickLoad();

        Assert.True(result.StateRestored);
        Assert.Equal(1, callbackCalls);
    }

    public void Dispose()
    {
        if (File.Exists(_quickSavePath))
            File.Delete(_quickSavePath);
    }
}
