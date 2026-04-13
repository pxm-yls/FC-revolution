using System.Net.WebSockets;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FCRevolution.Backend.Hosting.WebSockets;

internal sealed class BackendControlWebSocketHandler
{
    private const string WebSocketButtonDeviceType = "websocket-button";

    private static bool TryResolveIncomingPort(
        SocketClientMessage incoming,
        RemoteControlLeaseCoordinator leaseCoordinator,
        bool allowClaimFallback,
        out string portId)
    {
        if (ControlMessageCodec.TryResolveControlPort(incoming.PortId, out portId))
            return true;

        if (allowClaimFallback &&
            !string.IsNullOrWhiteSpace(leaseCoordinator.ClaimedPortId))
        {
            portId = leaseCoordinator.ClaimedPortId;
            return true;
        }

        portId = string.Empty;
        return false;
    }

    internal async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var remoteControlContract = context.RequestServices.GetRequiredService<IRemoteControlContract>();
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        WebSocket? socket = null;
        RemoteControlLeaseCoordinator? finalLeaseCoordinator = null;

        try
        {
            socket = await context.WebSockets.AcceptWebSocketAsync();
            var codec = new ControlMessageCodec(socket);
            var leaseCoordinator = new RemoteControlLeaseCoordinator(remoteControlContract, codec, clientIp, cancellationToken);
            finalLeaseCoordinator = leaseCoordinator;
            await codec.SendSocketMessageAsync(new SocketMessage("ready", "WebSocket 已连接"));

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var incoming = await codec.ReceiveClientMessageAsync(cancellationToken);
                if (incoming == null)
                    break;

                switch (incoming.Action)
                {
                    case "claim":
                    {
                        var claimSessionId = ControlMessageCodec.ParseSessionId(incoming.SessionId);
                        if (!claimSessionId.HasValue)
                        {
                            await codec.SendSocketMessageAsync(new SocketMessage("error", "sessionId 无效", incoming.SessionId, incoming.PortId));
                            break;
                        }

                        if (!TryResolveIncomingPort(incoming, leaseCoordinator, allowClaimFallback: false, out var claimPortId))
                        {
                            await codec.SendSocketMessageAsync(new SocketMessage("error", "portId 无效", claimSessionId.Value.ToString(), incoming.PortId));
                            break;
                        }

                        leaseCoordinator.UpdateClientName(incoming.ClientName);
                        var claimResult = await leaseCoordinator.EnsureClaimAsync(claimSessionId.Value, claimPortId, "该控制端口已被其他网页占用");
                        if (!claimResult.Success)
                            break;

                        await codec.SendSocketMessageAsync(
                            ControlMessageCodec.CreateSocketMessage(
                                "claimed",
                                claimResult.Changed ? "控制权已分配" : "控制权保持不变",
                                claimSessionId.Value.ToString(),
                                claimPortId));
                        break;
                    }

                    case "heartbeat":
                    {
                        var heartbeatSessionId = ControlMessageCodec.ParseSessionId(incoming.SessionId) ?? leaseCoordinator.ClaimedSessionId;
                        if (!heartbeatSessionId.HasValue ||
                            !TryResolveIncomingPort(incoming, leaseCoordinator, allowClaimFallback: true, out var heartbeatPortId))
                        {
                            await codec.SendSocketMessageAsync(new SocketMessage("error", "请先 claim 控制端口", incoming.SessionId, incoming.PortId));
                            break;
                        }

                        var heartbeatClaim = await leaseCoordinator.EnsureClaimAsync(heartbeatSessionId.Value, heartbeatPortId, "目标控制端口当前不可用");
                        if (!heartbeatClaim.Success)
                            break;

                        if (heartbeatClaim.Changed)
                        {
                            await codec.SendSocketMessageAsync(
                                ControlMessageCodec.CreateSocketMessage("claimed", "控制权已切换", heartbeatSessionId.Value.ToString(), heartbeatPortId));
                        }

                        await remoteControlContract.RefreshHeartbeatAsync(
                            heartbeatSessionId.Value,
                            new RefreshHeartbeatRequest(PortId: heartbeatPortId),
                            cancellationToken);
                        break;
                    }

                    case "button":
                    {
                        var buttonSessionId = ControlMessageCodec.ParseSessionId(incoming.SessionId) ?? leaseCoordinator.ClaimedSessionId;
                        if (!buttonSessionId.HasValue ||
                            !TryResolveIncomingPort(incoming, leaseCoordinator, allowClaimFallback: true, out var buttonPortId))
                        {
                            await codec.SendSocketMessageAsync(new SocketMessage("error", "请先 claim 控制端口", incoming.SessionId, incoming.PortId));
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(incoming.Button))
                        {
                            await codec.SendSocketMessageAsync(ControlMessageCodec.CreateSocketMessage("error", "按键无效", buttonSessionId.Value.ToString(), buttonPortId));
                            break;
                        }

                        var buttonClaim = await leaseCoordinator.EnsureClaimAsync(buttonSessionId.Value, buttonPortId, "目标控制端口当前不可用");
                        if (!buttonClaim.Success)
                            break;

                        if (buttonClaim.Changed)
                        {
                            await codec.SendSocketMessageAsync(
                                ControlMessageCodec.CreateSocketMessage("claimed", "控制权已切换", buttonSessionId.Value.ToString(), buttonPortId));
                        }

                        var actionId = incoming.Button.Trim();
                        var genericButtonRequest = new SetInputStateRequest(
                        [
                            new InputActionValueDto(
                                buttonPortId,
                                WebSocketButtonDeviceType,
                                actionId,
                                (incoming.Pressed ?? false) ? 1f : 0f)
                        ]);

                        var pressedOk = await remoteControlContract.SetInputStateAsync(
                            buttonSessionId.Value,
                            genericButtonRequest,
                            cancellationToken);
                        if (!pressedOk)
                        {
                            leaseCoordinator.InvalidateClaim();
                            await codec.SendSocketMessageAsync(ControlMessageCodec.CreateSocketMessage("error", "当前控制权已失效", buttonSessionId.Value.ToString(), buttonPortId));
                        }

                        break;
                    }

                    case "input":
                    {
                        var inputSessionId = ControlMessageCodec.ParseSessionId(incoming.SessionId) ?? leaseCoordinator.ClaimedSessionId;
                        if (!inputSessionId.HasValue ||
                            !TryResolveIncomingPort(incoming, leaseCoordinator, allowClaimFallback: true, out var inputPortId))
                        {
                            await codec.SendSocketMessageAsync(new SocketMessage("error", "请先 claim 控制端口", incoming.SessionId, incoming.PortId));
                            break;
                        }

                        if (incoming.Inputs == null || incoming.Inputs.Count == 0)
                        {
                            await codec.SendSocketMessageAsync(ControlMessageCodec.CreateSocketMessage("error", "输入动作不能为空", inputSessionId.Value.ToString(), inputPortId));
                            break;
                        }

                        var inputClaim = await leaseCoordinator.EnsureClaimAsync(inputSessionId.Value, inputPortId, "目标控制端口当前不可用");
                        if (!inputClaim.Success)
                            break;

                        if (inputClaim.Changed)
                        {
                            await codec.SendSocketMessageAsync(
                                ControlMessageCodec.CreateSocketMessage("claimed", "控制权已切换", inputSessionId.Value.ToString(), inputPortId));
                        }

                        var inputOk = await remoteControlContract.SetInputStateAsync(
                            inputSessionId.Value,
                            new SetInputStateRequest(incoming.Inputs),
                            cancellationToken);
                        if (!inputOk)
                        {
                            leaseCoordinator.InvalidateClaim();
                            await codec.SendSocketMessageAsync(ControlMessageCodec.CreateSocketMessage("error", "当前输入状态未能应用", inputSessionId.Value.ToString(), inputPortId));
                        }

                        break;
                    }

                    case "release":
                    {
                        var releaseSessionId = ControlMessageCodec.ParseSessionId(incoming.SessionId) ?? leaseCoordinator.ClaimedSessionId;
                        if (releaseSessionId.HasValue &&
                            TryResolveIncomingPort(incoming, leaseCoordinator, allowClaimFallback: true, out var releasePortId))
                        {
                            await leaseCoordinator.ReleaseSpecifiedAsync(releaseSessionId.Value, releasePortId, "网页控制已释放");

                            await codec.SendSocketMessageAsync(
                                ControlMessageCodec.CreateSocketMessage("released", "控制权已释放", releaseSessionId.Value.ToString(), releasePortId));
                        }

                        break;
                    }
                }
            }
        }
        finally
        {
            if (finalLeaseCoordinator != null)
                await finalLeaseCoordinator.ReleaseOnDisconnectAsync();

            if (socket is { State: WebSocketState.Open })
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }
}
