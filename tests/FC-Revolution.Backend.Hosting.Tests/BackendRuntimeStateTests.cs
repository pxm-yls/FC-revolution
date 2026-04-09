using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendRuntimeStateTests
{
    [Fact]
    public void Replace_Snapshot_Keeps_Previous_Readers_Stable()
    {
        var state = new BackendRuntimeState();
        state.ReplaceRoms([CreateRom(1)]);
        state.ReplaceSessions([CreateSession(1)]);

        var romSnapshot = state.Roms;
        var sessionSnapshot = state.Sessions;

        state.ReplaceRoms([CreateRom(2), CreateRom(3)]);
        state.ReplaceSessions([CreateSession(2), CreateSession(3)]);

        Assert.Single(romSnapshot);
        Assert.Equal("Rom-1", romSnapshot[0].DisplayName);
        Assert.Single(sessionSnapshot);
        Assert.Equal("Session-1", sessionSnapshot[0].DisplayName);

        Assert.Equal(2, state.Roms.Count);
        Assert.Equal("Rom-2", state.Roms[0].DisplayName);
        Assert.Equal(2, state.Sessions.Count);
        Assert.Equal("Session-2", state.Sessions[0].DisplayName);
    }

    [Fact]
    public async Task Concurrent_Reads_Observe_Stable_Snapshots_During_Replacements()
    {
        var state = new BackendRuntimeState();
        state.ReplaceRoms([CreateRom(0)]);
        state.ReplaceSessions([CreateSession(0)]);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var writer = Task.Run(() =>
        {
            var i = 1;
            while (!cts.Token.IsCancellationRequested)
            {
                state.ReplaceRoms([CreateRom(i)]);
                state.ReplaceSessions([CreateSession(i)]);
                i++;
            }
        }, cts.Token);

        var readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var roms = state.Roms;
                    var sessions = state.Sessions;
                    Assert.Single(roms);
                    Assert.Single(sessions);
                    Assert.Equal($"/roms/{ExtractIndex(roms[0].DisplayName)}.nes", roms[0].Path);
                    Assert.Equal($"Session-{ExtractIndex(sessions[0].DisplayName)}", sessions[0].DisplayName);
                }
            }, cts.Token))
            .ToArray();

        await Task.WhenAll(readers);
        await writer;
    }

    private static RomSummaryDto CreateRom(int index) =>
        new($"Rom-{index}", $"/roms/{index}.nes", true, HasPreview: index % 2 == 0);

    private static GameSessionSummaryDto CreateSession(int index) =>
        new(Guid.NewGuid(), $"Session-{index}", $"/roms/{index}.nes", "control", PlayerControlSourceDto.Local, PlayerControlSourceDto.Remote);

    private static int ExtractIndex(string value) =>
        int.Parse(value[value.IndexOf('-')..].TrimStart('-'));
}
