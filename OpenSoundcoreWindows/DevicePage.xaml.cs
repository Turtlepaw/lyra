using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace OpenSoundcoreWindows
{
    public sealed partial class DevicePage : Page
    {
        public DevicePage()
        {
            this.InitializeComponent();
            RefreshStats();
        }

        public void RefreshStats()
        {
            try
            {
                var snapshot = OpenScq30Native.GetDeviceSnapshot();
                UpdateStatsFromSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = $"Failed to refresh stats: {ex.Message}";
            }
        }

        private void UpdateStatsFromSnapshot(string snapshot)
        {
            if (snapshot.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            {
                ConnectionStatusText.Text = snapshot;
                return;
            }

            var values = ParseSnapshot(snapshot);
            var model = values.TryGetValue("model", out var modelValue) ? modelValue : "Life Q30";
            var ambient = values.TryGetValue("ambient", out var ambientValue) ? ambientValue : "N/A";
            var ancMode = values.TryGetValue("ancMode", out var ancModeValue) ? ancModeValue : "N/A";
            var battery = values.TryGetValue("battery", out var batteryValue) ? batteryValue : "--";

            DeviceModelText.Text = model;
            ConnectionStatusText.Text = "Connected";
            BatteryLevelText.Text = $"{battery}%";

            // Update Battery Icon
            if (int.TryParse(battery, out int level))
            {
                if (level >= 100) BatteryIcon.Glyph = "\uE83F"; // Battery10
                else if (level >= 90) BatteryIcon.Glyph = "\uE859"; // Battery9
                else if (level >= 80) BatteryIcon.Glyph = "\uE858"; // Battery8
                else if (level >= 70) BatteryIcon.Glyph = "\uE857"; // Battery7
                else if (level >= 60) BatteryIcon.Glyph = "\uE856"; // Battery6
                else if (level >= 50) BatteryIcon.Glyph = "\uE855"; // Battery5
                else if (level >= 40) BatteryIcon.Glyph = "\uE854"; // Battery4
                else if (level >= 30) BatteryIcon.Glyph = "\uE853"; // Battery3
                else if (level >= 20) BatteryIcon.Glyph = "\uE852"; // Battery2
                else if (level >= 10) BatteryIcon.Glyph = "\uE851"; // Battery1
                else BatteryIcon.Glyph = "\uE850"; // Battery0
            }

            // Update Sound Mode Buttons Styling
            UpdateButtonStyle(NoiseCancelingButton, ambient == "NoiseCanceling");
            UpdateButtonStyle(TransparencyButton, ambient == "Transparency");
            UpdateButtonStyle(NormalButton, ambient == "Normal");

            // Handle ANC Profiles
            SetAncVisibility(ambient == "NoiseCanceling", ancMode);
        }

        private void SetAncVisibility(bool visible, string activeProfile)
        {
            if (visible)
            {
                if (AncProfilesPanel.Visibility == Visibility.Collapsed)
                {
                    AncProfilesPanel.Visibility = Visibility.Visible;
                    ShowAncProfiles.Begin();
                }
                UpdateButtonStyle(AncTransportButton, activeProfile == "Transport");
                UpdateButtonStyle(AncIndoorButton, activeProfile == "Indoor");
                UpdateButtonStyle(AncOutdoorButton, activeProfile == "Outdoor");
            }
            else if (AncProfilesPanel.Visibility == Visibility.Visible)
            {
                HideAncProfiles.Begin();
                // We use a timer or Completed event to actually collapse it after animation
                HideAncProfiles.Completed += (s, e) => { AncProfilesPanel.Visibility = Visibility.Collapsed; };
            }
        }

        private void UpdateButtonStyle(Button btn, bool isActive)
        {
            btn.Style = (Style)Application.Current.Resources[isActive ? "AccentButtonStyle" : "DefaultButtonStyle"];
        }

        private Dictionary<string, string> ParseSnapshot(string snapshot)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in snapshot.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var split = part.Split('=', 2, StringSplitOptions.None);
                if (split.Length == 2)
                {
                    dict[split[0].Trim()] = split[1].Trim();
                }
            }
            return dict;
        }

        private async void SetError(string error)
        {
            SoundModeStatusText.Visibility = Visibility.Visible;
            SoundModeStatusText.Text = $"Error: {error}";
        }

        private async void SoundMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mode)
            {
                try
                {
                    await System.Threading.Tasks.Task.Run(() => OpenScq30Native.SetAmbientSoundMode(mode));
                    RefreshStats();
                }
                catch (Exception ex)
                {
                    SetError(ex.Message);
                }
            }
        }

        private async void AncProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string profile)
            {
                try
                {
                    await System.Threading.Tasks.Task.Run(() => OpenScq30Native.SetNoiseCancelingMode(profile));
                    RefreshStats();
                }
                catch (Exception ex)
                {
                    SetError(ex.Message);
                }
            }
        }

        private void RefreshStats_Click(object sender, RoutedEventArgs e) => RefreshStats();

        private void DisconnectDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenScq30Native.Disconnect();
                App.MainWindow?.SetNavVisibility(false);
                var frame = App.MainWindow?.ContentFrameRef;
                frame?.Navigate(typeof(ConnectPage));
                frame?.BackStack.Clear();
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = $"Error: {ex.Message}";
            }
        }
    }
}
