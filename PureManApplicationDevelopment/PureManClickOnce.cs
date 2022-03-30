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
        /// <param name="publishPath">The publish path.</param>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find entry assembly name!</exception>
        public PureManClickOnce(string publishPath)
        {
            _PublishPath = publishPath;
            _CurrentPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            _IsNetworkDeployment = CheckIsNetworkDeployment();
            _CurrentAppName = Assembly.GetEntryAssembly()?.GetName().Name;
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
        private readonly bool _IsNetworkDeployment;
        /// <summary>
        /// The current application name
        /// </summary>
        private readonly string _CurrentAppName;
        /// <summary>
        /// The current path
        /// </summary>
        private readonly string _CurrentPath;
        /// <summary>
        /// The publish path
        /// </summary>
        private readonly string _PublishPath;
        /// <summary>
        /// The data dir
        /// </summary>
        private readonly string _DataDir;
        /// <summary>
        /// From
        /// </summary>
        private InstallFrom _From;

        #endregion

        #region < Public Properties >

        /// <summary>
        /// Gets a value indicating whether this instance is network deployment.
        /// </summary>
        /// <value><c>true</c> if this instance is network deployment; otherwise, <c>false</c>.</value>
        public bool IsNetworkDeployment => _IsNetworkDeployment;
        /// <summary>
        /// Gets the data dir.
        /// </summary>
        /// <value>The data dir.</value>
        public string DataDir => _DataDir;

        #endregion

        #region < Private Constructor Methods >

        /// <summary>
        /// Searches the application data dir.
        /// </summary>
        /// <param name="programData">The program data.</param>
        /// <param name="currentFolderName">Name of the current folder.</param>
        /// <param name="i">The i.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find data dir for {currentFolderName} in path: {programData}</exception>
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
        /// Currents the version.
        /// </summary>
        /// <returns>Task&lt;Version&gt;.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Not deployed by network!</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Application name is empty!</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't find manifest file at path {path}</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Invalid manifest document for {path}</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Version info is empty!</exception>
        public async Task<Version> CurrentVersion()
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

            return new Version(version);
        }

        /// <summary>
        /// Servers the version.
        /// </summary>
        /// <returns>Task&lt;Version&gt;.</returns>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">No network install was set</exception>
        /// <inheritdoc cref="ReadServerManifest(Stream, CancellationToken?)"/>
        public async Task<Version> ServerVersion(CancellationToken? token = null)
        {
            if (_From == InstallFrom.Web)
            {
                using (var client = new HttpClient {BaseAddress = new Uri(_PublishPath)})
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
        /// Reads the server manifest.
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
            var CToken = token ?? CancellationToken.None;
            var xmlDoc = await XDocument.LoadAsync(stream, LoadOptions.None, CToken);
            CToken.ThrowIfCancellationRequested();
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

            return new Version(version);
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
        /// <inheritdoc cref="ServerVersion(CancellationToken?)"/>
        public async Task<bool> UpdateAvailable(CancellationToken? token = null)
        {
            var currentVersion = await CurrentVersion();
            var serverVersion = await ServerVersion(token);
            if (token.IsCancellationRequested()) return false;
            return currentVersion < serverVersion;
        }


        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <returns>Task&lt;System.Boolean&gt; whose result will be TRUE if the update completed successfully.</returns>
        /// <param name="token">optional <see cref="CancellationToken"/> used to cancel the update process. Use at own risk. </param>
        /// <inheritdoc cref="remarks" path="*"/>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">No network install was set</exception>
        /// <exception cref="PureManApplicationDeployment.ClickOnceDeploymentException">Can't start update process</exception>
        /// <inheritdoc cref="ServerVersion(CancellationToken?)"/>
        public async Task<bool> Update(CancellationToken? token = null)
        {
            var currentVersion = await CurrentVersion();
            var CToken = token ?? CancellationToken.None;
            var serverVersion = await ServerVersion(CToken);

            if (currentVersion >= serverVersion | CToken.IsCancellationRequested)
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
                using (var client = new HttpClient())
                {
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

            //Ensure process is cleaned up
            if (!(proc is null))
            {
                if (!proc.HasExited) proc?.Kill();
                proc?.Dispose();
            }

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
        private void remarks() { }
#else
        private void remarks() { }
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
