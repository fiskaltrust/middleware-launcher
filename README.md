# fiskaltrust Launcher
The **fiskaltrust Launcher** is an application that hosts the packages of the **fiskaltrust Middleware**, a modular fiscalization and POS platform that can be embedded into POS systems to suffice international fiscalization regulations.

> :warning: This all-new fiskaltrust Launcher is currently in development. We plan to release a preview version to interested customers soon - please reach out to us in the [discussion section](https://github.com/fiskaltrust/middleware-launcher/discussions) if you want to participate.

**You can track the ongoing development of the first release in the project's [backlog and board](https://github.com/orgs/fiskaltrust/projects/3/).**

## Overview

Middleware packages each provide specific fiscalization-, data source- and security device implementations. These package can be aggregated into a configuration container (_Cashbox_) in the fiskaltrust Portal. The Launcher then uses this configuration to decide which packages to download and run, and provides configurable hosted endpoints so that the POS software can communicate with them (e.g. gRPC or HTTP).

Below, we illustrate a minimal sample configuration with the international SQLite _Queue_ package (with a configured HTTP endpoint) and a German _Signature Creation Unit_ (with a gRPC endpoint) that abstracts a Swissbit TSS. 

<div align="center">
  <img src="./doc/images/overview.png" alt="overview" />
</div>

## Getting Started

Clone this github repository and bild the project with visual studio.

Start the Launcher via the commandline:
```sh
fiskaltrust.Launcher.exe run --cashbox-id <cashboxid> --access-token <accesstoken> --sandbox
```

To stop the Launcher press <kbd>Ctrl</kbd> + <kbd>C</kbd>.

> ***Note:***  
> See help for other start parameters:
> ```sh
> fiskaltrust.Launcher.exe run --help
> ```
> See help for other available commands:
> ```sh
> fiskaltrust.Launcher.exe --help
> ```

### Service

The 2.0 Launcher can be installed as a service on Windows and linux (when systemd is available) using the `install` command:
```sh
fiskaltrust.Launcher.exe install --cashbox-id <cashboxid> --access-token <accesstoken> --launcher-configuration-file <launcher-configuration-file>
```

### Launcher configuration

The 2.0 Launcher configuration is now read from a json file (`launcher.configuration.json` in the working directory per default).

This file can be set via the `--launcher-configuration-file` cli argument.

The file can contain the following config keys:
```jsonc
{
  
  "ftCashBoxId": "<ftCashBoxId>",      // string
  "accessToken": "<accessToken>",      // string
  "launcherPort": "<launcherPort>",    // int (default: 5050)
  "serviceFolder": "<serviceFolder>",  // string (default-windows: "C:/ProgramData/fiskaltrust", default-linux: "/var/lib/fiskaltrust", default-macos: "/Library/Application Support/fiskaltrust")
  "sandbox": "<sandbox>",              // bool (default: false)
  "useOffline": "<useOffline>",        // bool (default: false)
  "logFolder": "<logFolder>",          // string (default: "<serviceFolder>/logs")
  "logLevel": "<logLevel>",            // string (default: "Information")
  "packagesUrl": "<packagesUrl>",      // string (default: "https://packages-2-0[-sandbox].fiskaltrust.cloud")
  "helipadUrl": "<helipadUrl>",        // string (default: "https://helipad[-sandbox].fiskaltrust.cloud")
  "downloadRetry": "<downloadRetry>",  // int (default: 1)
  "sslValidation": "<sslValidation>",  // bool (default: false)
  "proxy": "<proxy>",                  // string (default: null)
  "configurationUrl": "<configurationUrl>",                    // string (default: "https://configuration[-sandbox].fiskaltrust.cloud")
  "downloadTimeoutSec": "<downloadTimeoutSec>",                // int (default: 15)
  "processHostPingPeriodSec": "<processHostPingPeriodSec>",    // int (default: 10)
  "cashboxConfigurationFile": "<cashboxConfigurationFile>",    // string (default: "Configuration-<ftCashBoxId>.json")
}
```

All of these config keys can be overridden using the corresponding cli arguments.

### Supported Packages in the Alpha

| Name                                           | Versions    |
| ---------------------------------------------- | ----------- |
| fiskaltrust.Middleware.Queue.MySQL             | v1.3.28     |
| fiskaltrust.Middleware.Queue.SQLite            | v1.3.28     |
| fiskaltrust.Middleware.SCU.DE.FiskalyCertified | v1.3.26     |
| fiskaltrust.Middleware.SCU.DE.CryptoVision     | v1.3.28-rc1 |
| fiskaltrust.Middleware.SCU.DE.DeutscheFiskal   | v1.3.27     |
| fiskaltrust.Middleware.SCU.DE.DieboldNixdorf   | v1.3.20     |
| fiskaltrust.Middleware.SCU.DE.Epson            | v1.3.19     |
| fiskaltrust.Middleware.SCU.DE.Swissbit         | v1.3.25     |
| fiskaltrust.Middleware.SCU.DE.SwissbitCloud    | v1.3.27     |
| fiskaltrust.Middleware.Helper.Helipad          | v1.3.26     |

## Contributing
In general, we welcome all kinds of contributions and feedback, e.g. via issues or pull requests, and want to thank every future contributors in advance!

Please check out the [contribution guidelines](CONTRIBUTING.md) for more detailed information about how to proceed.

## License
The fiskaltrust Launcher is released under the [MIT License](LICENSE).
