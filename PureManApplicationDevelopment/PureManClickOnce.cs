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
    public class PureManClickOnce
    {
        private readonly bool _IsNetworkDeployment;
        private readonly string _CurrentAppName;
        private readonly string _CurrentPath;
        private readonly string _PublishPath;
        private InstallFrom _From;

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
            SetInstallFrom();
        }

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

        public bool IsNetworkDeployment => _IsNetworkDeployment;

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

        public async Task<Version> ServerVersion()
        {
            if (_From == InstallFrom.Web)
            {
                using (var client = new HttpClient {BaseAddress = new Uri(_PublishPath)})
                {
                    using (var stream = await client.GetStreamAsync($"{_CurrentAppName}.application"))
                    {
                        return await ReadServerManifest(stream);
                    }
                }
            }

            if (_From == InstallFrom.Unc)
            {
                using (var stream = File.OpenRead(Path.Combine($"{_PublishPath}", $"{_CurrentAppName}.application")))
                {
                    return await ReadServerManifest(stream);
                }
            }

            throw new ClickOnceDeploymentException("No network install was set");
        }

        private async Task<Version> ReadServerManifest(Stream stream)
        {
            var xmlDoc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
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

        public async Task<bool> UpdateAvailable()
        {
            var currentVersion = await CurrentVersion();
            var serverVersion = await ServerVersion();

            return currentVersion < serverVersion;
        }

        public async Task<bool> Update()
        {
            var currentVersion = await CurrentVersion();
            var serverVersion = await ServerVersion();

            if (currentVersion >= serverVersion)
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
                    var response = await client.GetAsync(uri);
                    using (var fs = new FileStream(setupPath, FileMode.CreateNew))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
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

            await proc.WaitForExitAsync();

            if (!string.IsNullOrEmpty(setupPath))
            {
                File.Delete(setupPath);
            }

            return true;
        }

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
                return Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")
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

        private bool CheckIsNetworkDeployment()
        {
            if (!string.IsNullOrEmpty(_CurrentPath) && _CurrentPath.Contains("AppData\\Local\\Apps"))
            {
                return true;
            }

            return false;
        }
    }
}
