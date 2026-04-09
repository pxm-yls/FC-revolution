using Avalonia.Threading;

namespace FC_Revolution.UI.Tests;

internal static class AvaloniaThreadingTestHelper
{
    public static void RunOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Send).GetAwaiter().GetResult();
    }

    public static T RunOnUiThread<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
            return action();

        return Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Send).GetAwaiter().GetResult();
    }

    public static void DrainJobs() =>
        RunOnUiThread(() => Dispatcher.UIThread.RunJobs());
}
