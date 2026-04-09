using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class WebProbeViewModel : ViewModelBase
{
    private readonly string _localBaseUrl;
    private readonly string _lanBaseUrl;

    public WebProbeViewModel(string localBaseUrl, string lanBaseUrl)
    {
        _localBaseUrl = localBaseUrl.TrimEnd('/');
        _lanBaseUrl = lanBaseUrl.TrimEnd('/');
        WindowSummary = $"本机: {_localBaseUrl} | 局域网: {_lanBaseUrl}";
        ProbeOutput = "点击“刷新探针”开始检测。";
    }

    [ObservableProperty]
    private string _windowSummary = string.Empty;

    [ObservableProperty]
    private string _probeOutput = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var builder = new StringBuilder();
            await AppendProbeAsync(builder, $"{_localBaseUrl}/", "localhost /");
            await AppendProbeAsync(builder, $"{_localBaseUrl}/debug/minimal", "localhost /debug/minimal");
            await AppendProbeAsync(builder, $"{_localBaseUrl}/debug/html", "localhost /debug/html");
            await AppendProbeAsync(builder, $"{_localBaseUrl}/assets/webpad/app.js", "localhost /assets/webpad/app.js");
            await AppendProbeAsync(builder, $"{_lanBaseUrl}/", "lan /");
            await AppendProbeAsync(builder, $"{_lanBaseUrl}/debug/minimal", "lan /debug/minimal");
            await AppendProbeAsync(builder, $"{_lanBaseUrl}/debug/html", "lan /debug/html");
            ProbeOutput = builder.ToString().TrimEnd();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task AppendProbeAsync(StringBuilder builder, string url, string label)
    {
        builder.AppendLine($"[{label}] {url}");
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            using var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            builder.AppendLine($"status={(int)response.StatusCode} type={response.Content.Headers.ContentType?.MediaType ?? "unknown"} len={content.Length}");
            builder.AppendLine(BuildPreview(content));
        }
        catch (Exception ex)
        {
            builder.AppendLine($"error={ex.Message}");
        }

        builder.AppendLine();
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

        if (normalized.Length > 240)
            normalized = normalized[..240] + "...";

        return normalized;
    }
}
