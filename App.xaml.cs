using System;
using System.Diagnostics;
using System.Windows;

namespace VirtualDesktopOverlay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if running on .NET 10 or later
            if (!CheckDotNetVersion())
            {
                // Show error message
                MessageBox.Show(
                    "Virtual Desktop Overlay requires .NET 10 Desktop Runtime or later.\n\n" +
                    "Click OK to be redirected to the download page.",
                    "Missing Runtime",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Open download page
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://dotnet.microsoft.com/download/dotnet/10.0",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // If browser fails to open, show the URL
                    MessageBox.Show(
                        "Please visit:\nhttps://dotnet.microsoft.com/download/dotnet/10.0\n\nto download .NET 10 Desktop Runtime.",
                        "Download .NET 10",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                // Shutdown the application
                this.Shutdown();
            }
        }

        private bool CheckDotNetVersion()
        {
            try
            {
                // Get the runtime version
                var version = Environment.Version;
                
                // Check if major version is 10 or greater
                // .NET 10 = Version 10.x.x
                return version.Major >= 10;
            }
            catch
            {
                // If we can't determine the version, assume it's wrong
                return false;
            }
        }
    }
}
