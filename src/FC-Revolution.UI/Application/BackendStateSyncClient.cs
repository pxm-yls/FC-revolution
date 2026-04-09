using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.UI.AppServices;

public sealed class BackendStateSyncClient : IBackendStateSyncClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public BackendStateSyncClient(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/'))
        };
    }

    public async Task PushRomsAsync(IReadOnlyList<RomSummaryDto> roms, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/internal/sync/roms", roms, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PushSessionsAsync(IReadOnlyList<GameSessionSummaryDto> sessions, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/internal/sync/sessions", sessions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _httpClient.Dispose();
}
