using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FC_Revolution.UI.Infrastructure;

internal static class LanFirewallProbe
{
    private const string MacFirewallToolPath = "/usr/libexec/ApplicationFirewall/socketfilterfw";

    public static LanFirewallStatus Probe()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new LanFirewallStatus(
                "防火墙检测暂未实现",
                "当前版本仅对 macOS Application Firewall 做了自动检测。",
                LanFirewallStatusLevel.Info);
        }

        try
        {
            var globalState = RunTool("--getglobalstate");
            var stealthMode = RunTool("--getstealthmode");
            var processPath = Environment.ProcessPath;
            var appBlocked = string.IsNullOrWhiteSpace(processPath) ? string.Empty : RunTool($"--getappblocked \"{processPath}\"");

            var firewallEnabled = globalState.Contains("enabled", StringComparison.OrdinalIgnoreCase) &&
                                  !globalState.Contains("disabled", StringComparison.OrdinalIgnoreCase);
            var stealthEnabled = stealthMode.Contains("on", StringComparison.OrdinalIgnoreCase);
            var appBlockedState = appBlocked.Contains("ALF: deny", StringComparison.OrdinalIgnoreCase);

            if (!firewallEnabled)
            {
                return new LanFirewallStatus(
                    "系统防火墙已关闭",
                    "当前 macOS 防火墙处于关闭状态，局域网访问通常不会被系统防火墙拦截。",
                    LanFirewallStatusLevel.Ok);
            }

            if (appBlockedState)
            {
                return new LanFirewallStatus(
                    "系统防火墙可能正在拦截本程序",
                    "检测到 macOS 防火墙已开启，且当前程序可能处于阻止状态。请到系统设置 -> 网络 -> 防火墙里允许本程序接收入站连接。",
                    LanFirewallStatusLevel.Warning);
            }

            if (stealthEnabled)
            {
                return new LanFirewallStatus(
                    "系统防火墙已开启，且隐身模式开启",
                    "隐身模式可能影响其他主机探测到本机服务。若局域网仍无法访问，请检查防火墙是否已允许当前程序接收入站连接。",
                    LanFirewallStatusLevel.Warning);
            }

            return new LanFirewallStatus(
                "系统防火墙已开启",
                "未检测到当前程序被明确阻止，但若其他主机仍无法访问，请在系统防火墙中确认已允许本程序接收入站连接。",
                LanFirewallStatusLevel.Info);
        }
        catch (Exception ex)
        {
            return new LanFirewallStatus(
                "防火墙检测失败",
                $"无法读取系统防火墙状态: {ex.Message}",
                LanFirewallStatusLevel.Warning);
        }
    }

    private static string RunTool(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = MacFirewallToolPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());

        return stdout.Trim();
    }
}

internal enum LanFirewallStatusLevel
{
    Info = 0,
    Ok = 1,
    Warning = 2
}

internal sealed record LanFirewallStatus(string Title, string Detail, LanFirewallStatusLevel Level);
