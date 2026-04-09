using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FCRevolution.Backend.Hosting;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendHealthAndAssetsEndpointTests
{
    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();

        var response = await host.Client.GetFromJsonAsync<HealthResponse>("api/health");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal(host.Port, response.Port);
    }

    [Fact]
    public async Task External_Curl_Can_Reach_Health_Endpoint()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/curl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-sS");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add($"{host.HttpBaseAddress}api/health");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process!.WaitForExitAsync();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("200 OK", stdout);
        Assert.Contains("\"ok\":true", stdout);
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
    }

    [Fact]
    public async Task WebPad_And_Debug_Pages_Are_Served_From_Embedded_Assets()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();

        var indexHtml = await host.Client.GetStringAsync("/");
        var debugHtml = await host.Client.GetStringAsync("debug/minimal");
        var plainHtml = await host.Client.GetStringAsync("debug/html");
        var appCss = await host.Client.GetStringAsync("assets/webpad/app.css");
        var appJs = await host.Client.GetStringAsync("assets/webpad/app.js");

        Assert.Contains("FC Revolution LAN Pad", indexHtml);
        Assert.Contains("/assets/webpad/app.css", indexHtml);
        Assert.Contains("/assets/webpad/app.js", indexHtml);
        Assert.Contains("LAN Pad Debug OK", debugHtml);
        Assert.Contains("LAN PAD HTML DEBUG OK", plainHtml);
        Assert.Contains(".app-shell", appCss);
        Assert.Contains("refreshAllData", appJs);
        Assert.Contains("ensureSocketClaim", appJs);
    }

    [Fact]
    public async Task Disable_WebPad_Hides_WebPad_Endpoints_But_Health_Remains_Available()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync(
            optionsFactory: port => new BackendHostOptions(port, EnableWebPad: false, EnableDebugPages: true));

        using var index = await host.Client.GetAsync("/");
        using var appCss = await host.Client.GetAsync("assets/webpad/app.css");
        using var appJs = await host.Client.GetAsync("assets/webpad/app.js");
        using var debugMinimal = await host.Client.GetAsync("debug/minimal");
        using var health = await host.Client.GetAsync("api/health");

        Assert.Equal(HttpStatusCode.NotFound, index.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, appCss.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, appJs.StatusCode);
        Assert.Equal(HttpStatusCode.OK, debugMinimal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Disable_DebugPages_Hides_Debug_Endpoints_But_WebPad_And_Health_Remain_Available()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync(
            optionsFactory: port => new BackendHostOptions(port, EnableWebPad: true, EnableDebugPages: false));

        using var debugMinimal = await host.Client.GetAsync("debug/minimal");
        using var debugHtml = await host.Client.GetAsync("debug/html");
        using var debugPlain = await host.Client.GetAsync("debug/plain");
        using var index = await host.Client.GetAsync("/");
        using var appCss = await host.Client.GetAsync("assets/webpad/app.css");
        using var health = await host.Client.GetAsync("api/health");

        Assert.Equal(HttpStatusCode.NotFound, debugMinimal.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, debugHtml.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, debugPlain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal(HttpStatusCode.OK, appCss.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private sealed record HealthResponse(bool Ok, int Port);
}
