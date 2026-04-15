using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
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
            Buttons = [FallbackInputTestData.ActionA, "Invalid", FallbackInputTestData.ActionA, FallbackInputTestData.ActionB]
        };

        var binding = controller.ResolveExtraInputBinding(profile, schema);

        Assert.NotNull(binding);
        Assert.Equal("p1", binding.PortId);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal(ExtraInputBindingKind.Turbo, binding.Kind);
        Assert.Equal(FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA, FallbackInputTestData.ActionB), binding.ActionIds);
    }

    [Fact]
    public void ResolveExtraInputBinding_ReturnsNull_WhenKeyOrButtonsAreInvalid()
    {
        var controller = new MainWindowInputStateController();

        var invalidKey = controller.ResolveExtraInputBinding(new ExtraInputBindingProfile
        {
            LegacyPortOrdinal = 0,
            Kind = ExtraInputBindingKind.Combo.ToString(),
            Key = "NotAKey",
            Buttons = [FallbackInputTestData.ActionA]
        });
        var invalidButtons = controller.ResolveExtraInputBinding(new ExtraInputBindingProfile
        {
            LegacyPortOrdinal = 0,
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
                LegacyPortOrdinal = 7,
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
            [Key.Z] = ("p1", FallbackInputTestData.ActionA),
            [Key.X] = ("p1", FallbackInputTestData.ActionB)
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA)),
            new("p2", Key.Z, ExtraInputBindingKind.Combo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionSelect))
        };

        var keys = controller.BuildEffectiveHandledKeys(effectiveKeyMap, extraBindings);

        Assert.Equal(3, keys.Count);
        Assert.Contains(Key.Z, keys);
        Assert.Contains(Key.X, keys);
        Assert.Contains(Key.Q, keys);
    }

    [Fact]
    public void BuildDesiredActions_RespectsTurboPulseAndComboBindings()
    {
        var controller = new MainWindowInputStateController();
        var pressedKeys = new HashSet<Key> { Key.Z, Key.Q, Key.W };
        var effectiveKeyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", FallbackInputTestData.ActionA),
            [Key.I] = ("p2", FallbackInputTestData.ActionB)
        };
        var extraBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionB)),
            new("p2", Key.W, ExtraInputBindingKind.Combo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionSelect, FallbackInputTestData.ActionStart))
        };

        var withoutTurboPulse = controller.BuildDesiredActions(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: false);
        var withTurboPulse = controller.BuildDesiredActions(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive: true);

        AssertActions(withoutTurboPulse.GetActions("p1"), FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA));
        AssertActions(withoutTurboPulse.GetActions("p2"), FallbackInputTestData.ActionIds(FallbackInputTestData.ActionSelect, FallbackInputTestData.ActionStart));

        AssertActions(withTurboPulse.GetActions("p1"), FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA, FallbackInputTestData.ActionB));
        AssertActions(withTurboPulse.GetActions("p2"), FallbackInputTestData.ActionIds(FallbackInputTestData.ActionSelect, FallbackInputTestData.ActionStart));
    }

    [Fact]
    public void BuildMaskTransitions_ReturnsOnlyButtonsWithStateChanges()
    {
        var controller = new MainWindowInputStateController();
        var desiredActions = new HashSet<string>
        {
            FallbackInputTestData.ActionStart,
            FallbackInputTestData.ActionA
        };
        var currentActions = new HashSet<string>
        {
            FallbackInputTestData.ActionA,
            FallbackInputTestData.ActionSelect
        };
        var transitions = controller.BuildActionTransitions(
            desiredActions,
            currentActions,
            FallbackInputTestData.ActionIds(
                FallbackInputTestData.ActionA,
                FallbackInputTestData.ActionB,
                FallbackInputTestData.ActionSelect,
                FallbackInputTestData.ActionStart));

        Assert.Equal(2, transitions.Count);
        Assert.Contains(transitions, t => t.ActionId == FallbackInputTestData.ActionSelect && !t.DesiredPressed);
        Assert.Contains(transitions, t => t.ActionId == FallbackInputTestData.ActionStart && t.DesiredPressed);
    }

    [Fact]
    public void BuildTurboPulseDecision_CoversNoTurboAndActiveTurboBranches()
    {
        var controller = new MainWindowInputStateController();
        var noTurboBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.W, ExtraInputBindingKind.Combo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA))
        };
        var activeTurboBindings = new List<ResolvedExtraInputBinding>
        {
            new("p1", Key.Q, ExtraInputBindingKind.Turbo, FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA))
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

    private static void AssertActions(
        IReadOnlySet<string> actual,
        IReadOnlyCollection<string> expected)
    {
        Assert.Equal(
            expected.OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase),
            actual.OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
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
