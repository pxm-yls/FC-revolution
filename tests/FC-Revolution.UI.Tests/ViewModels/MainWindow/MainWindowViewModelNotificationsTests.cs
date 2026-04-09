using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowViewModelNotificationsTests
{
    [Fact]
    public void AboutCommand_WritesTaskMessageHistory()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.AboutCommand.Execute(null);

        Assert.Contains(
            vm.FilteredTaskMessages,
            message => message.Content.Contains("FC-Revolution v0.1", StringComparison.Ordinal));
        Assert.Contains(vm.FilteredTaskMessages, message => message.Category == MessageCategory.Status);
        Assert.True(vm.TaskMessageCount > 0);
    }
}
