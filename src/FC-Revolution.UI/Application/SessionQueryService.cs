using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using FCRevolution.Contracts.Sessions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class SessionQueryService
{
    private readonly IGameSessionService _gameSessionService;

    public SessionQueryService(IGameSessionService gameSessionService)
    {
        _gameSessionService = gameSessionService;
    }

    public IReadOnlyList<GameSessionSummaryDto> GetSessionSummaries() =>
        _gameSessionService.Sessions
            .Select(session => new GameSessionSummaryDto(
                session.SessionId,
                session.DisplayName,
                session.RomPath,
                string.IsNullOrWhiteSpace(session.ViewModel.RemoteControlStatusText) ? "当前本地控制" : session.ViewModel.RemoteControlStatusText,
                MapSource(session.ViewModel.Player1ControlSource),
                MapSource(session.ViewModel.Player2ControlSource)))
            .ToList();

    public byte[]? GetSessionPreview(Guid sessionId)
    {
        var session = _gameSessionService.FindSession(sessionId);
        return session?.SnapshotBitmap == null ? null : EncodeBitmap(session.SnapshotBitmap);
    }

    private static PlayerControlSourceDto MapSource(GamePlayerControlSource source) =>
        source == GamePlayerControlSource.Remote ? PlayerControlSourceDto.Remote : PlayerControlSourceDto.Local;

    private static byte[] EncodeBitmap(WriteableBitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
