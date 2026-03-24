using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace fiskaltrust.Launcher.Common.Extensions;

public static partial class LoggerExtensions
{
    #region Configuration

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuration '{configName}' is deprecated and will be removed in future.")]
    public static partial void ConfigurationDeprecated(this ILogger logger, string configName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Launcher configuration file {file} does not exist.")]
    public static partial void LauncherConfigFileNotExist(this ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Launcher configuration file {file} does not exist. Creating new file.")]
    public static partial void LauncherConfigFileNotExistCreating(this ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Information, Message = "Set values in launcher configuration file {file}.")]
    public static partial void SetValuesInLauncherConfig(this ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not read launcher configuration {file}.")]
    public static partial void CouldNotReadLauncherConfig(this ILogger logger, Exception? exception, string file);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not read launcher configuration.")]
    public static partial void CouldNotReadLauncherConfigGeneral(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error reserialising launcher configuration file.")]
    public static partial void ErrorReserialisingLauncherConfig(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not write launcher configuration.")]
    public static partial void CouldNotWriteLauncherConfig(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error decrypting launcher configuration file {file}.")]
    public static partial void ErrorDecryptingLauncherConfig(this ILogger logger, Exception? exception, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error encrypting launcher configuration file.")]
    public static partial void ErrorEncryptingLauncherConfig(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to encrypt value of configuration field {name}. Consider using the 'config set' command to set the field's value.")]
    public static partial void FailedToEncryptConfigField(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to decrypt value of configuration field {name}. Consider using the 'config set' command to set the field's value.")]
    public static partial void FailedToDecryptConfigField(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Please specify the --access-token parameter or an existing launcher configuration file containing an access token.")]
    public static partial void SpecifyAccessTokenParameter(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Please specify the --access-token parameter or set it in the provided launcher configuration file.")]
    public static partial void SpecifyAccessTokenInConfig(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "To decrypt the encrypted values from the configuration file specify the --access-token parameter or set it in the provided launcher configuration file.")]
    public static partial void ToDecryptSpecifyAccessToken(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "CashBoxId and AccessToken are not provided.")]
    public static partial void CashboxIdAndAccessTokenNotProvided(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cashbox configuration was not downloaded because UseOffline is set.")]
    public static partial void CashboxConfigNotDownloadedOfflineMode(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Launcher Configuration File: {launcherConfigurationFile}")]
    public static partial void LauncherConfigFileDebug(this ILogger logger, string launcherConfigurationFile);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cashbox Configuration File: {cashboxConfigurationFile}")]
    public static partial void CashboxConfigFileDebug(this ILogger logger, string cashboxConfigurationFile);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Launcher running as {serviceType}")]
    public static partial void LauncherRunningAsServiceType(this ILogger logger, string serviceType);

    #endregion

    #region Legacy Configuration

    [LoggerMessage(Level = LogLevel.Error, Message = "Error when reading legacy config file {path}.")]
    public static partial void ErrorReadingLegacyConfig(this ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Read legacy config file {path}.")]
    public static partial void ReadLegacyConfig(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The legacy configuration option '{key}' cannot be automatically parsed. Please use the '{executable} config set --help' argument to list compatible options.")]
    public static partial void LegacyConfigOptionCannotBeParsed(this ILogger logger, string key, string executable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Proxy settings can currently not be migrated from legacy config files. Please set the proxy with the '{executable} config set --proxy' command.")]
    public static partial void ProxySettingsCannotBeMigrated(this ILogger logger, string executable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not parse legacy configuration file \"{legacyConfigurationFile}\".")]
    public static partial void CouldNotParseLegacyConfig(this ILogger logger, Exception? exception, string legacyConfigurationFile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not parse launcher configuration file \"{launcherConfigurationFile}\".")]
    public static partial void CouldNotParseLauncherConfigFile(this ILogger logger, Exception? exception, string launcherConfigurationFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local configuration {launcherConfigurationFile}\n{serializedConfig}")]
    public static partial void LocalConfigurationInfo(this ILogger logger, string launcherConfigurationFile, string serializedConfig);

    [LoggerMessage(Level = LogLevel.Information, Message = "Legacy configuration {legacyConfigFile}\n{serializedConfig}")]
    public static partial void LegacyConfigurationInfo(this ILogger logger, string legacyConfigFile, string serializedConfig);

    [LoggerMessage(Level = LogLevel.Information, Message = "Remote configuration from {cashBoxConfigurationFile}\n{serializedConfig}")]
    public static partial void RemoteConfigurationInfo(this ILogger logger, string cashBoxConfigurationFile, string serializedConfig);

    #endregion

    #region Cashbox Configuration

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create cashbox-configuration-file folder.")]
    public static partial void CouldNotCreateCashboxConfigFolder(this ILogger logger, Exception? exception);

    public static void CouldNotDownloadCashboxConfig(this ILogger logger, Exception? exception, bool sandbox)
    {
        var detail = $"(Launcher is running in {(sandbox ? "sandbox" : "production")} mode.";
        if (!sandbox)
        {
            detail += " Did you forget the --sandbox flag?";
        }
        detail += ")";
        CouldNotDownloadCashboxConfigMessage(logger, exception, detail);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not download Cashbox configuration. {detail}")]
    private static partial void CouldNotDownloadCashboxConfigMessage(ILogger logger, Exception? exception, string detail);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not read Cashbox configuration file.")]
    public static partial void CouldNotReadCashboxConfig(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not parse Cashbox configuration.")]
    public static partial void CouldNotParseCashboxConfig(this ILogger logger, Exception? exception);

    #endregion

    #region Download

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading package {name}.")]
    public static partial void DownloadingPackage(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot get latest {package} version from SemVer Range in offline mode.")]
    public static partial void CannotGetLatestVersionOfflineMode(this ILogger logger, string package);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not get latest {package} version from SemVer Range {range}.")]
    public static partial void CouldNotGetLatestVersion(this ILogger logger, Exception? exception, string package, string range);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Package {name} not found in download cache.")]
    public static partial void PackageNotFoundInCache(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File hash for {name} incorrect.")]
    public static partial void FileHashIncorrect(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Package {name} did not contain the needed files.")]
    public static partial void PackageMissingNeededFiles(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hash file not found.")]
    public static partial void HashFileNotFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Incorrect Hash.")]
    public static partial void IncorrectHash(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found package in download cache.")]
    public static partial void FoundPackageInCache(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No offline packages found.")]
    public static partial void NoOfflinePackagesFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Package {fileName} already exists in cache.")]
    public static partial void PackageAlreadyExistsInCache(this ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Copied package {fileName} to cache.")]
    public static partial void CopiedPackageToCache(this ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not download package.")]
    public static partial void CouldNotDownloadPackage(this ILogger logger, Exception? exception);

    #endregion

    #region Service Installation

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing service.")]
    public static partial void InstallingService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting service.")]
    public static partial void StartingService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully installed service \"{serviceName}\".")]
    public static partial void SuccessfullyInstalledService(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping service.")]
    public static partial void StoppingService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling service.")]
    public static partial void UninstallingService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully uninstalled service \"{serviceName}\".")]
    public static partial void SuccessfullyUninstalledService(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start service \"{serviceName}\".")]
    public static partial void CouldNotStartService(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not stop service \"{serviceName}\".")]
    public static partial void CouldNotStopService(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not uninstall service \"{serviceName}\".")]
    public static partial void CouldNotUninstallService(this ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run as admin to install service.")]
    public static partial void RunAsAdminToInstallService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run as admin to uninstall service.")]
    public static partial void RunAsAdminToUninstallService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Wrong Operating system.")]
    public static partial void WrongOperatingSystem(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The launcher can only install itself as a service on windows and linux(systemd). On other OSs it has to be done manually.")]
    public static partial void ServiceInstallationManualRequired(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Only windows or linux(systemd) service can be installed automatically.")]
    public static partial void ServiceUninstallationManualRequired(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not install service \"{serviceName}\".")]
    public static partial void CouldNotInstallService(this ILogger logger, string serviceName);

    #endregion

    #region Linux SystemD

    [LoggerMessage(Level = LogLevel.Error, Message = "Systemd is not running on this machine. No service installation is possible.")]
    public static partial void SystemdNotRunning(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service is already installed and cannot be installed twice for one cashbox.")]
    public static partial void ServiceAlreadyInstalled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Systemd is not running on this machine. No service uninstallation is possible.")]
    public static partial void SystemdNotRunningUninstall(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service is not installed!")]
    public static partial void ServiceNotInstalled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service installation works only for systemd setup.")]
    public static partial void ServiceInstallationOnlyForSystemd(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing service via systemd.")]
    public static partial void InstallingServiceViaSystemd(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting systemd service.")]
    public static partial void StartingSystemdService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enabling systemd service.")]
    public static partial void EnablingSystemdService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping systemd service.")]
    public static partial void StoppingSystemdService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disabling systemd service.")]
    public static partial void DisablingSystemdService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing systemd service.")]
    public static partial void RemovingSystemdService(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading systemd daemon.")]
    public static partial void ReloadingSystemdDaemon(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resetting state for failed systemd units.")]
    public static partial void ResettingSystemdFailedUnits(this ILogger logger);

    #endregion

    #region ProcessHost

    [LoggerMessage(Level = LogLevel.Information, Message = "Package: {package} {version}")]
    public static partial void PackageInfo(this ILogger logger, string package, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Id:      {id}")]
    public static partial void PackageIdInfo(this ILogger logger, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping Package.")]
    public static partial void StoppingPackage(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error Starting Hosting.")]
    public static partial void ErrorStartingHosting(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception when calling StopBegin and StopEnd hooks.")]
    public static partial void ExceptionCallingStopHooks(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start {url} hosting.")]
    public static partial void CouldNotStartUrlHosting(this ILogger logger, Exception? exception, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Helper StartBegin() and StartEnd().")]
    public static partial void HelperStartBeginAndEnd(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error Starting Monarchs.")]
    public static partial void ErrorStartingMonarchs(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start {package} {id}.")]
    public static partial void CouldNotStartPackage(this ILogger logger, Exception? exception, string package, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {package} {id}.")]
    public static partial void StartedPackage(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started all packages.")]
    public static partial void StartedAllPackages(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Press CTRL+C to exit.")]
    public static partial void PressCtrlCToExit(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down launcher.")]
    public static partial void ShuttingDownLauncher(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ProcessHost {id} had crashed.")]
    public static partial void ProcessHostCrashed(this ILogger logger, Exception? exception, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "fiskaltrust.Launcher: {version}")]
    public static partial void LauncherVersionInfo(this ILogger logger, string? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "OS:                   {os}, {bit}")]
    public static partial void OsInfo(this ILogger logger, string os, string bit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin User:           {admin}")]
    public static partial void AdminUserInfo(this ILogger logger, string admin);

    [LoggerMessage(Level = LogLevel.Information, Message = "CWD:                  {cwd}")]
    public static partial void CwdInfo(this ILogger logger, string cwd);

    [LoggerMessage(Level = LogLevel.Information, Message = "CashBoxId:            {cashBoxId}")]
    public static partial void CashboxIdInfo(this ILogger logger, string cashBoxId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Killing {package} {id}.")]
    public static partial void KillingPackage(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Host {package} {id} has shutdown.")]
    public static partial void HostHasShutdown(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Host {package} {id} has crashed.")]
    public static partial void HostHasCrashed(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while starting {package} {id}.\n{error}")]
    public static partial void ErrorWhileStartingPackage(this ILogger logger, string package, string id, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start ProcessHost process for {package} {id}.")]
    public static partial void CouldNotStartProcessHost(this ILogger logger, Exception? exception, string package, string id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start ProcessHost process.")]
    public static partial void CouldNotStartProcessHostGeneral(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backing off restart {delay} for {package} {id}.")]
    public static partial void BackingOffRestart(this ILogger logger, string delay, string package, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restarting {package} {id}.")]
    public static partial void RestartingPackage(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling cancellation {package} {id}.")]
    public static partial void HandlingCancellation(this ILogger logger, string package, string id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProcessId {id}")]
    public static partial void ProcessIdDebug(this ILogger logger, int id);

    #endregion

    #region Hosting

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {hostingType} hosting on {url}.")]
    public static partial void StartedHosting(this ILogger logger, string hostingType, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "A TLS certificate path was defined, but the file '{pfxPath}' does not exist or is not a valid PFX file.")]
    public static partial void TlsCertificatePathNotExist(this ILogger logger, string pfxPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "A TLS certificate was defined via base64 input, but could not be parsed. Error message: {tlsParsingError}")]
    public static partial void TlsCertificateBase64ParseError(this ILogger logger, string tlsParsingError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{configName} was set but TLS certificate was not provided. HTTPS endpoints will not be secured.")]
    public static partial void TlsCertificateWarning(this ILogger logger, string configName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UseHttpSysBinding is enabled but the fiskaltrust.Launcher was not started as an Administrator. Url binding may fail.")]
    public static partial void UseHttpSysBindingNotAdmin(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UseHttpSysBinding is only supported on Windows.")]
    public static partial void UseHttpSysBindingOnlyWindows(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "net.pipe url support will be added in an upcoming version of the launcher 2.0.")]
    public static partial void NetPipeUrlSupportComingSoon(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UseHttpSysBinding is not supported for grpc.")]
    public static partial void UseHttpSysBindingNotSupportedForGrpc(this ILogger logger);

    #endregion

    #region SelfUpdate

    [LoggerMessage(Level = LogLevel.Debug, Message = "SelfUpdate Enabled.")]
    public static partial void SelfUpdateEnabled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "SelfUpdate Disabled.")]
    public static partial void SelfUpdateDisabled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "A new Launcher version is set.")]
    public static partial void NewLauncherVersionSet(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "An older Launcher version is set.")]
    public static partial void OlderLauncherVersionSet(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "A new Launcher version is found for configured range \"{range}\".")]
    public static partial void NewLauncherVersionFoundForRange(this ILogger logger, string range);

    [LoggerMessage(Level = LogLevel.Information, Message = "An older Launcher version is found for configured range \"{range}\".")]
    public static partial void OlderLauncherVersionFoundForRange(this ILogger logger, string range);

    [LoggerMessage(Level = LogLevel.Information, Message = "A new launcher version {newVersion} is available.")]
    public static partial void NewLauncherVersionAvailable(this ILogger logger, string newVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading new version {newVersion}.")]
    public static partial void DownloadingNewVersion(this ILogger logger, string newVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher will be updated to version {newVersion} on shutdown.")]
    public static partial void LauncherWillBeUpdatedOnShutdown(this ILogger logger, string newVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher will be downgraded to version {oldVersion} on shutdown.")]
    public static partial void LauncherWillBeDowngradedOnShutdown(this ILogger logger, string oldVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not download new Launcher version.")]
    public static partial void CouldNotDownloadNewLauncherVersion(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher update starting in the background.")]
    public static partial void LauncherUpdateStartingInBackground(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Launcher Update failed. See {logFolder} for the update log.")]
    public static partial void LauncherUpdateFailed(this ILogger logger, string logFolder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shutdown requested.")]
    public static partial void ShutdownRequested(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Running launcher health check.")]
    public static partial void RunningLauncherHealthCheck(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Launcher healthcheck after update failed.")]
    public static partial void LauncherHealthcheckFailed(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolled back update.")]
    public static partial void RolledBackUpdate(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Launcher update successful.")]
    public static partial void LauncherUpdateSuccessful(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for launcher to shut down.")]
    public static partial void WaitingForLauncherToShutDown(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Copying launcher executable from \"{from}\" to \"{to}\".")]
    public static partial void CopyingLauncherExecutable(this ILogger logger, string from, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "\n{stdOut}")]
    public static partial void UpdaterDoctorStdOut(this ILogger logger, string stdOut);

    [LoggerMessage(Level = LogLevel.Error, Message = "\n{stdErr}")]
    public static partial void UpdaterDoctorStdErr(this ILogger logger, string stdErr);

    [LoggerMessage(Level = LogLevel.Error, Message = "An exception occurred.")]
    public static partial void UpdaterExceptionOccurred(this ILogger logger, Exception? exception);

    #endregion

    #region Doctor

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load ECDH curve. Skipping some related doctor checks.")]
    public static partial void FailedToLoadEcdhCurve(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No configuration file downloaded yet.")]
    public static partial void NoConfigurationFileDownloadedYet(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Doctor found no issues.")]
    public static partial void DoctorFoundNoIssues(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Doctor found errors.")]
    public static partial void DoctorFoundErrors(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "[SUCCESS] {operation}")]
    public static partial void DoctorCheckSuccess(this ILogger logger, string operation);

    [LoggerMessage(Level = LogLevel.Error, Message = "[ERROR] {operation}")]
    public static partial void DoctorCheckError(this ILogger logger, Exception? exception, string operation);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[WARNING] {operation}")]
    public static partial void DoctorCheckWarning(this ILogger logger, Exception? exception, string operation);

    #endregion

    #region Security/Crypto

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not load client curve.")]
    public static partial void CouldNotLoadClientCurve(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while loading or decrypting ECDH curve from file: {clientEcdhPath}")]
    public static partial void ErrorLoadingEcdhCurve(this ILogger logger, Exception? exception, string clientEcdhPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error loading or decrypting ECDH curve: {errorMessage}. Regenerating new curve.")]
    public static partial void ErrorLoadingEcdhCurveRegenerating(this ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallback config encryption mechanism is used on linux.")]
    public static partial void FallbackConfigEncryptionLinux(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallback config encryption mechanism is used on macos.")]
    public static partial void FallbackConfigEncryptionMacos(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fallback config encryption mechanism used.")]
    public static partial void FallbackConfigEncryptionUsed(this ILogger logger);

    #endregion

    #region Directory/Permission

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to change owner of the service directory.")]
    public static partial void FailedToChangeOwnerOfServiceDirectory(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to change permissions of the service directory.")]
    public static partial void FailedToChangePermissionsOfServiceDirectory(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Changing owner and permissions is skipped on non-Unix operating systems.")]
    public static partial void ChangingOwnerAndPermissionsSkipped(this ILogger logger);

    public static void CouldNotCreateServiceDirectory(this ILogger logger, Exception? exception, string path)
    {
        CouldNotCreateServiceDirectoryMessage(logger, exception, path);
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Access to the path '{path}' is denied. Please run the application with sufficient permissions.")]
    private static partial void CouldNotCreateServiceDirectoryMessage(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Service user name is not set. Owner of the service directory will not be changed.")]
    public static partial void ServiceUserNameNotSet(this ILogger logger);

    #endregion

    #region General

    [LoggerMessage(Level = LogLevel.Error, Message = "An unhandled exception occurred.")]
    public static partial void AnUnhandledExceptionOccurred(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not load {type}.")]
    public static partial void CouldNotLoadMiddlewareBootstrapper(this ILogger logger, Exception? exception, string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {fileName} exited with code {exitCode}.")]
    public static partial void ProcessExitedWithCode(this ILogger logger, string fileName, int exitCode);

    #endregion

    #region Helper Methods

    public static ILogger ToDotnetLogger(this Serilog.ILogger logger)
    {
        return new SerilogLoggerFactory(logger).CreateLogger("fiskaltrust.Launcher");
    }

    #endregion
}
