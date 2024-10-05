namespace PureManApplicationDeployment.Models;


public enum ClickOnceResult
{
    UnknownError,
    Ok,
    NoUpdate,
    UpdateSuccessful,
    VersionCheckError,
    TimeoutOccured,
    NoNetworkInstall,
    ErrorProcessNotStarted,
    RunningTimeoutError,
    ApplicationNameIsEmpty,
    ManifestNotFound,
    CannotProcessManifest,
    VersionIsEmpty,
    CannotFindDirectory,
}
