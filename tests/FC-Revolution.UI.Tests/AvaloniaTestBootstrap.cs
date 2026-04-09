using Avalonia;
using Avalonia.Headless;

namespace FC_Revolution.UI.Tests;

public sealed class AvaloniaTestFixture
{
    private static bool _initialized;
    private static readonly object SyncRoot = new();

    public AvaloniaTestFixture()
    {
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            AppBuilder.Configure<FC_Revolution.UI.App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();

            _initialized = true;
        }
    }
}

[CollectionDefinition("Avalonia")]
public sealed class AvaloniaCollection : ICollectionFixture<AvaloniaTestFixture>;
