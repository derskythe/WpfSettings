﻿using System;
using System.Diagnostics;
using System.Windows;
using NLog;
using PureManApplicationDeployment;

namespace WpfSettings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        // ReSharper disable InconsistentNaming
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        // ReSharper restore InconsistentNaming
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private PureManClickOnce _ClickOnce;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _ClickOnce = new PureManClickOnce("http://test:8080/clickonce/");
        }

        private void ButtonIsNetworkInstalled_OnClick(object sender, RoutedEventArgs e)
        {
            LabelIsNetworkInstalled.Content = _ClickOnce.IsNetworkDeployment;
        }

        private void ButtonLocalVersion_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LabelLocalVersion.Content = _ClickOnce.CurrentVersion;
            }
            catch (Exception exp)
            {
                Log.Error(exp, exp.Message);
            }
        }

        private void ButtonGetServerVersion_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LabelServerVersion.Content = _ClickOnce.CachedServerVersion;
            }
            catch (Exception exp)
            {
                Log.Error(exp, exp.Message);
            }
        }

        private void ButtonUpdateAvailable_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LabelUpdateAvailable.Content = _ClickOnce.CachedIsUpdateAvailable;
            }
            catch (Exception exp)
            {
                Log.Error(exp, exp.Message);
            }
        }

        private async void ButtonUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // On update don't make application restart! This is important!
                // Just make shutdown of current app
                var updateResult = await _ClickOnce.UpdateAsync();
                LabelUpdate.Content = updateResult;
                if (updateResult)
                {
                    Application.Current.Shutdown(0);
                }
            }
            catch (Exception exp)
            {
                Log.Error(exp, exp.Message);
            }
        }

        private void ButtonDataDir_OnClick(object sender, RoutedEventArgs e)
        {
            LabelDataDir.Content = _ClickOnce.DataDir;
        }
    }
}
