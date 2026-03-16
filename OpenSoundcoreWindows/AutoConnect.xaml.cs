using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;

namespace OpenSoundcoreWindows
{
    public sealed partial class AutoConnect : Page
    {
        public AutoConnect()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Start progress ring
            ConnectingProgressRing.IsActive = true;
            
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string mac = settings.Values["LastConnectedMac"] as string;
            string name = settings.Values["LastConnectedName"] as string;

            if (string.IsNullOrEmpty(mac))
            {
                NavigateToConnect();
                return;
            }

            try
            {
                // Attempt connection in background
                string result = await Task.Run(() => 
                {
                    if (mac == "00:00:00:00:00:00")
                        return OpenScq30Native.ConnectDemoDevice();
                    else
                        return OpenScq30Native.Connect(mac, name);
                });

                if (result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    // Fail gracefully and go to connect page
                    NavigateToConnect();
                }
                else
                {
                    // Success!
                    OnConnectComplete();
                }
            }
            catch (Exception)
            {
                NavigateToConnect();
            }
        }

        private void OnConnectComplete()
        {
            var mainWindow = App.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SetNavVisibility(true);
                var frame = mainWindow.ContentFrameRef;
                frame.Navigate(typeof(DevicePage));
                frame.BackStack.Clear();
            }
        }

        private void NavigateToConnect()
        {
            var mainWindow = App.MainWindow;
            if (mainWindow != null)
            {
                var frame = mainWindow.ContentFrameRef;
                frame.Navigate(typeof(ConnectPage));
                frame.BackStack.Clear();
            }
        }
    }
}
