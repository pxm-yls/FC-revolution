using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;
using System.Collections.ObjectModel;
using System.Threading;

namespace FCRevolution.Backend.Hosting;

public sealed class BackendRuntimeState
{
    private RuntimeSnapshot _snapshot = RuntimeSnapshot.Empty;

    public IReadOnlyList<RomSummaryDto> Roms => Volatile.Read(ref _snapshot).Roms;
    public IReadOnlyList<GameSessionSummaryDto> Sessions => Volatile.Read(ref _snapshot).Sessions;

    public void ReplaceRoms(IEnumerable<RomSummaryDto> roms)
    {
        ArgumentNullException.ThrowIfNull(roms);
        var romSnapshot = roms.ToArray();

        while (true)
        {
            var current = Volatile.Read(ref _snapshot);
            var next = current.WithRoms(romSnapshot);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, next, current), current))
                return;
        }
    }

    public void ReplaceSessions(IEnumerable<GameSessionSummaryDto> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var sessionSnapshot = sessions.ToArray();

        while (true)
        {
            var current = Volatile.Read(ref _snapshot);
            var next = current.WithSessions(sessionSnapshot);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, next, current), current))
                return;
        }
    }

    private sealed class RuntimeSnapshot
    {
        internal static RuntimeSnapshot Empty { get; } = new([], []);

        private RuntimeSnapshot(RomSummaryDto[] roms, GameSessionSummaryDto[] sessions)
        {
            Roms = new ReadOnlyCollection<RomSummaryDto>(roms);
            Sessions = new ReadOnlyCollection<GameSessionSummaryDto>(sessions);
        }

        internal IReadOnlyList<RomSummaryDto> Roms { get; }

        internal IReadOnlyList<GameSessionSummaryDto> Sessions { get; }

        internal RuntimeSnapshot WithRoms(RomSummaryDto[] roms) =>
            new([..roms], [..Sessions]);

        internal RuntimeSnapshot WithSessions(GameSessionSummaryDto[] sessions) =>
            new([..Roms], [..sessions]);
    }
}
