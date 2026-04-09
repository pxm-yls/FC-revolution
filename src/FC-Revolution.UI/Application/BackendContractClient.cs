using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.UI.AppServices;

public sealed class BackendContractClient : IBackendContractClient
{
    private readonly HttpClient _httpClient;

    public BackendContractClient(string baseUrl)
    {
        BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        _httpClient = new HttpClient
        {
            BaseAddress = BaseAddress
        };
    }

    public Uri BaseAddress { get; }

    public async Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<RomSummaryDto>>("api/roms", cancellationToken);
        return response ?? [];
    }

    public async Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<GameSessionSummaryDto>>("api/sessions", cancellationToken);
        return response ?? [];
    }

    public async Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/sessions", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartSessionResponse>(cancellationToken);
    }

    public async Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/sessions/{sessionId}/close", content: null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/claim", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/release", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/heartbeat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"api/sessions/{sessionId}/input", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public void Dispose() => _httpClient.Dispose();
}
