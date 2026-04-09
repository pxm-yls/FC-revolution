using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FC_Revolution.UI.AppServices;

public sealed class LanArcadeDiagnosticsService : ILanArcadeDiagnosticsService
{
    public async Task<string> BuildDiagnosticsAsync(int port, string entryUrl, ILanArcadeService lanArcadeService, IReadOnlyList<string>? lanCandidates = null)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Where(endpoint => endpoint.Port == port)
            .Select(endpoint => endpoint.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string localhostResult;
        string lanEntryResult;
        string rootPageResult;
        string scriptResult;
        string rootPagePreview;
        string scriptPreview;
        string externalCurlLocalHealth;
        string externalCurlLocalRoot;
        string externalCurlLanHealth;
        string listenerProbe;
        try
        {
            localhostResult = await lanArcadeService.GetLocalHealthAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            localhostResult = $"失败: {ex.Message}";
        }

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            lanEntryResult = await client.GetStringAsync($"{entryUrl.TrimEnd('/')}/api/health");
        }
        catch (Exception ex)
        {
            lanEntryResult = $"失败: {ex.Message}";
        }

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            using var response = await client.GetAsync(entryUrl);
            var content = await response.Content.ReadAsStringAsync();
            rootPageResult = $"{(int)response.StatusCode} len={content.Length} type={response.Content.Headers.ContentType?.MediaType ?? "unknown"}";
            rootPagePreview = BuildPreview(content);
        }
        catch (Exception ex)
        {
            rootPageResult = $"失败: {ex.Message}";
            rootPagePreview = "无";
        }

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            using var response = await client.GetAsync($"{entryUrl.TrimEnd('/')}/assets/webpad/app.js");
            var content = await response.Content.ReadAsStringAsync();
            scriptResult = $"{(int)response.StatusCode} len={content.Length} type={response.Content.Headers.ContentType?.MediaType ?? "unknown"}";
            scriptPreview = BuildPreview(content);
        }
        catch (Exception ex)
        {
            scriptResult = $"失败: {ex.Message}";
            scriptPreview = "无";
        }

        externalCurlLocalHealth = await RunExternalHttpProbeAsync($"http://127.0.0.1:{port}/api/health");
        externalCurlLocalRoot = await RunExternalHttpProbeAsync($"http://127.0.0.1:{port}/");
        externalCurlLanHealth = await RunExternalHttpProbeAsync($"{entryUrl.TrimEnd('/')}/api/health");
        listenerProbe = await RunExternalListenerProbeAsync(port);

        var listenerText = listeners.Count > 0
            ? string.Join(", ", listeners)
            : "未发现该端口处于监听状态";

        var candidateResults = new List<string>();
        if (lanCandidates is { Count: > 0 })
        {
            foreach (var candidate in lanCandidates.Take(4))
            {
                try
                {
                    using var client = new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(2)
                    };
                    var candidateResult = await client.GetStringAsync($"http://{candidate}:{port}/api/health");
                    candidateResults.Add($"{candidate} => {candidateResult}");
                }
                catch (Exception ex)
                {
                    candidateResults.Add($"{candidate} => 失败: {ex.Message}");
                }
            }
        }

        return
            $"监听端口: {port}{Environment.NewLine}" +
            $"宿主模式: Backend.Hosting (embedded){Environment.NewLine}" +
            $"活动监听地址: {listenerText}{Environment.NewLine}" +
            $"本机回环健康检查: {localhostResult}{Environment.NewLine}" +
            $"本机局域网入口健康检查: {lanEntryResult}{Environment.NewLine}" +
            $"首页返回检测: {rootPageResult}{Environment.NewLine}" +
            $"首页返回预览: {rootPagePreview}{Environment.NewLine}" +
            $"脚本资源检测: {scriptResult}{Environment.NewLine}" +
            $"脚本资源预览: {scriptPreview}{Environment.NewLine}" +
            $"进程外 HTTP 探针(127.0.0.1 /api/health): {externalCurlLocalHealth}{Environment.NewLine}" +
            $"进程外 HTTP 探针(127.0.0.1 /): {externalCurlLocalRoot}{Environment.NewLine}" +
            $"进程外 HTTP 探针(LAN /api/health): {externalCurlLanHealth}{Environment.NewLine}" +
            $"进程外监听探针: {listenerProbe}{Environment.NewLine}" +
            $"候选局域网地址: {(lanCandidates is { Count: > 0 } ? string.Join(", ", lanCandidates) : "未解析")}{Environment.NewLine}" +
            $"候选地址逐项检测: {(candidateResults.Count > 0 ? string.Join(" | ", candidateResults) : "无")}{Environment.NewLine}" +
            $"局域网入口: {entryUrl}";
    }

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "(empty)";

        var normalized = content
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Trim();

        if (normalized.Length > 120)
            normalized = normalized[..120] + "...";

        return normalized;
    }

    private static Task<string> RunExternalHttpProbeAsync(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var command =
                "$ProgressPreference='SilentlyContinue'; " +
                $"$r=Invoke-WebRequest -UseBasicParsing '{url}'; " +
                "Write-Output ('status=' + [int]$r.StatusCode); " +
                "Write-Output $r.Content";
            return RunExternalProbeAsync("powershell.exe", "-NoProfile", "-Command", command);
        }

        return RunExternalProbeAsync("/usr/bin/curl", "-sS", "-D", "-", url);
    }

    private static Task<string> RunExternalListenerProbeAsync(int port)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RunExternalProbeAsync("cmd.exe", "/c", $"netstat -ano | findstr :{port}");

        return RunExternalProbeAsync("/usr/sbin/lsof", "-nP", $"-iTCP:{port}", "-sTCP:LISTEN");
    }

    private static async Task<string> RunExternalProbeAsync(string fileName, params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
                return "未能启动外部探针";

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, timeoutTask);
            if (completed != waitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return "超时";
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            if (string.IsNullOrWhiteSpace(output))
                output = $"exit={process.ExitCode}";
            else
                output = $"exit={process.ExitCode} {BuildPreview(output)}";

            return output;
        }
        catch (Exception ex)
        {
            return $"失败: {ex.Message}";
        }
    }
}
