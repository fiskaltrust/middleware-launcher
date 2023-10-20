# fiskaltrust Launcher

The **fiskaltrust Launcher** is an application that hosts the packages of the **fiskaltrust Middleware**, a modular fiscalization and POS platform that can be embedded into POS systems to suffice international fiscalization regulations.

> **Warning**
> This all-new fiskaltrust Launcher is currently in development. We plan to release a preview version to interested customers soon - please reach out to us in the [discussion section](https://github.com/fiskaltrust/middleware-launcher/discussions) if you want to participate.

**You can track the ongoing development of the first release in the project's [backlog and board](https://github.com/orgs/fiskaltrust/projects/3/).**

## Overview

Middleware packages each provide specific fiscalization-, data source- and security device implementations. These package can be aggregated into a configuration container (_Cashbox_) in the fiskaltrust Portal. The Launcher then uses this configuration to decide which packages to download and run, and provides configurable hosted endpoints so that the POS software can communicate with them (e.g. gRPC or HTTP).

Below, we illustrate a minimal sample configuration with the international SQLite _Queue_ package (with a configured HTTP endpoint) and a German _Signature Creation Unit_ (with a gRPC endpoint) that abstracts a Swissbit TSS.

<div align="center">
  <img src="./doc/images/overview.png" alt="overview" />
</div>

## Getting Started

> warning: This beta version  of the Launcher 2.0 is for test purpose only and should be used with our German sandbox.

Download the latest release from GitHub. We always recommend using the latest release to benefit from the newest improvements.
Unzip the downloaded release.

Start the Launcher via the commandline:

```sh
fiskaltrust.Launcher.exe run --cashbox-id <cashboxid> --access-token <accesstoken> --sandbox
```

To stop the Launcher press <kbd>Ctrl</kbd> + <kbd>C</kbd>.

> See help for other start parameters:
>  

```sh
> fiskaltrust.Launcher.exe run --help
> ```

>  
> See help for other available commands:
>  

```sh
> fiskaltrust.Launcher.exe --help
> ```

> See [CLI](#cli) for more information.

### Installation

On debian based linux systems the Launcher can also be installed via `apt-get` . The executable will be installed at `/usr/bin/fiskaltrust.Launcher` and can be run like that `fiskaltrust.Launcher --help` .

```bash
curl -L http://downloads.fiskaltrust.cloud/apt-repo/KEY.gpg | gpg --dearmor | sudo tee /usr/share/keyrings/fiskaltrust-archive-keyring.gpg > /dev/null
echo "deb [signed-by=/usr/share/keyrings/fiskaltrust-archive-keyring.gpg] http://downloads.fiskaltrust.cloud/apt-repo stable main" | sudo tee /etc/apt/sources.list.d/fiskaltrust.list
sudo apt update
sudo apt install fiskaltrust-middleware-launcher
```

> When installed this way the self-update funtionality of the launcher is disabled and it has to be updated via `apt-get` .
>  

```bash
> sudo apt update && sudo apt install --only-upgrade fiskaltrust-middleware-launcher
> ```

## Migration guide

> Caution: To switch from a launcher version 1.3.x to a version 2.0 is possible using the version Launcher 2.0- Public Preview 3 onwards.

Before switching from a 1.3.x Launcher to a Launcher 2.0, please make sure that the packages configured are compatible. You can check with the [table of the supported Packages in the Alpha](#supported-packages-in-the-alpha).

Run the uninstall-service.cmd or sh command to deinstall the old launcher.

Create the [configuration file](#launcher-configuration), and make sure to include the cashboxId and access token. 

In the new launcher folder run the following command `.\fiskaltrust.Launcher.exe install --sandbox` .

To check that the switch is successful, try send receipt to the middleware using our Postman collection.

## Launcher configuration

The Launcher 2.0 configuration is now read from a json file ( `launcher.configuration.json` in the working directory per default). The configuration has to be created mannually.

This file can be set via the `--launcher-configuration-file` cli argument.

The configuration file should contain the following config keys:

```jsonc
{
  
  "ftCashBoxId": "<ftCashBoxId>",         // string
  "accessToken": "<accessToken>",         // string
  "launcherPort": "<launcherPort>",       // int (default: 5050)
  "serviceFolder": "<serviceFolder>",     // string (default-windows: "C:/ProgramData/fiskaltrust", default-linux: "/var/lib/fiskaltrust", default-macos: "/Library/Application Support/fiskaltrust")
  "sandbox": "<sandbox>",                 // bool (default: true)
  "useOffline": "<useOffline>",           // bool (default: false)
  "launcherVersion": "<launcherVersion>", // string (default: null)
  "logFolder": "<logFolder>",             // string (default: "<serviceFolder>/logs")
  "logLevel": "<logLevel>",               // string (default: "Information")
  "packageCache": "<packageCache>",       // string (default: "<serviceFolder>/cache")
  "packagesUrl": "<packagesUrl>",         // string (default: "https://packages-2-0[-sandbox].fiskaltrust.cloud")
  "helipadUrl": "<helipadUrl>",           // string (default: "https://helipad[-sandbox].fiskaltrust.cloud")
  "downloadRetry": "<downloadRetry>",     // int (default: 1)
  "sslValidation": "<sslValidation>",     // bool (default: false)
  "proxy": "<proxy>",                     // string (default: null)
  "configurationUrl": "<configurationUrl>",                 // string (default: "https://configuration[-sandbox].fiskaltrust.cloud")
  "downloadTimeoutSec": "<downloadTimeoutSec>",             // int (default: 15)
  "processHostPingPeriodSec": "<processHostPingPeriodSec>", // int (default: 10)
  "cashboxConfigurationFile": "<cashboxConfigurationFile>", // string (default: "<serviceFolder>/service/Configuration-<ftCashBoxId>.json")
  "useHttpSysBinding": "useHttpSysBinding",                 // bool (default: false)
}
```

All of these config keys can be overridden using the corresponding cli arguments.

## CLI

### `run`

The _**run**_ command of the fiskaltrust. Launcher tool is used to execute the launcher, providing users with various options to configure its behaviour and logging details.

| Option                                                        | Description                                                                | Default                                                                                                                             |
|---------------------------------------------------------------|----------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| `--cashbox-id <cashbox-id>`                                   | Specifies the ID of the cashbox.                                           |                                                                                                                                     |
| `--access-token <access-token>`                               | Token used for authentication.                                             |                                                                                                                                     |
| `--sandbox`                                                   | Enables sandbox mode.                                                      | `false`                                                                                                                             |
| `--log-folder <log-folder>`                                   | Path to the folder where logs will be saved.                               | `"<service-folder>/logs"`                                                                                                           |
| `--log-level <level>`                                         | Determines the logging level. Accepts values like Critical, Debug, etc.    | `"Information"`                                                                                                                     |
| `--launcher-configuration-file <file>`                        | Path to the launcher configuration file.                                   | `"launcher.configuration.json"`                                                                                                     |
| `--legacy-configuration-file <file>`                          | Path to the legacy configuration file.                                     | `"fiskaltrust.exe.config"`                                                                                                          |
| `--merge-legacy-config-if-exists`                             | If set, merges legacy configuration if it exists.                          | `true`                                                                                                                              |
| `--launcher-port <port>`                                      | Specifies the port which the launcher will use for internal communication. | `5050`                                                                                                                              |
| `--use-offline`                                               | Enables offline mode.                                                      | `false`                                                                                                                             |
| `--service-folder <service-folder>`                           | Path to the service folder.                                                | windows: `"C:/ProgramData/fiskaltrust"`<br/>linux: `"/var/lib/fiskaltrust"`<br/>macos: `"/Library/Application Support/fiskaltrust"` |
| `--configuration-url <configuration-url>`                     | URL to fetch the configuration from.                                       | `"https://configuration[-sandbox].fiskaltrust.cloud"`                                                                               |
| `--packages-url <packages-url>`                               | URL to fetch packages from.                                                | `"https://packages-2-0[-sandbox].fiskaltrust.cloud"`                                                                                |
| `--package-cache <package-cache>`                             | Cache directory for the packages.                                          | `"<serviceFolder>/cache"`                                                                                                           |
| `--helipad-url <helipad-url>`                                 | URL for the helipad.                                                       | `"https://helipad[-sandbox].fiskaltrust.cloud"`                                                                                     |
| `--download-timeout-sec <download-timeout-sec>`               | Timeout for downloads in seconds.                                          | `15`                                                                                                                                |
| `--download-retry <download-retry>`                           | Number of times to retry a failed download.                                | `1`                                                                                                                                 |
| `--ssl-validation`                                            | Validates SSL certificates.                                                | `true`                                                                                                                              |
| `--proxy <proxy>`                                             | Proxy server details.                                                      |                                                                                                                                     |
| `--processhost-ping-period-sec <processhost-ping-period-sec>` | Ping period for the process host in seconds.                               | `10`                                                                                                                                |
| `--cashbox-configuration-file <cashbox-configuration-file>`   | Path to the cashbox configuration file.                                    | `""<serviceFolder>/service/Configuration-<ftCashBoxId>.json"`                                                                       |
| `--tls-certificate-path <tls-certificate-path>`               | Path to the TLS certificate.                                               |                                                                                                                                     |
| `--tls-certificate-base64 <tls-certificate-base64>`           | Base64 encoded TLS certificate.                                            |                                                                                                                                     |
| `--tls-certificate-password <tls-certificate-password>`       | Password for the TLS certificate.                                          |                                                                                                                                     |
| `--use-http-sys-binding <use-http-sys-binding>`               | Uses HTTP sys binding.                                                     | `false`                                                                                                                             |
| `--use-legacy-data-protection <use-legacy-data-protection>`   | Enables use of legacy data protection.                                     | `false`                                                                                                                             |
| `-?` , `-h` , `--help`                                        | Displays help and usage information.                                       |                                                                                                                                     |

## `config`

## `doctor`

## Service

The Launcher 2.0 can be installed as a service on Windows and linux (when systemd is available) using the `install` command:

```sh
fiskaltrust.Launcher.exe install --cashbox-id <cashboxid> --access-token <accesstoken> --launcher-configuration-file <launcher-configuration-file>
```

## Selfupdate

The Launcher 2.0 can update itsself automatically. For this the `launcherVersion` must be set in the [launcher configuration file](#launcher-configuration).

This can be set to a specific version (e.g. `"launcherVersion": "2.0.0-preview3"` updates to version `2.0.0-preview3` ).

Or this can be set to a [semver range](https://devhints.io/semver#ranges) (e.g. `"launcherVersion": ">= 2.0.0-preview3 < 2.0.0"` automatically updates to all preview versions greater or equal to `2.0.0-preview3` but does not update to non preview versions).

## Getting Started for developers

Clone this github repository and bild the project with Visual Studio.

When using VS Code, please ensure that the following command line parameters are passed to `dotnet build` to enable seamless debugging: `-p:PublishSingleFile=true -p:PublishReadyToRun=true` .

## FAQ

**Q:** Are additional components required to be installed to be able to run the Launcher 2.0?

**A:** The Launcher 2.0 does not require any additionnal components to be installed.

---

**Q:** Which market can test the launcher 2.0?

**A:** Right now only the German market can test the launcher 2.0. It is possible for everyone to register to the German sandbox and test the launcher 2.0.  Also, we are working on making the launcher available for all market.

---

**Q:** Is it possible to update the launcher version (e.g. from 1.3 to 2.0)?

**A:** It is possible to switch the launcher version from 1.3 to 2.0 using the version Launcher 2.0-Public Preview 3 and later versions.

---

**Q:** Can I use portsharing to run multiple Queues or SCUs on the same port (e.g. `rest://localhost:1500/queue1` and `rest://localhost:1500/queue2` )

**A:** Yes this is possible by setting the launcher config parameter `useHttpSysBinding` to true.

HttpSysBinding has some limitations:

* It is only supported on windows
* It is not supported for grpc communication
* The launcher may need to be run as an administrator
* No Tls certificates can be set

---

**Q:** The Launcher fails with the messages `Host ... has shutdown.` and `Restarting ...` .

**A:** The Launcher could probably not bind to the configured `launcherPort` . Try setting another port in the configuration. This is a [known issue](https://github.com/fiskaltrust/middleware-launcher/issues/98) that will be fixed in a future version.

## Known Issues

* For multiple Launcher installations on the same maching the `launcherPort` configuration parameter needs to be set to a different port for each running launcher. ([#98](https://github.com/fiskaltrust/middleware-launcher/issues/98))
* The Launcher has access problems when writing to the keyring on linux if run as a service. The launcher configuration parameter `useLegacyDataProtection` needs to be set to `true` as a workaround. ([#100](https://github.com/fiskaltrust/middleware-launcher/issues/100)

## Contributing

We welcome all kinds of contributions and feedback, e.g. via issues or pull requests, and want to thank every future contributors in advance!

Please check out the [contribution guidelines](CONTRIBUTING.md) for more detailed information about how to proceed.

## License

The fiskaltrust Middleware is released under the [EUPL 1.2](./LICENSE).

As a Compliance-as-a-Service provider, the security and authenticity of the products installed on our users' endpoints is essential to us. To ensure that only peer-reviewed binaries are distributed by maintainers, fiskaltrust explicitly reserves the sole right to use the brand name "fiskaltrust Middleware" (and the brand names of related products and services) for the software provided here as open source - regardless of the spelling or abbreviation, as long as conclusions can be drawn about the original product name.  

The fiskaltrust Middleware (and related products and services) as contained in these repositories may therefore only be used in the form of binaries signed by fiskaltrust.
