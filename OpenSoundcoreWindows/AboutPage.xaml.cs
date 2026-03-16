using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace OpenSoundcoreWindows
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            RefreshStats();
        }

        private void RefreshStats_Click(object sender, RoutedEventArgs e)
        {
            RefreshStats();
        }

        public void RefreshStats()
        {
            try
            {
                var snapshot = OpenScq30Native.GetDeviceSnapshot();
                UpdateStatsFromSnapshot(snapshot);
            }
            catch (Exception)
            {
                // Silent failure or basic feedback for about page
                DeviceModelText.Text = "Model: Failed to fetch";
            }
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

        private void UpdateStatsFromSnapshot(string snapshot)
        {
            if (snapshot.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            {
                DeviceModelText.Text = "Model: N/A (Disconnected)";
                return;
            }

            var values = ParseSnapshot(snapshot);
            var model = values.TryGetValue("model", out var modelValue) ? modelValue : "N/A";
            var ambient = values.TryGetValue("ambient", out var ambientValue) ? ambientValue : "N/A";
            
            DeviceModelText.Text = $"Model: {model}";
            AmbientModeText.Text = $"Ambient Mode: {ambient}";

            // Helper to handle visibility and text
            void SetStat(TextBlock textBlock, string label, string key, string suffix = "%")
            {
                if (values.TryGetValue(key, out var val) && int.TryParse(val, out int intVal) && intVal >= 0)
                {
                    textBlock.Visibility = Visibility.Visible;
                    textBlock.Text = $"{label}: {intVal}{suffix}";
                }
                else
                {
                    textBlock.Visibility = Visibility.Collapsed;
                }
            }

            SetStat(BatteryOverallText, "Battery", "battery");
            SetStat(BatteryLeftText, "Left Battery", "batteryLeft");
            SetStat(BatteryRightText, "Right Battery", "batteryRight");
            SetStat(BatteryCaseText, "Case Battery", "batteryCase");

            if (values.TryGetValue("charging", out var charging) && int.TryParse(charging, out int isCharging) && isCharging >= 0)
            {
                ChargingText.Visibility = Visibility.Visible;
                string c = isCharging != 0 ? "Yes" : "No";
                
                string details = "";
                if (values.TryGetValue("chargingLeft", out var cl) && int.TryParse(cl, out int icl) && icl >= 0)
                    details += $"L: {(icl != 0 ? "Yes" : "No")}, ";
                if (values.TryGetValue("chargingRight", out var cr) && int.TryParse(cr, out int icr) && icr >= 0)
                    details += $"R: {(icr != 0 ? "Yes" : "No")}";
                
                details = details.TrimEnd(' ', ',');
                ChargingText.Text = $"Charging: {c} {(string.IsNullOrEmpty(details) ? "" : $"({details})")}";
            }
            else
            {
                ChargingText.Visibility = Visibility.Collapsed;
            }
        }
    }
}
