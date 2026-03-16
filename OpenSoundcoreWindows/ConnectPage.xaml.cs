using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace OpenSoundcoreWindows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectPage : Page
    {
        private class BluetoothDeviceItem
        {
            public string Name { get; set; } = "";
            public string Id { get; set; } = "";
            public string MacAddress { get; set; } = "";

            public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : Name;
        }

        public ConnectPage()
        {
            InitializeComponent();
#if DEBUG
            ConnectDemoButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
#endif
            _ = RefreshConnectedDevicesAsync();
        }

        private void OnConnectComplete(string mac, string name)
        {
            var mainWindow = App.MainWindow;

            if (mainWindow != null)
            {
                // Store last connected device for auto-connect
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
                settings.Values["LastConnectedMac"] = mac;
                settings.Values["LastConnectedName"] = name;

                mainWindow.SetNavVisibility(true);
                var frame = mainWindow.ContentFrameRef;

                frame.Navigate(typeof(DevicePage));
                frame.BackStack.Clear();
            }
        }

        private async void ConnectDemo_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                ConnectionStatusText.Text = "Connecting to Demo Device...";
                ConnectionStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];
                ConnectingProgressRing.IsActive = true;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

                var connectMessage = await System.Threading.Tasks.Task.Run(() => OpenScq30Native.ConnectDemoDevice());

                ConnectingProgressRing.IsActive = false;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                if (connectMessage.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                {
                    ConnectionStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
                }
                ConnectionStatusText.Text = connectMessage;
                // Demo uses a special mac address
                OnConnectComplete("00:00:00:00:00:00", "Demo Q30");
            }
            catch (Exception ex)
            {
                ConnectingProgressRing.IsActive = false;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                ConnectionStatusText.Text = $"Failed to connect demo device: {ex.Message}";
                ConnectionStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            }
        }

        private async System.Threading.Tasks.Task RefreshConnectedDevicesAsync()
        {
            try
            {
                ConnectionStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];
                ConnectingProgressRing.IsActive = true;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

                var requestedProperties = new[] { 
                    "System.Devices.Aep.IsConnected",
                    "System.Devices.Aep.DeviceAddress" 
                };
                var selector = BluetoothDevice.GetDeviceSelector();
                var devices = await DeviceInformation.FindAllAsync(selector, requestedProperties);

                var connected = new List<BluetoothDeviceItem>();

                foreach (var device in devices)
                {
                    if (device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value)
                        && value is bool isConnected
                        && isConnected)
                    {
                        var mac = device.Id;
                        if (device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var addrObj) 
                            && addrObj is string addr)
                        {
                            mac = addr;
                        }

                        connected.Add(new BluetoothDeviceItem
                        {
                            Name = device.Name,
                            Id = device.Id,
                            MacAddress = mac
                        });
                    }
                }

                var orderedList = connected.OrderBy(x => x.Name).ToList();

                ConnectingProgressRing.IsActive = false;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                if (orderedList.Count == 0)
                {
                    ConnectedDevicesList.ItemsSource = null;
                }
                else
                {
                    ConnectedDevicesList.ItemsSource = orderedList;
                }
            }
            catch (Exception ex)
            {
                ConnectingProgressRing.IsActive = false;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                ConnectionStatusText.Text = $"Failed to query connected devices: {ex.Message}";
                ConnectionStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
                ConnectedDevicesList.ItemsSource = null;
            }
        }

        private async void RefreshDevices_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await RefreshConnectedDevicesAsync();
        }

        private async void ConnectedDevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ConnectedDevicesList.SelectedItem is BluetoothDeviceItem item && !string.IsNullOrEmpty(item.MacAddress))
                {
                    ConnectionStatusText.Text = $"Connecting to {item.Name}...";
                    ConnectionStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];
                    ConnectingProgressRing.IsActive = true;
                    ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

                    var connectMessage = await System.Threading.Tasks.Task.Run(() => OpenScq30Native.Connect(item.MacAddress, item.Name));
                    
                    ConnectingProgressRing.IsActive = false;
                    ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                    if (connectMessage.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                    {
                        ConnectionStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
                    } else
                    {
                        OnConnectComplete(item.MacAddress, item.Name);
                    }
                    ConnectionStatusText.Text = connectMessage;
                }
                else
                {
                    ConnectionStatusText.Text = "Please select a connected device from the list above.";
                    ConnectionStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];
                }
            }
            catch (Exception ex)
            {
                ConnectingProgressRing.IsActive = false;
                ConnectingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                ConnectionStatusText.Text = $"Failed to connect device: {ex.Message}";
                ConnectionStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            }
        }

        private void ResetStats()
        {
            // Placeholder for any status resets if needed in this page
        }

        private void DisconnectDevice_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var message = OpenScq30Native.Disconnect();
                ConnectionStatusText.Text = message;
                ResetStats();
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = $"Failed to disconnect: {ex.Message}";
            }
        }
    }
}
