using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NLog;
using PureManApplicationDeployment;

namespace WpfSettings;


/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public sealed partial class MainWindow
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
        // TODO: Change it!!!
        //_ClickOnce = new PureManClickOnce("http://test:8080/clickonce/");
        _ClickOnce = new PureManClickOnce(@"\\localhost\ClickOnce\WpfSettings");
    }

    private void ButtonIsNetworkInstalled_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            SetCorrectValue(LabelIsNetworkInstalled, _ClickOnce.IsNetworkDeployment.ToString());
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private async void ButtonLocalVersion_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            SetCorrectValue(LabelLocalVersion, (await _ClickOnce.CurrentVersion())?.ToString());
        }
        catch (Exception exp)
        {
            Log.Error(exp, exp.Message);
            SetCorrectValue(LabelLocalVersion);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private async void ButtonGetServerVersion_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            SetCorrectValue(LabelServerVersion, (await _ClickOnce.CachedServerVersion())?.ToString());
        }
        catch (Exception exp)
        {
            Log.Error(exp, exp.Message);
            SetCorrectValue(LabelServerVersion);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private async void ButtonUpdateAvailable_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            SetCorrectValue(LabelUpdateAvailable, (await _ClickOnce.CachedIsUpdateAvailable()).ToString());
        }
        catch (Exception exp)
        {
            Log.Error(exp, exp.Message);
            SetCorrectValue(LabelUpdateAvailable);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private async void ButtonUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            // On update don't make application restart!
            // THIS IS IMPORTANT!
            //
            // Just make shutdown of current app
            var updateResult = await _ClickOnce.UpdateAsync();
            SetCorrectValue(LabelUpdate, updateResult.ToString());

            if (updateResult)
            {
                Application.Current.Shutdown(0);
            }
        }
        catch (Exception exp)
        {
            Log.Error(exp, exp.Message);
            SetCorrectValue(LabelUpdate);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void ButtonDataDir_OnClick(object sender, RoutedEventArgs e)
    {
        Cursor = Cursors.Wait;

        try
        {
            SetCorrectValue(LabelDataDir, _ClickOnce.DataDir);
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void SetCorrectValue(ContentControl label, string value = "")
    {
        if (string.IsNullOrEmpty(value))
        {
            label.Content = "<NO DATA>";
            label.Foreground = System.Windows.Media.Brushes.Brown;
        }
        else
        {
            label.Content = value;
            label.Foreground = System.Windows.Media.Brushes.Black;
        }
    }
}
