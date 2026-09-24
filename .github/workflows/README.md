# CI/CD Workflows

This directory contains the GitHub Actions workflows used to test, package, deploy, and release the fiskaltrust Launcher.

## Release Launcher

The entrypoint for the release process is [`release.yml`](release.yml). It is an orchestration workflow that calls the reusable workflows in this directory.

### Triggers

`Release Launcher` can be started by:

- Manually from the **Actions** tab with **Run workflow**.
- A version tag matching `v*`.

The reusable [`package.yml`](package.yml) workflow can also be run independently. It supports manual dispatch, workflow calls, pushes to `main` or its declared tag-like branch filter, and pull requests targeting `main`.

### Release flow

```text
package
  |
  v
release-sandbox
  |
  v
release-production
   |
   v
github-release
```

1. **Package** calls [`package.yml`](package.yml).
   - Runs unit and integration tests on Windows, Ubuntu, and macOS.
   - Runs the `version` job after `test-launcher` succeeds.
   - Calculates the package version with the shared middleware version action and passes it to the packaging jobs.
   - validates that the Git tag matches the version resolved from `version.json` in the format `v<version>`.
   - Builds self-contained packages for `win-x64`, `win-x86`, `linux-x64`, `linux-arm`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
   - Signs Windows Launcher binaries with the configured Azure Key Vault certificate.
   - Publishes package, script, and drop artifacts for each runtime target.
   - Builds Debian packages for `linux-x64`, `linux-arm`, and `linux-arm64` through [`deb-package.yml`](deb-package.yml), and publishes a `meta` artifact.
2. **Release Sandbox** calls [`deploy.yml`](deploy.yml) with the `Sandbox` environment.
   - Downloads the package and script artifacts.
   - Deploys them to the package storage account.
   - Requires the `package` job to complete successfully.
3. **Release Production** calls [`deploy.yml`](deploy.yml) with the `Production` environment.
   - Uses the same artifacts and deployment process as Sandbox.
   - Requires the Sandbox deployment to complete successfully.
4. **GitHub Release** calls [`github-release.yml`](github-release.yml).
   - Runs after Production succeeds.
   - Combines each platform drop with its scripts and Launcher executable files before repackaging.
   - Publishes one ZIP per runtime target to the GitHub release for `github.ref_name`.

The GitHub release is therefore blocked until both Sandbox and Production deployments succeed.

## Related workflows

- [`test.yml`](test.yml): cross-platform build and test workflow.
- [`package.yml`](package.yml): versioning and platform packaging.
- [`deploy.yml`](deploy.yml): package storage deployment.
- [`github-release.yml`](github-release.yml): GitHub release ZIP creation.