using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputStateControllerTests
{
    [Fact]
    public void ResolveExtraInputBinding_ParsesKeyAndButtons_AndFallsBackToTurboKind()
    {
        var controller = new MainWindowInputStateController();
        var schema = CoreInputBindingSchema.CreateFallback();
        var profile = new ExtraInputBindingProfile
        {
            PortId = "p1",
            Kind = "UnknownKind",
            Key = Key.Q.ToString(),
            Buttons = [NesButton.A.ToString(), "Invalid", NesButton.A.ToString(), NesButton.B.ToString()]
        };

        var binding = controller.ResolveExtraInputBinding(profile, schema);

        Assert.NotNull(binding);
        Assert.Equal("p1", binding.PortId);
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
    public void ResolveExtraInputBinding_UsesSchemaPlayerIndexFallback_WhenPortIdMissing()
    {
        var controller = new MainWindowInputStateController();
        var binding = controller.ResolveExtraInputBinding(
            new ExtraInputBindingProfile
            {
                Player = 7,
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.Q.ToString(),
                Buttons = ["shield"]
            },
            CoreInputBindingSchema.Create(new HighIndexInputSchema()));

        Assert.NotNull(binding);
        Assert.Equal("pad-east", binding!.PortId);
        Assert.Equal("shield", Assert.Single(binding.ActionIds));
    }

    [Fact]
    public void BuildEffectiveHandledKeys_MergesKeyMapAndExtraBindingKeys()
    {
        var controller = new MainWindowInputStateController();
        var effectiveKeyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", NesInputTestAdapter.ActionId(NesButton.A)),
            [Key.X] = ("p1", NesInputTestAdapter.ActionId(NesButton.B))
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A)),
            new("p2", Key.Z, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.Select))
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
        var schema = CoreInputBindingSchema.CreateFallback();
        var pressedKeys = new HashSet<Key> { Key.Z, Key.Q, Key.W };
        var effectiveKeyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", NesInputTestAdapter.ActionId(NesButton.A)),
            [Key.I] = ("p2", NesInputTestAdapter.ActionId(NesButton.B))
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.B)),
            new("p2", Key.W, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.Select, NesButton.Start))
        };

        var withoutTurboPulse = controller.BuildDesiredMasks(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: false,
            schema);
        var withTurboPulse = controller.BuildDesiredMasks(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: true,
            schema);

        Assert.Equal((byte)NesButton.A, withoutTurboPulse.GetMask("p1"));
        Assert.Equal((byte)((byte)NesButton.Select | (byte)NesButton.Start), withoutTurboPulse.GetMask("p2"));

        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), withTurboPulse.GetMask("p1"));
        Assert.Equal((byte)((byte)NesButton.Select | (byte)NesButton.Start), withTurboPulse.GetMask("p2"));
    }

    [Fact]
    public void BuildMaskTransitions_ReturnsOnlyButtonsWithStateChanges()
    {
        var controller = new MainWindowInputStateController();
        var desiredActions = new HashSet<string>
        {
            NesInputTestAdapter.ActionId(NesButton.Start),
            NesInputTestAdapter.ActionId(NesButton.A)
        };
        var currentActions = new HashSet<string>
        {
            NesInputTestAdapter.ActionId(NesButton.A),
            NesInputTestAdapter.ActionId(NesButton.Select)
        };
        var transitions = controller.BuildActionTransitions(
            desiredActions,
            currentActions,
            NesInputTestAdapter.ActionIds(NesButton.A, NesButton.B, NesButton.Select, NesButton.Start));

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
            new("p1", Key.W, ExtraInputBindingKind.Combo, NesInputTestAdapter.ActionIds(NesButton.A))
        };
        var activeTurboBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, NesInputTestAdapter.ActionIds(NesButton.A))
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

    private sealed class HighIndexInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } =
        [
            new("pad-west", "West Pad", 4),
            new("pad-east", "East Pad", 7)
        ];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } =
        [
            new("fire", "Fire", "pad-west", InputValueKind.Digital),
            new("shield", "Shield", "pad-east", InputValueKind.Digital)
        ];
    }
}
