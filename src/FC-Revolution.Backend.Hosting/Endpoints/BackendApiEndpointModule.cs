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
    private const string ButtonCompatDeviceType = "http-button-edge";

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
            var ok = await contract.ClaimControlAsync(sessionId, NormalizeRequest(request), cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.Conflict(new { error = "该控制端口已被其他控制端占用" });
        });

        app.MapPost("/api/sessions/{sessionId:guid}/release", async (Guid sessionId, ReleaseControlRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            await contract.ReleaseControlAsync(sessionId, NormalizeRequest(request), cancellationToken);
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
                request = new RefreshHeartbeatRequest(PortId: NormalizePortId(httpRequest.Query["portId"].ToString()));
            }

            await contract.RefreshHeartbeatAsync(sessionId, NormalizeRequest(request), cancellationToken);
            return Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/buttons", async (Guid sessionId, ButtonCompatRequest request, IRemoteControlContract contract, CancellationToken cancellationToken) =>
        {
            if (!TryBuildInputRequest(request, out var genericRequest))
                return Results.BadRequest(new { error = "按键输入无效" });

            var ok = await contract.SetInputStateAsync(sessionId, genericRequest, cancellationToken);
            return ok
                ? Results.Json(new { ok = true }, BackendJsonDefaults.SerializerOptions)
                : Results.Conflict(new { error = "输入状态未能应用到当前会话" });
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

    private static bool TryBuildInputRequest(ButtonCompatRequest request, out SetInputStateRequest inputStateRequest)
    {
        ArgumentNullException.ThrowIfNull(request);

        inputStateRequest = default!;

        var actionId = request.ActionId;
        if (string.IsNullOrWhiteSpace(actionId))
            actionId = request.Button;

        if (string.IsNullOrWhiteSpace(actionId))
            return false;

        var portId = NormalizePortId(request.PortId);
        if (string.IsNullOrWhiteSpace(portId))
            return false;

        inputStateRequest = new SetInputStateRequest(
        [
            new InputActionValueDto(
                portId,
                ButtonCompatDeviceType,
                actionId.Trim(),
                request.Pressed ? 1f : 0f)
        ]);
        return true;
    }

    private static ClaimControlRequest NormalizeRequest(ClaimControlRequest request) =>
        new(
            ClientIp: request.ClientIp,
            ClientName: request.ClientName,
            PortId: NormalizePortId(request.PortId));

    private static ReleaseControlRequest NormalizeRequest(ReleaseControlRequest request) =>
        new(
            Reason: request.Reason,
            PortId: NormalizePortId(request.PortId));

    private static RefreshHeartbeatRequest NormalizeRequest(RefreshHeartbeatRequest request) =>
        new(PortId: NormalizePortId(request.PortId));

    private static string? NormalizePortId(string? portId)
    {
        if (string.IsNullOrWhiteSpace(portId))
            return null;

        return portId.Trim();
    }

    private sealed record ButtonCompatRequest(
        string? PortId = null,
        bool Pressed = false,
        string? ActionId = null,
        string? Button = null);
}
