using System.Reflection;

namespace FCRevolution.Backend.Hosting.WebPad;

internal static class WebPadAssetCatalog
{
    private static readonly Assembly Assembly = typeof(WebPadAssetCatalog).Assembly;
    private static readonly string ResourcePrefix = $"{typeof(WebPadAssetCatalog).Namespace}.";

    public static readonly WebPadAsset IndexHtml = Load("index.html", "text/html; charset=utf-8");
    public static readonly WebPadAsset DebugMinimalHtml = Load("debug-minimal.html", "text/html; charset=utf-8");
    public static readonly WebPadAsset AppCss = Load("app.css", "text/css; charset=utf-8");
    public static readonly WebPadAsset AppJs = Load("app.js", "application/javascript; charset=utf-8");

    private static WebPadAsset Load(string fileName, string contentType)
    {
        var resourceName = ResourcePrefix + fileName;
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"未找到嵌入资源: {resourceName}");
        using var reader = new StreamReader(stream);
        return new WebPadAsset(fileName, contentType, reader.ReadToEnd());
    }
}

internal sealed record WebPadAsset(string FileName, string ContentType, string Content);
