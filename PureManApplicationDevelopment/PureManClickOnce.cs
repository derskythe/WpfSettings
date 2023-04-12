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
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Syroot.Windows.IO;

namespace PureManApplicationDeployment
{
    /// <summary>
    /// Class PureManClickOnce.
    /// </summary>
    public class PureManClickOnce
    {
        #region < Constructor >

        /// <summary>
        /// Initializes a new instance of the <see cref="PureManClickOnce"/> class.
        /// </summary>
        /// <param name="publishPath">The publish path - where to check for updates </param>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find entry assembly name!</exception>
        public PureManClickOnce(string publishPath)
        {
            _PublishPath = publishPath;
            _CurrentPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            _IsNetworkDeployment = CheckIsNetworkDeployment();
            CurrentAssemblyName = Assembly.GetEntryAssembly()?.GetName(false);
            _CurrentAppName = CurrentAssemblyName?.Name;
            if (string.IsNullOrEmpty(_CurrentAppName))
            {
                throw new ClickOnceDeploymentException("Can't find entry assembly name!");
            }

            if (_IsNetworkDeployment && !string.IsNullOrEmpty(_CurrentPath))
            {
                var programData = Path.Combine(KnownFolders.LocalAppData.Path, @"Apps\2.0\Data\");
                var currentFolderName = new DirectoryInfo(_CurrentPath).Name;
                _DataDir = SearchAppDataDir(programData, currentFolderName, 0);
            }
            else
            {
                _DataDir = string.Empty;
            }

            SetInstallFrom();
        }

        #endregion

        #region < Private Fields  >

        /// <summary>
        /// The is network deployment
        /// </summary>
        private bool _IsNetworkDeployment { get; }

        /// <summary>
        /// The current application name
        /// </summary>
        private string _CurrentAppName { get; }

        /// <summary>
        /// The path to the directory that contains the application
        /// </summary>
        private string _CurrentPath { get; }

        /// <summary>
        /// The publish path (where to check for updates)
        /// </summary>
        private string _PublishPath { get; }

        /// <summary>
        /// The data dir
        /// </summary>
        private string _DataDir { get; }

        /// <summary>
        /// From
        /// </summary>
        private InstallFrom _From;

        // This value is set when the class is statically constructed. This is to set a base value for the DateTime, assuming the app checks for updates prior to starting up.
        private static readonly DateTime FirstAccessTime = DateTime.Now;
        private Version _CachedLocalVersion;
        private Version _CachedServerVersion;

        #endregion

        #region < Public Properties >

        /// <summary>
        /// Access to the underlying AssemblyName object this object utilizes.
        /// </summary>
        /// <remarks>Generated during object construction</remarks>
        public AssemblyName CurrentAssemblyName { get; }

        /// <summary>
        /// Gets the current version of the deployment
        /// </summary>
        /// <returns>
        /// If <see cref="IsNetworkDeployment"/> : The last read <see cref="Version"/> read by <see cref="RefreshCurrentVersion"/>
        /// <br/> ELSE: <see cref="AssemblyName.Version"/>
        /// </returns>
        public Version CurrentVersion
        {
            get
            {
                if (_CachedLocalVersion is { })
                {
                    return _CachedLocalVersion;
                }

                if (!IsNetworkDeployment)
                {
                    return CurrentAssemblyName.Version;
                }

                return RefreshCurrentVersion().Result;
            }
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
        public string DataDir => _DataDir;

        /// <summary>
        /// Gets the Web site or file share from which this application updates itself.
        /// </summary>
        /// <returns>
        /// The publishPath passed into the constructor.
        /// </returns>
        public string UpdateLocation => _PublishPath;

        /// <summary>
        /// The last time the application checked for an update.
        /// <br/>The last time <see cref="RefreshServerVersion(CancellationToken?)"/> was called.
        /// </summary>
        public DateTime TimeOfLastUpdateCheckValue { get; private set; } = FirstAccessTime;

        /// <summary>
        /// Number of milliseconds to wait for the ServerVersion to successfully read prior to cancelling the request.
        /// </summary>
        public static int DefaultCancellationTime { get; set; } = 3000;

        /// <summary>
        /// Returns that last <see cref="Version"/> read by <see cref="ServerVersion(CancellationToken?)"/>. <br/>
        /// If it has not been read yet, try reading it synchronously (timeout after <see cref="DefaultCancellationTime"/> ms has elapsed).
        /// </summary>
        /// <remarks>
        /// Should only be checked if <see cref="IsNetworkDeployment"/> == true
        /// </remarks>
        /// <returns>
        /// If <see cref="ServerVersion(CancellationToken?)"/> was successfull, return the last read <see cref="Version"/> object. <br/>
        /// Otherwise return null.
        /// </returns>
        public Version CachedServerVersion
        {
            get
            {
                if (_CachedServerVersion is { })
                {
                    return _CachedServerVersion;
                }

                try
                {
                    RefreshServerVersion(new CancellationTokenSource(DefaultCancellationTime).Token).Wait();
                }
                catch
                {
                    // silent catch
                }

                return _CachedServerVersion;
            }
        }

        /// <summary>
        /// Value indicating if <see cref="RefreshServerVersion(CancellationToken?)"/> had run successfully.
        /// </summary>
        /// <remarks>If this is false, then <see cref="CachedServerVersion"/> and <see cref="TimeOfLastUpdateCheckValue"/> are
        /// set up to default values.</remarks>
        public bool ServerVersionCheckedSuccessfully => _CachedServerVersion != null;

        /// <summary>
        /// Compare the <see cref="CachedServerVersion"/> and the <see cref="CurrentVersion"/>
        /// </summary>
        /// <returns>
        /// TRUE if ( <see cref="IsNetworkDeployment"/> == TRUE &amp;&amp; <see cref="CachedServerVersion"/> &gt; <see cref="CurrentVersion"/> ) <br/>
        /// Otherwise : FALSE.
        /// </returns>
        public bool CachedIsUpdateAvailable =>
            IsNetworkDeployment && ServerVersionCheckedSuccessfully && _CachedServerVersion > CurrentVersion;

        #endregion

        #region < Private Constructor Methods >

        /// <summary>
        /// Searches the application data dir.
        /// </summary>
        /// <param name="programData">The program data.</param>
        /// <param name="currentFolderName">Name of the current folder.</param>
        /// <param name="i">The i.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find data dir for {currentFolderName}
        /// in path: {programData}</exception>
        private string SearchAppDataDir(string programData, string currentFolderName, int i)
        {
            i++;
            if (i > 100)
            {
                throw new ClickOnceDeploymentException($"Can't find data dir for {currentFolderName} in path: {programData}");
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

                result = SearchAppDataDir(Path.Combine(programData, dir), currentFolderName, i);
                if (!string.IsNullOrEmpty(result))
                {
                    break;
                }
            }

            return result;
        }


        /// <summary>
        /// Sets the install from.
        /// </summary>
        private void SetInstallFrom()
        {
            if (_IsNetworkDeployment && !string.IsNullOrEmpty(_PublishPath))
            {
                _From = _PublishPath.StartsWith("http") ? InstallFrom.Web : InstallFrom.Unc;
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
        /// <returns>Task&lt;Version&gt;.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Not deployed by network!</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Application name is empty!</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find manifest file at path {path}</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Invalid manifest document for {path}</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Version info is empty!</exception>
        public async Task<Version> RefreshCurrentVersion()
        {
            if (!IsNetworkDeployment)
            {
                throw new ClickOnceDeploymentException("Not deployed by network!");
            }

            if (string.IsNullOrEmpty(_CurrentAppName))
            {
                throw new ClickOnceDeploymentException("Application name is empty!");
            }

            var path = Path.Combine(_CurrentPath, $"{_CurrentAppName}.exe.manifest");
            if (!File.Exists(path))
            {
                throw new ClickOnceDeploymentException($"Can't find manifest file at path {path}");
            }

            var fileContent = await File.ReadAllTextAsync(path);
            var xmlDoc = XDocument.Parse(fileContent, LoadOptions.None);
            XNamespace nsSys = "urn:schemas-microsoft-com:asm.v1";
            var xmlElement = xmlDoc.Descendants(nsSys + "assemblyIdentity").FirstOrDefault();

            if (xmlElement == null)
            {
                throw new ClickOnceDeploymentException($"Invalid manifest document for {path}");
            }

            var version = xmlElement.Attribute("version")?.Value;
            if (string.IsNullOrEmpty(version))
            {
                throw new ClickOnceDeploymentException("Version info is empty!");
            }

            _CachedLocalVersion = new Version(version);
            return _CachedLocalVersion;
        }

        /// <summary>
        /// Servers the version.
        /// </summary>
        /// <returns>Task&lt;Version&gt;.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">No network install was set</exception>
        /// <inheritdoc cref="ReadServerManifest(Stream, CancellationToken?)"/>
        public async Task<Version> RefreshServerVersion(CancellationToken? token = default)
        {
            if (_From == InstallFrom.Web)
            {
                using (var client = new HttpClient { BaseAddress = new Uri(_PublishPath) })
                {
                    using (var stream = await client.GetStreamAsync($"{_CurrentAppName}.application"))
                    {
                        return await ReadServerManifest(stream, token);
                    }
                }
            }

            if (_From == InstallFrom.Unc)
            {
                using (var stream = File.OpenRead(Path.Combine($"{_PublishPath}", $"{_CurrentAppName}.application")))
                {
                    return await ReadServerManifest(stream, token);
                }
            }

            throw new ClickOnceDeploymentException("No network install was set");
        }

        /// <summary>
        /// Reads the server manifest. <br/>
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="token"><see cref="CancellationToken"/> used to cancel the request to read the server manifest.</param>
        /// <returns>Task&lt;Version&gt;.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Invalid manifest document for {_CurrentAppName}.application</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Version info is empty!</exception>
        /// <inheritdoc cref="CancellationToken.ThrowIfCancellationRequested"/>
        /// <inheritdoc cref="Version.Version(string)"/>
        private async Task<Version> ReadServerManifest(Stream stream, CancellationToken? token)
        {
            var cToken = token ?? new CancellationTokenSource(DefaultCancellationTime).Token;
            var xmlDoc = await XDocument.LoadAsync(stream, LoadOptions.None, cToken);
            cToken.ThrowIfCancellationRequested();
            XNamespace nsSys = "urn:schemas-microsoft-com:asm.v1";
            var xmlElement = xmlDoc.Descendants(nsSys + "assemblyIdentity").FirstOrDefault();

            if (xmlElement == null)
            {
                throw new ClickOnceDeploymentException($"Invalid manifest document for {_CurrentAppName}.application");
            }

            var version = xmlElement.Attribute("version")?.Value;
            if (string.IsNullOrEmpty(version))
            {
                throw new ClickOnceDeploymentException($"Version info is empty!");
            }

            _CachedServerVersion = new Version(version);
            TimeOfLastUpdateCheckValue = DateTime.Now;
            return _CachedServerVersion;
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
        /// <inheritdoc cref="RefreshServerVersion(CancellationToken?)"/>
        public async Task<bool> CheckUpdateAvailableAsync(CancellationToken? token = default)
        {
            var currentVersion = await RefreshCurrentVersion();
            var serverVersion = await RefreshServerVersion(token);
            if (token.IsCancellationRequested())
            {
                return false;
            }
            return currentVersion < serverVersion;
        }


        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <returns><see cref="Task"/>&lt;<see cref="bool"/>&gt; whose result will be TRUE if the update completed successfully.</returns>
        /// <param name="token">optional <see cref="CancellationToken"/> used to cancel the update process. Use at own risk. </param>
        /// <inheritdoc cref="UpdateAsyncRemarks" path="*"/>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">No network install was set</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't start update process</exception>
        /// <inheritdoc cref="RefreshServerVersion(CancellationToken?)"/>
        public async Task<bool> UpdateAsync(CancellationToken? token = default)
        {
            var currentVersion = await RefreshCurrentVersion();
            var CToken = token ?? CancellationToken.None;
            var serverVersion = await RefreshServerVersion(CToken);

            if ((currentVersion >= serverVersion) | CToken.IsCancellationRequested)
            {
                // Nothing to update
                return false;
            }

            Process proc;
            string setupPath = null;
            if (_From == InstallFrom.Web)
            {
                var downLoadFolder = KnownFolders.Downloads;
                var uri = new Uri($"{_PublishPath}setup.exe");
                setupPath = Path.Combine(downLoadFolder.Path, $"setup{serverVersion}.exe");
                // TODO: Must be HttpClientFactory without disposing by @DerSkythe
                var client = new HttpClient();
                var response = await client.GetAsync(uri, token ?? CancellationToken.None);
                token.ThrowIfCancellationRequested();

                using (var fs = new FileStream(setupPath, FileMode.CreateNew))
                {
#if NET5_0_OR_GREATER
                    await response.Content.CopyToAsync(fs, CToken);
#else
                    // this action is not cancellable -> overload does no exist!
                    var task = response.Content.CopyToAsync(fs);
#endif
                }

                token.ThrowIfCancellationRequested(); // Last chance to prevent the process starting
                proc = OpenUrl(setupPath);
            }
            else if (_From == InstallFrom.Unc)
            {
                proc = OpenUrl(Path.Combine($"{_PublishPath}", $"{_CurrentAppName}.application"));
            }
            else
            {
                throw new ClickOnceDeploymentException("No network install was set");
            }

            if (proc == null)
            {
                throw new ClickOnceDeploymentException("Can't start update process");
            }

            await proc.WaitForExitAsync(CToken);

            // Ensure process is cleaned up
            // Proc is not null, nobody set it to null before by @DerSkythe
            //if (!(proc is null))
            //{
            if (!proc.HasExited)
            {
                proc.Kill();
            }

            proc.Dispose();
            //}

            if (!string.IsNullOrEmpty(setupPath))
            {
                File.Delete(setupPath);
            }

            return true;
        }

#if NETCOREAPP3_1
        /// <remarks>
        /// If installing from the web and using .NetCoreApp3.1, the stream download is unable to be cancelled due to no support for CopyToAsync overload with a cancellation token. <br/>
        /// If cancellation is requested during the download, it will throw prior to the process that triggers the update being started.
        /// </remarks>
        private void UpdateAsyncRemarks()
        {
        }
#else
        private void UpdateAsyncRemarks() { }
#endif

        /// <summary>
        /// Opens the URL.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <returns>Process.</returns>
        private static Process OpenUrl(string url)
        {
            try
            {
                var info = new ProcessStartInfo(url)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false
                };
                var proc = Process.Start(info);
                return proc;
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                url = url.Replace("&", "^&");
                return Process.Start(new ProcessStartInfo("cmd", $"/c start \"\"{url}\"\"")
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = false,
                        UseShellExecute = false,
                    }
                );
            }
        }

        /// <summary>
        /// Checks the is network deployment.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CheckIsNetworkDeployment()
        {
            if (!string.IsNullOrEmpty(_CurrentPath) && _CurrentPath.Contains("AppData\\Local\\Apps"))
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
