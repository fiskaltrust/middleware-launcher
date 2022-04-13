# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

> These are the canges compared with the legacy fiskaltrust.Launcher.

### Added

- The fiskaltrust.Launcher is now open source.
- Json launcher configuration file support.
- Native hosting of GRPC urls.
- Package download cache.
- Handling of encrypted configuration values.
- .NET 6 support.
- Native executables for Windows, Linux and MacOS.
- Linux service installation.

### Changed

- Better console and file logging.
- Better command line interface.

### Removed

- Support for `fiskaltrust.exe.config` files removed.
- Support for .NET Framework 4. (required framework is now included)
- Hosting of WCF/SOAP. (SOAP support will be re-added in a later version)


[Unreleased]: https://github.com/fiskaltrust/middleware-launcher/compare/master...proof-of-concept
