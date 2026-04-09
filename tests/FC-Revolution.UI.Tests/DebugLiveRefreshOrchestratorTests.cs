using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugLiveRefreshOrchestratorTests
{
    [Fact]
    public void HasActiveRefreshSections_ReturnsTrueWhenAnySectionEnabled()
    {
        var noneEnabled = DebugLiveRefreshOrchestrator.HasActiveRefreshSections(
            showRegisters: false,
            showPpu: false,
            showDisasm: false,
            showStack: false,
            showZeroPage: false,
            showMemoryPage: false);
        var oneEnabled = DebugLiveRefreshOrchestrator.HasActiveRefreshSections(
            showRegisters: false,
            showPpu: false,
            showDisasm: false,
            showStack: true,
            showZeroPage: false,
            showMemoryPage: false);

        Assert.False(noneEnabled);
        Assert.True(oneEnabled);
    }

    [Theory]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, true, true, false)]
    [InlineData(false, false, false, false, false)]
    [InlineData(false, false, false, true, true)]
    public void ShouldRunLiveRefreshTick_EvaluatesTickGating(
        bool isDisposed,
        bool hasSessionFailure,
        bool isMemoryCellEditing,
        bool hasActiveRefreshSections,
        bool expected)
    {
        var shouldRun = DebugLiveRefreshOrchestrator.ShouldRunLiveRefreshTick(
            isDisposed,
            hasSessionFailure,
            isMemoryCellEditing,
            hasActiveRefreshSections);

        Assert.Equal(expected, shouldRun);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void ShouldScheduleRefresh_EvaluatesSchedulingGating(
        bool refreshScheduled,
        bool isDisposed,
        bool hasSessionFailure,
        bool expected)
    {
        var shouldSchedule = DebugLiveRefreshOrchestrator.ShouldScheduleRefresh(
            refreshScheduled,
            isDisposed,
            hasSessionFailure);

        Assert.Equal(expected, shouldSchedule);
    }

    [Fact]
    public void BuildCapturePlan_UsesRequestCaptureFlags()
    {
        var emptyRequest = new DebugRefreshRequest();
        var stateRequest = new DebugRefreshRequest
        {
            CaptureDisasm = true
        };

        var emptyPlan = DebugLiveRefreshOrchestrator.BuildCapturePlan(emptyRequest);
        var statePlan = DebugLiveRefreshOrchestrator.BuildCapturePlan(stateRequest);

        Assert.False(emptyPlan.RequiresAnyCapture);
        Assert.False(emptyPlan.RequiresState);
        Assert.True(statePlan.RequiresAnyCapture);
        Assert.True(statePlan.RequiresState);
    }

    [Fact]
    public void BuildAddressPlan_ComputesExpectedMemoryAndDisasmStarts()
    {
        var request = new DebugRefreshRequest
        {
            MemoryPageIndex = 2,
            StackPageIndex = 1,
            ZeroPageSliceIndex = 3,
            DisasmPageIndex = -2
        };

        var plan = DebugLiveRefreshOrchestrator.BuildAddressPlan(
            request,
            programCounter: 0x8000,
            memoryPageSize: DebugViewModel.MemoryPageSize,
            stackPageSize: DebugViewModel.StackPageSize,
            zeroPageSliceSize: DebugViewModel.ZeroPageSliceSize,
            disasmPageSize: DebugViewModel.DisasmPageSize);

        Assert.Equal((ushort)0x0100, plan.MemoryPageStart);
        Assert.Equal((ushort)0x0140, plan.StackPageStart);
        Assert.Equal((ushort)0x00C0, plan.ZeroPageStart);
        Assert.Equal((ushort)0x7FE8, plan.DisasmStart);
    }

    [Fact]
    public void BuildApplyPlan_ProjectsSectionFlags()
    {
        var plan = DebugLiveRefreshOrchestrator.BuildApplyPlan(
            showRegisters: true,
            showPpu: false,
            showMemoryPage: true,
            showStack: false,
            showZeroPage: true,
            showDisasm: false);

        Assert.True(plan.ApplyRegisters);
        Assert.False(plan.ApplyPpu);
        Assert.True(plan.ApplyMemoryPage);
        Assert.False(plan.ApplyStack);
        Assert.True(plan.ApplyZeroPage);
        Assert.False(plan.ApplyDisasm);
    }

    [Fact]
    public void LocatorHelpers_ComputeStartAndScheduleFallbackDecision()
    {
        var start = DebugLiveRefreshOrchestrator.ResolveMemoryPageStart(memoryPageIndex: 3, memoryPageSize: DebugViewModel.MemoryPageSize);
        var shouldScheduleWhenUpdated = DebugLiveRefreshOrchestrator.ShouldScheduleRefreshAfterLocatorUpdate(updatedInPlace: true);
        var shouldScheduleWhenMissed = DebugLiveRefreshOrchestrator.ShouldScheduleRefreshAfterLocatorUpdate(updatedInPlace: false);

        Assert.Equal((ushort)0x0180, start);
        Assert.False(shouldScheduleWhenUpdated);
        Assert.True(shouldScheduleWhenMissed);
    }
}
