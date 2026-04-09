using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FCRevolution.Contracts.RemoteControl;
using FC_Revolution.UI.AppServices;

namespace FC_Revolution.UI.Tests;

public sealed class BackendContractClientTests
{
    [Fact]
    public async Task SetInputStateAsync_PostsInputRequest()
    {
        var sessionId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/input")
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = CreateClient(handler);

        var changed = await client.SetInputStateAsync(
            sessionId,
            new SetInputStateRequest(
            [
                new InputActionValueDto("p2", "gamepad", "x", 1f)
            ]));

        Assert.True(changed);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal($"/api/sessions/{sessionId}/input", captured.Path);

        var payload = JsonSerializer.Deserialize<SetInputStateRequest>(captured.Body, JsonOptions);
        Assert.NotNull(payload);
        var action = Assert.Single(payload.Actions);
        Assert.Equal("p2", action.PortId);
        Assert.Equal("x", action.ActionId);
        Assert.Equal(1f, action.Value);
    }

    [Fact]
    public async Task SetInputStateAsync_WhenInputReturnsConflict_ReturnsFalse()
    {
        var sessionId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == $"/api/sessions/{sessionId}/input")
                return new HttpResponseMessage(HttpStatusCode.Conflict);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = CreateClient(handler);

        var changed = await client.SetInputStateAsync(
            sessionId,
            new SetInputStateRequest(
            [
                new InputActionValueDto("p2", "gamepad", "x", 1f)
            ]));

        Assert.False(changed);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal($"/api/sessions/{sessionId}/input", captured.Path);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static BackendContractClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var client = new BackendContractClient("http://localhost:18999/");
        var field = typeof(BackendContractClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(client, new HttpClient(handler) { BaseAddress = client.BaseAddress });
        return client;
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                body));

            return responder(request);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);
}
