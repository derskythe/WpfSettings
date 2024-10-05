// ***********************************************************************
// Assembly         : PureManApplicationDeployment
// Author           : Skif
// Created          : 02-04-2021
//
// Last Modified By : RFBomb
// Last Modified On : 03-03-2022
// ***********************************************************************
// <copyright file="PureManClickOnce.cs" company="PureManApplicationDeployment">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PureManApplicationDeployment.Helpers;
using PureManApplicationDeployment.Models;

namespace PureManApplicationDeployment;


/// <summary>
/// Class PureManClickOnce.
/// </summary>
public class PureManClickOnce : IDisposable
{
    #region < Constructor >

    /// <summary>
    /// Initializes a new instance of the <see cref="PureManClickOnce"/> class.
    /// </summary>
    /// <param name="publishPath">The path to publish - where to check for updates </param>
    /// <param name="defaultCancellationTime">Time in seconds to cancel long-running requests</param>
    /// <exception cref="ClickOnceDeploymentException">Can't find entry assembly name!</exception>
    public PureManClickOnce(string publishPath, int defaultCancellationTime = 10)
    {
        _DefaultCancellationTime = defaultCancellationTime * 1000;
        _PublishPath = publishPath;
        _CurrentPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;
        _IsNetworkDeployment = CheckIsNetworkDeployment();
        CurrentAssemblyName = Assembly.GetEntryAssembly()?.GetName(false) ?? throw new ClickOnceDeploymentException(ClickOnceResult.UnknownError, "Can't find entry assembly name!");
        _CurrentAppName = CurrentAssemblyName.Name                        ?? throw new ClickOnceDeploymentException(ClickOnceResult.UnknownError, "Can't find entry assembly name!");

        if (_IsNetworkDeployment && !string.IsNullOrEmpty(_CurrentPath))
        {
            var programData = Path.Combine
                (
                 Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 "Apps",
                 "2.0",
                 "Data"
                );

            var currentFolderName = new DirectoryInfo(_CurrentPath).Name;

            DataDir = SearchAppDataDir
                (
                 programData,
                 currentFolderName,
                 0
                );
        }
        else
        {
            DataDir = string.Empty;
        }

        SetInstallFrom();

        if (!string.IsNullOrEmpty(_CurrentAppName))
        {
            var applicationFileManifest = $"{_CurrentAppName}.exe.manifest";
            _ApplicationFileName = $"{_CurrentAppName}.application";
            _ApplicationFileNamePath = Path.Combine(_PublishPath, _ApplicationFileName);
            _ManifestPath = Path.Combine(_CurrentPath,            applicationFileManifest);
        }
        else
        {
            _ApplicationFileName = string.Empty;
            _ApplicationFileNamePath = string.Empty;
            _ManifestPath = string.Empty;
        }
    }

    #endregion

    #region < Private Fields  >

    /// <summary>
    /// An instance of the HttpClient used for making HTTP requests in the PureManClickOnce class.
    /// </summary>
    private readonly HttpClient _HttpClient = new HttpClient();

    /// <summary>
    /// A constant string representing the namespace for the Microsoft assembly schema version 1.
    /// </summary>
    private const string URN_SCHEMAS_MICROSOFT_COM_ASM_V1 = "urn:schemas-microsoft-com:asm.v1";

    /// <summary>
    /// The maximum number of inner directories to search when looking for the application data directory.
    /// </summary>
    private const int MAX_INNER_DIRS = 100;

    /// <summary>
    /// The is network deployment
    /// </summary>
    private readonly bool _IsNetworkDeployment;

    /// <summary>
    /// The current application name
    /// </summary>
    private readonly string _CurrentAppName;

    /// <summary>
    /// The path to the directory that contains the application
    /// </summary>
    private readonly string _CurrentPath;

    /// <summary>
    /// The publishing path (where to check for updates)
    /// </summary>
    private readonly string _PublishPath;

    /// <summary>
    /// From
    /// </summary>
    private InstallFrom _From;

    // This value is set when the class is statically constructed. This is to set a base value for the DateTime, assuming the app checks for updates prior to starting up.
    private static readonly DateTime _FirstAccessTime = DateTime.Now;

    /// <summary>
    /// Cached version of the local application, used to reduce the number of manifest reads.
    /// </summary>
    private Version? _CachedLocalVersion;

    /// <summary>
    /// Stores the version information of the server, cached for performance reasons.
    /// </summary>
    private Version? _CachedServerVersion;

    /// <summary>
    /// The filename of the application's deployment manifest.
    /// </summary>
    private readonly string _ApplicationFileName;

    /// <summary>
    /// The path to the application manifest file used for ClickOnce deployment
    /// </summary>
    private readonly string _ManifestPath;

    /// <summary>
    /// The file path of the application file within the publish directory.
    /// </summary>
    private readonly string _ApplicationFileNamePath;

    /// <summary>
    /// Indicates whether the current instance of the PureManClickOnce class has been disposed.
    /// </summary>
    private bool _IsDisposed;

    /// <summary>
    /// Number of milliseconds to wait for the ServerVersion to successfully read prior to cancelling the request.
    /// </summary>
    private readonly int _DefaultCancellationTime;

    #endregion

    #region < Public Properties >

    /// <summary>
    /// Access to the underlying AssemblyName object this object utilizes.
    /// </summary>
    /// <remarks>Generated during object construction</remarks>

    // ReSharper disable once MemberCanBePrivate.Global
    public AssemblyName CurrentAssemblyName { get; }

    /// <summary>
    /// Gets the current version of the deployment
    /// </summary>
    /// <returns>
    /// <br/> ELSE: <see cref="AssemblyName.Version"/>
    /// </returns>
    public async Task<Version?> CurrentVersion()
    {
        if (_CachedLocalVersion is not null)
        {
            return _CachedLocalVersion;
        }

        if (!IsNetworkDeployment)
        {
            return CurrentAssemblyName.Version;
        }

        using var cts = new CancellationTokenSource(_DefaultCancellationTime);

        return await RefreshCurrentVersion(cts.Token);
    }

    public async Task<(Version? version, ClickOnceResult result)> CurrentVersionExtended()
    {
        if (_CachedLocalVersion is not null)
        {
            return (_CachedLocalVersion, ClickOnceResult.Ok);
        }

        if (!IsNetworkDeployment)
        {
            return (CurrentAssemblyName.Version, ClickOnceResult.NoNetworkInstall);
        }

        using var cts = new CancellationTokenSource(_DefaultCancellationTime);


        return await RefreshCurrentVersionExtended(cts.Token);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is network deployment.
    /// </summary>
    /// <value><c>true</c> if this instance is network deployment; otherwise, <c>false</c>.</value>
    public bool IsNetworkDeployment => _IsNetworkDeployment;

    /// <summary>
    /// Gets the data directory path
    /// </summary>
    /// <value>The data directory</value>
    public string DataDir { get; }

    /// <summary>
    /// Gets the Web site or file share from which this application updates itself.
    /// </summary>
    /// <returns>
    /// The publishPath passed into the constructor.
    /// </returns>

    // ReSharper disable once UnusedMember.Global
    public string UpdateLocation => _PublishPath;

    /// <summary>
    /// The last time the application checked for an update.
    /// <br/>The last time <see cref="RefreshServerVersion(CancellationToken)"/> was called.
    /// </summary>

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public DateTime TimeOfLastUpdateCheckValue { get; private set; } = _FirstAccessTime;

    /// <summary>
    /// Returns that last <see cref="Version"/>
    /// . <br/>
    /// If it has not been read yet, try reading it synchronously (timeout by default after 3 sec).
    /// <see cref="PureManClickOnce"/> constuctor for more information.
    /// </summary>
    /// <remarks>
    /// Should only be checked if <see cref="IsNetworkDeployment"/> == true
    /// </remarks>
    /// <returns>
    /// If call to <c>ServerVersion</c> was successfully, return the last read <see cref="Version"/> object. <br/>
    /// Otherwise, return null.
    /// </returns>
    public async Task<Version?> CachedServerVersionAsync()
    {
        if (_CachedServerVersion is not null)
        {
            return _CachedServerVersion;
        }

        try
        {
            using var cts = new CancellationTokenSource(_DefaultCancellationTime);
            await RefreshServerVersion(cts.Token);
        }
        catch (Exception exp)
        {
            Console.WriteLine(exp.Message);
        }

        return _CachedServerVersion;
    }

    /// <summary>
    /// Value indicating if <see cref="RefreshServerVersion(CancellationToken)"/> had run successfully.
    /// </summary>
    /// <remarks>If this is false, then <see cref="CachedServerVersionAsync"/> and <see cref="TimeOfLastUpdateCheckValue"/> are
    /// set up to default values.</remarks>

    // ReSharper disable once MemberCanBePrivate.Global
    public bool ServerVersionCheckedSuccessfully => _CachedServerVersion != null;

    /// <summary>
    /// Compare the <see cref="CachedServerVersionAsync"/> and the <see cref="CurrentVersion"/>
    /// </summary>
    /// <returns>
    /// TRUE if ( <see cref="IsNetworkDeployment"/> == TRUE &amp;&amp; <see cref="CachedServerVersionAsync"/> &gt;
    /// <see cref="CurrentVersion"/> ) <br/>
    /// Otherwise : FALSE.
    /// </returns>
    public async Task<bool> CachedIsUpdateAvailableAsync()
    {
        return IsNetworkDeployment && ServerVersionCheckedSuccessfully && _CachedServerVersion > await CurrentVersion();
    }

    #endregion

    #region < Private Constructor Methods >

    /// <summary>
    /// Searches the application data dir.
    /// </summary>
    /// <param name="programData">The program data.</param>
    /// <param name="currentFolderName">Name of the current folder.</param>
    /// <param name="i">The i.</param>
    /// <returns>System.String.</returns>
    /// <exception cref="ClickOnceDeploymentException">Can't find data dir for {currentFolderName}
    /// in path: {programData}</exception>
    private static string SearchAppDataDir
    (
        string programData,
        string currentFolderName,
        int i
    )
    {
        i++;

        if (i > MAX_INNER_DIRS)
        {
            throw new ClickOnceDeploymentException(ClickOnceResult.CannotFindDirectory, $"Can't find data dir for {currentFolderName} in path: {programData}");
        }

        var subdirectoryEntries = Directory.GetDirectories(programData);
        var result = string.Empty;

        foreach (var dir in subdirectoryEntries)
        {
            if (dir.Contains(currentFolderName))
            {
                result = Path.Combine(dir, "Data");

                break;
            }

            result = SearchAppDataDir
                (
                 Path.Combine(programData, dir),
                 currentFolderName,
                 i
                );

            if (!string.IsNullOrEmpty(result))
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Sets an install directory from.
    /// </summary>
    private void SetInstallFrom()
    {
        if (_IsNetworkDeployment && !string.IsNullOrEmpty(_PublishPath))
        {
            _From = _PublishPath.StartsWith("http", StringComparison.InvariantCulture) ? InstallFrom.Web : InstallFrom.Unc;
        }
        else
        {
            _From = InstallFrom.NoNetwork;
        }
    }

    #endregion

    #region < Read Version Information >

    /// <summary>
    /// Read the local manifest file to retrieve the Version information.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Task&lt;Version&gt;.</returns>
    /// <exception cref="ClickOnceDeploymentException">Not deployed by network!</exception>
    /// <exception cref="ClickOnceDeploymentException">Application name is empty!</exception>
    /// <exception cref="ClickOnceDeploymentException">Can't find manifest file at path {path}</exception>
    /// <exception cref="ClickOnceDeploymentException">Invalid manifest document for {path}</exception>
    /// <exception cref="ClickOnceDeploymentException">Version info is empty!</exception>

    // ReSharper disable once MemberCanBePrivate.Global
    public async Task<Version?> RefreshCurrentVersion(CancellationToken cancellationToken = default(CancellationToken))
    {
        var (version, result) = await RefreshCurrentVersionExtended(cancellationToken);

        ThrowClickOnceDeploymentException(result);

        return version;
    }

    private void ThrowClickOnceDeploymentException(ClickOnceResult result)
    {
        switch (result)
        {
            case ClickOnceResult.NoNetworkInstall:
                throw new ClickOnceDeploymentException(result, "Not deployed by network!");

            case ClickOnceResult.ApplicationNameIsEmpty:
                throw new ClickOnceDeploymentException(result, "Application name is empty!");

            case ClickOnceResult.ManifestNotFound:
                throw new ClickOnceDeploymentException(result, $"Can't find manifest file at path {_ManifestPath}");

            case ClickOnceResult.CannotProcessManifest:
                throw new ClickOnceDeploymentException(result, $"Invalid manifest document for {_ManifestPath}");

            case ClickOnceResult.VersionIsEmpty:
                throw new ClickOnceDeploymentException(result, "Version info is empty!");

            case ClickOnceResult.TimeoutOccured:
                throw new ClickOnceDeploymentException(result, "Timeout occured!");

            case ClickOnceResult.NoUpdate:
                throw new ClickOnceDeploymentException(result, "No update available!");

            case ClickOnceResult.VersionCheckError:
                throw new ClickOnceDeploymentException(result, "Error during version check!");

            case ClickOnceResult.ErrorProcessNotStarted:
                throw new ClickOnceDeploymentException(result, "Can't start update process!");

            case ClickOnceResult.RunningTimeoutError:
                throw new ClickOnceDeploymentException(result, "Running timeout error!");

            case ClickOnceResult.UnknownError:
                throw new ClickOnceDeploymentException(result, "Unknown error!");
        }
    }

    public async Task<(Version? version, ClickOnceResult result)> RefreshCurrentVersionExtended(CancellationToken cancellationToken)
    {
        if (!IsNetworkDeployment)
        {
            return (null, ClickOnceResult.NoNetworkInstall);
        }

        if (string.IsNullOrEmpty(_CurrentAppName))
        {
            return (null, ClickOnceResult.ApplicationNameIsEmpty);
        }

        if (!File.Exists(_ManifestPath))
        {
            return (null, ClickOnceResult.ManifestNotFound);
        }

        XElement? xmlElement;

        try
        {
            var fileContent = await File.ReadAllTextAsync(_ManifestPath, cancellationToken);
            var xmlDoc = XDocument.Parse(fileContent, LoadOptions.None);
            XNamespace nsSys = URN_SCHEMAS_MICROSOFT_COM_ASM_V1;

            xmlElement = xmlDoc.Descendants(nsSys + "assemblyIdentity").FirstOrDefault();
        }
        catch (Exception)
        {
            return (null, ClickOnceResult.CannotProcessManifest);
        }

        if (xmlElement == null)
        {
            return (null, ClickOnceResult.CannotProcessManifest);
        }

        var version = xmlElement.Attribute("version")?.Value;

        if (string.IsNullOrEmpty(version))
        {
            return (null, ClickOnceResult.VersionIsEmpty);
        }

        _CachedLocalVersion = new Version(version);

        return (_CachedLocalVersion, ClickOnceResult.Ok);
    }

    /// <summary>
    /// Servers the version.
    /// </summary>
    /// <returns>Task&lt;Version&gt;.</returns>
    /// <exception cref="ClickOnceDeploymentException">No network install was set</exception>

    // ReSharper disable once MemberCanBePrivate.Global
    public async Task<Version?> RefreshServerVersion(CancellationToken cancellationToken = default(CancellationToken))
    {
        var (version, result) = await RefreshServerVersionExtended(cancellationToken);
        ThrowClickOnceDeploymentException(result);

        return version;
    }

    public async Task<(Version? version, ClickOnceResult result)> RefreshServerVersionExtended(CancellationToken cancellationToken = default(CancellationToken))
    {
        if (_From == InstallFrom.Web)
        {
            _HttpClient.BaseAddress = new Uri(_PublishPath);
            await using var stream = await _HttpClient.GetStreamAsync(_ApplicationFileName, cancellationToken);

            return await ReadServerManifest(stream, cancellationToken);
        }

        if (_From != InstallFrom.Unc)
        {
            return (null, ClickOnceResult.NoNetworkInstall);
        }

        await using (var stream = File.OpenRead(_ApplicationFileNamePath))
        {
            return await ReadServerManifest(stream, cancellationToken);
        }
    }

    /// <summary>
    /// Reads the server manifest. <br/>
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> used to cancel the request to read the server manifest.</param>
    /// <returns>Task&lt;Version&gt;.</returns>
    /// <exception cref="ClickOnceDeploymentException">Invalid manifest document for {_CurrentAppName}.application</exception>
    /// <exception cref="ClickOnceDeploymentException">Version info is empty!</exception>
    /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested"/>
    /// <inheritdoc cref="Version"/>
    private async Task<(Version? version, ClickOnceResult result)> ReadServerManifest(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
    {
        XElement? xmlElement;

        try
        {
            var xmlDoc = await XDocument.LoadAsync
                             (
                              stream,
                              LoadOptions.None,
                              cancellationToken
                             );

            cancellationToken.ThrowIfCancellationRequested();
            XNamespace nsSys = URN_SCHEMAS_MICROSOFT_COM_ASM_V1;

            xmlElement = xmlDoc.Descendants(nsSys + "assemblyIdentity").FirstOrDefault();
        }
        catch (Exception)
        {
            return (null, ClickOnceResult.UnknownError);
        }


        if (xmlElement == null)
        {
            return (null, ClickOnceResult.ManifestNotFound);
        }

        var version = xmlElement.Attribute("version")?.Value;

        if (string.IsNullOrEmpty(version))
        {
            return (null, ClickOnceResult.VersionIsEmpty);
        }

        _CachedServerVersion = new Version(version);
        TimeOfLastUpdateCheckValue = DateTime.Now;

        return (_CachedServerVersion, ClickOnceResult.Ok);
    }

    #endregion

    #region < Check For & Update >

    /// <summary>
    /// Compares the CurrentVersion and the ServerVersion task results.
    /// </summary>
    /// <returns>
    /// Task&lt;System.Boolean&gt; <br/>
    /// <c>TRUE</c> if currentVersion &lt; serverVersion, otherwise <c>FALSE.</c>
    /// </returns>
    /// <inheritdoc cref="RefreshServerVersion(CancellationToken)"/>
    public async Task<bool> CheckUpdateAvailableAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        var currentVersion = await RefreshCurrentVersionExtended(cancellationToken);
        var serverVersion = await RefreshServerVersionExtended(cancellationToken);

        if (currentVersion.result != ClickOnceResult.Ok || serverVersion.result != ClickOnceResult.Ok)
        {
            return false;
        }

        return currentVersion.version < serverVersion.version;
    }

    /// <summary>
    /// Updates this instance.
    /// </summary>
    /// <returns><see cref="Task"/>&lt;<see cref="bool"/>&gt; whose result will be TRUE if the update completed successfully.</returns>
    /// <param name="cancellationToken">optional <see cref="CancellationToken"/> used to cancel the update process. Use at own risk. </param>
    /// <exception cref="ClickOnceDeploymentException">No network install was set</exception>
    /// <exception cref="ClickOnceDeploymentException">Can't start update process</exception>
    /// <inheritdoc cref="RefreshServerVersion(CancellationToken)"/>
    [SupportedOSPlatform("windows")]
    public async Task<ClickOnceResult> UpdateAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        Version? serverVersion;

        try
        {
            if (!await CheckUpdateAvailableAsync(cancellationToken))
            {
                return ClickOnceResult.NoUpdate;
            }

            serverVersion = _CachedServerVersion;
        }
        catch (Exception)
        {
            return ClickOnceResult.VersionCheckError;
        }

        Process? proc = null;
        var setupPath = string.Empty;

        try
        {
            if (_From == InstallFrom.Web)
            {
                var uri = _PublishPath[..^1] == "/" ? new Uri($"{_PublishPath}setup.exe") : new Uri($"{_PublishPath}/setup.exe");
                setupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"setup{serverVersion}.exe");
                var response = await _HttpClient.GetAsync(uri, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return ClickOnceResult.TimeoutOccured;
                }

                //cancellationToken.ThrowIfCancellationRequested();

                await using (var fs = new FileStream(setupPath, FileMode.CreateNew))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken);
                }

                // Last chance to prevent the process starting
                if (cancellationToken.IsCancellationRequested)
                {
                    return ClickOnceResult.TimeoutOccured;
                }

                proc = OpenUrl(setupPath);
            }
            else if (_From == InstallFrom.Unc)
            {
                proc = OpenUrl(_ApplicationFileNamePath);
            }
            else
            {
                // throw new ClickOnceDeploymentException("No network install was set");
                return ClickOnceResult.NoNetworkInstall;
            }

            if (proc == null)
            {
                return ClickOnceResult.ErrorProcessNotStarted;

                //throw new ClickOnceDeploymentException("Can't start update process");
            }

            await proc.WaitForExitAsync(cancellationToken);

            // Ensure process is cleaned up
            // Proc is not null, nobody set it to null before by @DerSkythe
            if (!proc.HasExited)
            {
                proc.Kill();

                return ClickOnceResult.RunningTimeoutError;
            }
        }
        catch (Exception)
        {
            return ClickOnceResult.UnknownError;
        }
        finally
        {
            proc?.Dispose();
        }

        //}

        if (!string.IsNullOrEmpty(setupPath))
        {
            File.Delete(setupPath);
        }

        return ClickOnceResult.Ok;
    }

    /// <summary>
    /// Opens the URL.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>Process.</returns>
    [SupportedOSPlatform("windows")]
    private static Process? OpenUrl(string url)
    {
        try
        {
            var info = new ProcessStartInfo(url)
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                ErrorDialog = true,
                LoadUserProfile = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), };

            return Process.Start(info);
        }
        catch (Exception exp)
        {
            Console.WriteLine(exp.Message);

            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            url = url.Replace("&", "^&");
            return Process.Start
                (
                 new ProcessStartInfo("cmd")
                 {
                     CreateNoWindow = true,
                     ArgumentList =
                     {
                         "/c",
                         "start",
                         url, },
                     WindowStyle = ProcessWindowStyle.Hidden,
                     RedirectStandardInput = true,
                     RedirectStandardOutput = false,
                     UseShellExecute = false,
                     LoadUserProfile = true,
                     WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     ErrorDialog = true, }
                );
        }
    }

    /// <summary>
    /// Checks the is network deployment.
    /// </summary>
    /// <returns><c>true</c> if <c>CurrentPath</c>, <c>false</c> otherwise.</returns>
    private bool CheckIsNetworkDeployment()
    {
        return !string.IsNullOrEmpty(_CurrentPath) && _CurrentPath.Contains(@"AppData\Local\Apps");
    }

    #endregion

    #region Implementation of IDisposable

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_IsDisposed)
        {
            return;
        }

        _IsDisposed = true;
        _HttpClient.Dispose();
    }

    #endregion
}
