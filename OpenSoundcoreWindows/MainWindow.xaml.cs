using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace OpenSoundcoreWindows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public Frame ContentFrameRef
        {
            get { return ContentFrame; }
        }

        public MainWindow()
        {
            InitializeComponent();
            
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey("LastConnectedMac") && settings.Values.ContainsKey("LastConnectedName"))
            {
                ContentFrame.Navigate(typeof(AutoConnect));
            }
            else
            {
                ContentFrame.Navigate(typeof(ConnectPage));
            }
            
            this.ExtendsContentIntoTitleBar = true; // Extend the content into the title bar and hide the default titlebar
            this.SetTitleBar(titleBar); // Set the custom title bar
            this.Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // Refresh content if we are on a page that supports it
                if (ContentFrame.Content is DevicePage devicePage)
                {
                    devicePage.RefreshStats();
                }
                else if (ContentFrame.Content is AboutPage aboutPage)
                {
                    aboutPage.RefreshStats();
                }
            }
        }

        public void SetNavVisibility(bool visible)
        {
            NavView.IsPaneVisible = visible;
            NavView.IsPaneToggleButtonVisible = visible;
            
            if (visible)
            {
                NavView.IsPaneOpen = true;
                NavView.SelectedItem = deviceItem;
            }
        }

        private void NavView_SelectionChanged(NavigationView sender,
    NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();

            Type pageType = tag switch
            {
                "device" => typeof(DevicePage),
                "about" => typeof(AboutPage),
                _ => typeof(DevicePage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }
    }
}
