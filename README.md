# fiskaltrust Launcher
The **fiskaltrust Launcher** is an application that hosts the packages of the **fiskaltrust Middleware**, a modular fiscalization and POS platform that can be embedded into POS systems to suffice international fiscalization regulations.

> **Warning** 
>This all-new fiskaltrust Launcher is currently in development. We plan to release a preview version to interested customers soon - please reach out to us in the [discussion section](https://github.com/fiskaltrust/middleware-launcher/discussions) if you want to participate.

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

> ***Note:***  
> See help for other start parameters:
> ```sh
> fiskaltrust.Launcher.exe run --help
> ```
> See help for other available commands:
> ```sh
> fiskaltrust.Launcher.exe --help
> ```

### Installation

On debian based linux systems the Launcher can also be installed via `apt-get`. The executable will be installed at `/usr/bin/fiskaltrust.Launcher` and can be run like that `fiskaltrust.Launcher --help`.

```bash
curl -L http://downloads.fiskaltrust.cloud/apt-repo/KEY.gpg | gpg --dearmor | sudo tee /usr/share/keyrings/fiskaltrust-archive-keyring.gpg > /dev/null
echo "deb [signed-by=/usr/share/keyrings/fiskaltrust-archive-keyring.gpg] http://downloads.fiskaltrust.cloud/apt-repo stable main" | sudo tee /etc/apt/sources.list.d/fiskaltrust.list
sudo apt update
sudo apt install fiskaltrust-middleware-launcher
```

> When installed this way the self-update funtionality of the launcher is disabled and it has to be updated via `apt-get`.
> ```bash
> sudo apt update && sudo apt install --only-upgrade fiskaltrust-middleware-launcher
> ```

## Migration guide

> Caution: To switch from a launcher version 1.3.x to a version 2.0 is possible using the version Launcher 2.0- Public Preview 3 onwards.

Before switching from a 1.3.x Launcher to a Launcher 2.0, please make sure that the packages configured are compatible. You can check with the [table of the supported Packages in the Alpha](#supported-packages-in-the-alpha).

Run the uninstall-service.cmd or sh command to deinstall the old launcher.

Create the [configuration file](#launcher-configuration), and make sure to include the cashboxId and access token. 

In the new launcher folder run the following command `.\fiskaltrust.Launcher.exe install --sandbox`.

To check that the switch is successful, try send receipt to the middleware using our Postman collection.

## Launcher configuration

The Launcher 2.0 configuration is now read from a json file (`launcher.configuration.json` in the working directory per default).The configuration has to be created mannually.

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
  "packagesUrl": "<packagesUrl>",         // string (default: "https://packages-2-0[-sandbox].fiskaltrust.cloud")
  "helipadUrl": "<helipadUrl>",           // string (default: "https://helipad[-sandbox].fiskaltrust.cloud")
  "downloadRetry": "<downloadRetry>",     // int (default: 1)
  "sslValidation": "<sslValidation>",     // bool (default: false)
  "proxy": "<proxy>",                     // string (default: null)
  "configurationUrl": "<configurationUrl>",                 // string (default: "https://configuration[-sandbox].fiskaltrust.cloud")
  "downloadTimeoutSec": "<downloadTimeoutSec>",             // int (default: 15)
  "processHostPingPeriodSec": "<processHostPingPeriodSec>", // int (default: 10)
  "cashboxConfigurationFile": "<cashboxConfigurationFile>", // string (default: "configuration-<ftCashBoxId>.json")
  "useHttpSysBinding": "useHttpSysBinding",                 // bool (default: false)
}
```
All of these config keys can be overridden using the corresponding cli arguments.

## Service

The Launcher 2.0 can be installed as a service on Windows and linux (when systemd is available) using the `install` command:
```sh
fiskaltrust.Launcher.exe install --cashbox-id <cashboxid> --access-token <accesstoken> --launcher-configuration-file <launcher-configuration-file>
```

## Selfupdate

The Launcher 2.0 can update itsself automatically. For this the `launcherVersion` must be set in the [launcher configuration file](#launcher-configuration).

This can be set to a specific version (e.g. `"launcherVersion": "2.0.0-preview3"` updates to version `2.0.0-preview3`).

Or this can be set to a [semver range](https://devhints.io/semver#ranges) (e.g. `"launcherVersion": ">= 2.0.0-preview3 < 2.0.0"` automatically updates to all preview versions greater or equal to `2.0.0-preview3` but does not update to non preview versions).

## Getting Started for developers

Clone this github repository and bild the project with Visual Studio.

When using VS Code, please ensure that the following command line parameters are passed to `dotnet build` to enable seamless debugging: `-p:PublishSingleFile=true -p:PublishReadyToRun=true`.

### Code Tours

We've created some code tours which guide you through the code. They provide an easy way to familiarize yourself with the codebase. 

You can run them using Visual Studio Code and the [CodeTour](https://marketplace.visualstudio.com/items?itemName=vsls-contrib.codetour) extension.

When viewing this [repository on GitHub](https://github.com/fiskaltrust/middleware-launcher) you can simply press the dot key <kbd>.</kbd> to open a codespace with this extension installed.

In the VS Code sidebar on the left open the `CODETOUR` section and "Introduction" or "Overview" tour.

<img src="data:image/png;base64,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" />


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

**Q:** Can I use portsharing to run multiple Queues or SCUs on the same port (e.g. `rest://localhost:1500/queue1` and `rest://localhost:1500/queue2`)

**A:** Yes this is possible by setting the launcher config parameter `useHttpSysBinding` to true.

HttpSysBinding has some limitations:
* It is only supported on windows
* It is not supported for grpc communication
* The launcher may need to be run as an administrator
* No Tls certificates can be set


## Contributing
We welcome all kinds of contributions and feedback, e.g. via issues or pull requests, and want to thank every future contributors in advance!

Please check out the [contribution guidelines](CONTRIBUTING.md) for more detailed information about how to proceed.

## License
The fiskaltrust Middleware is released under the [EUPL 1.2](./LICENSE). 

As a Compliance-as-a-Service provider, the security and authenticity of the products installed on our users' endpoints is essential to us. To ensure that only peer-reviewed binaries are distributed by maintainers, fiskaltrust explicitly reserves the sole right to use the brand name "fiskaltrust Middleware" (and the brand names of related products and services) for the software provided here as open source - regardless of the spelling or abbreviation, as long as conclusions can be drawn about the original product name.  

The fiskaltrust Middleware (and related products and services) as contained in these repositories may therefore only be used in the form of binaries signed by fiskaltrust. 
