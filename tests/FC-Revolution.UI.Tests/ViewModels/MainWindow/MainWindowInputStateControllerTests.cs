using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputStateControllerTests
{
    [Fact]
    public void ResolveExtraInputBinding_ParsesKeyAndButtons_AndFallsBackToTurboKind()
    {
        var controller = new MainWindowInputStateController();
        var profile = new ExtraInputBindingProfile
        {
            Player = 1,
            Kind = "UnknownKind",
            Key = Key.Q.ToString(),
            Buttons = [NesButton.A.ToString(), "Invalid", NesButton.A.ToString(), NesButton.B.ToString()]
        };

        var binding = controller.ResolveExtraInputBinding(profile);

        Assert.NotNull(binding);
        Assert.Equal(1, binding.Player);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal(ExtraInputBindingKind.Turbo, binding.Kind);
        Assert.Equal(NesInputTestAdapter.ActionIds(NesButton.A, NesButton.B), binding.ActionIds);
    }

    [Fact]
    public void ResolveExtraInputBinding_ReturnsNull_WhenKeyOrButtonsAreInvalid()
    {
        var controller = new MainWindowInputStateController();

        var invalidKey = controller.ResolveExtraInputBinding(new ExtraInputBindingProfile
        {
            Player = 0,
            Kind = ExtraInputBindingKind.Combo.ToString(),
            Key = "NotAKey",
            Buttons = [NesButton.A.ToString()]
        });
        var invalidButtons = controller.ResolveExtraInputBinding(new ExtraInputBindingProfile
        {
            Player = 0,
            Kind = ExtraInputBindingKind.Combo.ToString(),
            Key = Key.W.ToString(),
            Buttons = ["Nope"]
        });

        Assert.Null(invalidKey);
        Assert.Null(invalidButtons);
    }

    [Fact]
    public void BuildEffectiveHandledKeys_MergesKeyMapAndExtraBindingKeys()
    {
        var controller = new MainWindowInputStateController();
        var effectiveKeyMap = new Dictionary<Key, (int Player, string ActionId)>
        {
            [Key.Z] = (0, NesInputTestAdapter.ActionId(NesButton.A)),
            [Key.X] = (0, NesInputTestAdapter.ActionId(NesButton.B))
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new(0, Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A)),
            new(1, Key.Z, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.Select))
        };

        var keys = controller.BuildEffectiveHandledKeys(effectiveKeyMap, extraBindings);

        Assert.Equal(3, keys.Count);
        Assert.Contains(Key.Z, keys);
        Assert.Contains(Key.X, keys);
        Assert.Contains(Key.Q, keys);
    }

    [Fact]
    public void BuildDesiredMasks_RespectsTurboPulseAndComboBindings()
    {
        var controller = new MainWindowInputStateController();
        var pressedKeys = new HashSet<Key> { Key.Z, Key.Q, Key.W };
        var effectiveKeyMap = new Dictionary<Key, (int Player, string ActionId)>
        {
            [Key.Z] = (0, NesInputTestAdapter.ActionId(NesButton.A)),
            [Key.I] = (1, NesInputTestAdapter.ActionId(NesButton.B))
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new(0, Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.B)),
            new(1, Key.W, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.Select, NesButton.Start))
        };

        var withoutTurboPulse = controller.BuildDesiredMasks(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: false);
        var withTurboPulse = controller.BuildDesiredMasks(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: true);

        Assert.Equal((byte)NesButton.A, withoutTurboPulse.Player1Mask);
        Assert.Equal((byte)((byte)NesButton.Select | (byte)NesButton.Start), withoutTurboPulse.Player2Mask);

        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), withTurboPulse.Player1Mask);
        Assert.Equal((byte)((byte)NesButton.Select | (byte)NesButton.Start), withTurboPulse.Player2Mask);
    }

    [Fact]
    public void BuildMaskTransitions_ReturnsOnlyButtonsWithStateChanges()
    {
        var controller = new MainWindowInputStateController();
        var transitions = controller.BuildMaskTransitions(
            desiredMask: (byte)((byte)NesButton.A | (byte)NesButton.Start),
            currentMask: (byte)((byte)NesButton.A | (byte)NesButton.Select),
            controllerActionIds: NesInputTestAdapter.ActionIds(NesButton.A, NesButton.B, NesButton.Select, NesButton.Start));

        Assert.Equal(2, transitions.Count);
        Assert.Contains(transitions, t => t.ActionId == NesInputTestAdapter.ActionId(NesButton.Select) && !t.DesiredPressed);
        Assert.Contains(transitions, t => t.ActionId == NesInputTestAdapter.ActionId(NesButton.Start) && t.DesiredPressed);
    }

    [Fact]
    public void BuildTurboPulseDecision_CoversNoTurboAndActiveTurboBranches()
    {
        var controller = new MainWindowInputStateController();
        var noTurboBindings = new List<ResolvedExtraInputBinding>
        {
            new(0, Key.W, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.A))
        };
        var activeTurboBindings = new List<ResolvedExtraInputBinding>
        {
            new(0, Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A))
        };

        var noTurboInactive = controller.BuildTurboPulseDecision(
            turboPulseActive: false,
            pressedKeys: new HashSet<Key> { Key.W },
            extraBindings: noTurboBindings);
        var noTurboWhileActive = controller.BuildTurboPulseDecision(
            turboPulseActive: true,
            pressedKeys: new HashSet<Key> { Key.W },
            extraBindings: noTurboBindings);
        var activeTurbo = controller.BuildTurboPulseDecision(
            turboPulseActive: false,
            pressedKeys: new HashSet<Key> { Key.Q },
            extraBindings: activeTurboBindings);

        Assert.False(noTurboInactive.NextTurboPulseActive);
        Assert.False(noTurboInactive.ShouldRefreshActiveInputState);

        Assert.False(noTurboWhileActive.NextTurboPulseActive);
        Assert.True(noTurboWhileActive.ShouldRefreshActiveInputState);

        Assert.True(activeTurbo.NextTurboPulseActive);
        Assert.True(activeTurbo.ShouldRefreshActiveInputState);
    }
}
