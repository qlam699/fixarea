using Microsoft.UI.Xaml;

namespace FixArea.App;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Required for PublishSingleFile: native WASDK looks next to the EXE unless told
        // to use the .NET extract directory (AppContext.BaseDirectory).
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            AppContext.BaseDirectory);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ => new App());
    }
}
