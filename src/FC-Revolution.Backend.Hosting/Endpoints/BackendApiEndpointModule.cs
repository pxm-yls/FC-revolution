using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendApiEndpointModule
{
    internal static void Map(WebApplication app, BackendHostOptions options)
    {
        app.MapGet("/api/health", () => Results.Json(new { ok = true, port = options.Port }, BackendJsonDefaults.SerializerOptions));

        app.MapGet("/api/roms", async (IRomCatalogContract contract, CancellationToken cancellationToken) =>
            Results.Json(await contract.GetRomsAsync(cancellationToken), BackendJsonDefaults.SerializerOptions));

        app.MapGet("/api/roms/preview", async (string romPath, IBackendPreviewQueryBridge previewBridge, CancellationToken cancellationToken) =>
        {
            var asset = await previewBridge.GetRomPreviewAssetAsync(romPath, cancellationToken);
            return asset == null
                ? Results.NotFound(new { error = "当前 ROM 暂无预览动画" })
                : Results.File(asset.FilePath, asset.ContentType, enableRangeProcessing: true);
        });

        app.MapGet("/api/sessions", async (IGameSessionContract contract, CancellationToken cancellationToken) =>
            Results.Json(await contract.GetSessionsAsync(cancellationToken), BackendJsonDefaults.SerializerOptions));

        app.MapPost("/api/sessions", async (StartSessionRequest request, IGameSessionContract contract, CancellationToken cancellationToken) =>
        {
            var response = await contract.StartSessionAsync(request, cancellationToken);
            return response == null
                ? Results.NotFound(new { error = "未找到 ROM 或启动失败" })
                : Results.Json(response, BackendJsonDefaults.SerializerOptions);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/close", async (Guid sessionId, IGameSessionContract contract, CancellationToken cancellationToken) =>
        {
            var ok = await contract.CloseSessionAsync(sessionId, cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.NotFound(new { error = "未找到游戏会话" });
        });

        app.MapGet("/api/sessions/{sessionId:guid}/preview", async (Guid sessionId, IBackendPreviewQueryBridge previewBridge, CancellationToken cancellationToken) =>
        {
            var pngBytes = await previewBridge.GetSessionPreviewAsync(sessionId, cancellationToken);
            return pngBytes == null
                ? Results.NotFound(new { error = "当前会话暂无可用画面" })
                : Results.File(pngBytes, "image/png", enableRangeProcessing: false, lastModified: DateTimeOffset.UtcNow);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/claim", async (Guid sessionId, ClaimControlRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            var ok = await contract.ClaimControlAsync(sessionId, request, cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.Conflict(new { error = "该玩家槽位已被其他控制端占用" });
        });

        app.MapPost("/api/sessions/{sessionId:guid}/release", async (Guid sessionId, ReleaseControlRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            await contract.ReleaseControlAsync(sessionId, request, cancellationToken);
            return Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/heartbeat", async (Guid sessionId, HttpRequest httpRequest, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            RefreshHeartbeatRequest request;
            if (httpRequest.ContentLength > 0 || httpRequest.HasJsonContentType())
            {
                request = await httpRequest.ReadFromJsonAsync<RefreshHeartbeatRequest>(BackendJsonDefaults.SerializerOptions, cancellationToken)
                    ?? new RefreshHeartbeatRequest();
            }
            else
            {
                int? player = null;
                if (int.TryParse(httpRequest.Query["player"], out var parsedPlayer))
                    player = parsedPlayer;

                var portId = string.IsNullOrWhiteSpace(httpRequest.Query["portId"])
                    ? null
                    : httpRequest.Query["portId"].ToString();

                request = new RefreshHeartbeatRequest(Player: player, PortId: portId);
            }

            await contract.RefreshHeartbeatAsync(sessionId, request, cancellationToken);
            return Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/buttons", async (Guid sessionId, ButtonStateRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            if (RemoteControlRequestCompatibility.TryBuildGenericInputRequest(request, "http-button-compat", out var genericRequest))
            {
                var genericOk = await contract.SetInputStateAsync(sessionId, genericRequest, cancellationToken);
                return genericOk
                    ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                    : Results.Conflict(new { error = "输入状态未能应用到当前会话" });
            }

            var ok = await contract.SetButtonStateAsync(sessionId, request, cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.Conflict(new { error = "当前会话未由该控制端持有" });
        });

        app.MapPost("/api/sessions/{sessionId:guid}/input", async (Guid sessionId, SetInputStateRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            var ok = await contract.SetInputStateAsync(sessionId, request, cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.Conflict(new { error = "输入状态未能应用到当前会话" });
        });

        app.MapPost("/internal/sync/roms", async (IReadOnlyList<RomSummaryDto> roms, IBackendStateSyncContract contract, CancellationToken cancellationToken) =>
        {
            await contract.ReplaceRomsAsync(roms, cancellationToken);
            return Results.Json(new { ok = true, count = roms.Count }, BackendJsonDefaults.SerializerOptions);
        });

        app.MapPost("/internal/sync/sessions", async (IReadOnlyList<GameSessionSummaryDto> sessions, IBackendStateSyncContract contract, CancellationToken cancellationToken) =>
        {
            await contract.ReplaceSessionsAsync(sessions, cancellationToken);
            return Results.Json(new { ok = true, count = sessions.Count }, BackendJsonDefaults.SerializerOptions);
        });
    }
}
