using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal static class DebugDisplaySettingsController
{
    public static DebugWindowDisplaySettingsProfile LoadFromSystemConfig()
    {
        try
        {
            return DebugWindowDisplaySettingsProfile.Sanitize(SystemConfigProfile.Load().DebugWindowDisplaySettings);
        }
        catch
        {
            return DebugWindowDisplaySettingsProfile.Sanitize(null);
        }
    }

    public static void SaveToSystemConfig(DebugWindowDisplaySettingsProfile settings)
    {
        try
        {
            var profile = SystemConfigProfile.Load();
            profile.DebugWindowDisplaySettings = DebugWindowDisplaySettingsProfile.Sanitize(settings);
            SystemConfigProfile.Save(profile);
        }
        catch
        {
        }
    }
}
