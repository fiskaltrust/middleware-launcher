# CI/CD Workflows

This directory contains the GitHub Actions workflows used to test, package, deploy, and release the fiskaltrust Launcher.

## Release Launcher

The entrypoint for the release process is [`release.yml`](release.yml). It is an orchestration workflow that calls the reusable workflows in this directory.

### Triggers

`Release Launcher` can be started by:

- Manually from the **Actions** tab with **Run workflow**.
- A push to the `Release-pipeline` branch.
- A version tag matching `v*` as intended by the workflow configuration.

> **Trigger check:** GitHub Actions tag filters are normally declared under `on.push.tags`, while the current workflow lists `refs/tags/v*` under `on.push.branches`. Verify this configuration before relying on a tag push to start a release.

### Release flow

```text
package
  |
  v
release-sandbox -----> github-release
  |
  v
release-production
```

1. **Package** calls [`package.yml`](package.yml).
   - Runs unit and integration tests on Windows, Ubuntu, and macOS.
   - Calculates the package version with MinVer.
   - Builds self-contained packages for `win-x64`, `win-x86`, `linux-x64`, `linux-arm`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
   - Signs Windows Launcher binaries with the configured Azure Key Vault certificate.
   - Publishes package, script, and drop artifacts.
2. **Release Sandbox** calls [`deploy.yml`](deploy.yml) with the `Sandbox` environment.
   - Downloads the package and script artifacts.
   - Deploys them to the package storage account.
   - Requires the `package` job to complete successfully.
3. **Release Production** calls [`deploy.yml`](deploy.yml) with the `Production` environment.
   - Uses the same artifacts and deployment process as Sandbox.
   - Requires the Sandbox deployment to complete successfully.
4. **GitHub Release** calls [`github-release.yml`](github-release.yml).
   - Runs after Sandbox succeeds.
   - Combines each platform drop with its scripts.
   - Publishes one ZIP per runtime target to the GitHub release for `github.ref_name`.

The GitHub release job and Production deployment are independent after Sandbox. A GitHub release can therefore be created even if Production deployment fails.

## Required configuration

Configure these items in the repository or in the relevant GitHub environments before running a release.

### Environments

Create the following GitHub environments because `deploy.yml` selects them by name:

- `Sandbox`
- `Production`

Use environment protection rules on `Production` when a manual approval is required before deployment.

### Deployment secrets

The reusable deployment workflow expects these secrets through `secrets: inherit`:

| Secret | Used for |
| --- | --- |
| `AZURE_CLIENT_ID` | Azure workload identity used for package deployment |
| `AZURE_TENANT_ID` | Azure tenant used for package deployment |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription used for package deployment |

These values must be available to the environment in which the deployment runs.

### Windows code-signing configuration

The package workflow uses the following repository or environment variables and secrets when building Windows packages:

| Name | Type |
| --- | --- |
| `CODESIGNING_AZURE_KEY_VAULT_URL` | Variable |
| `CODESIGNING_AZURE_KEY_VAULT_CERTIFICATE` | Variable |
| `CODESIGNING_AZURE_KEY_VAULT_CLIENT_ID` | Secret |
| `CODESIGNING_AZURE_KEY_VAULT_TENANT_ID` | Secret |
| `CODESIGNING_AZURE_KEY_VAULT_SECRET` | Secret |

## Running a release

1. Confirm the intended commit is available on `Release-pipeline`, or create the intended version tag.
2. Open **Actions** and select **Release Launcher**.
3. Select **Run workflow** when starting manually.
4. Monitor `package` and confirm that all test matrix jobs pass.
5. Confirm the `Sandbox` deployment and its environment approvals.
6. Verify the generated GitHub release artifacts.
7. Confirm the `Production` deployment completed successfully.

## Troubleshooting

- **The workflow does not start for a tag:** check the tag filter note above and inspect the Actions event that GitHub received.
- **Packaging fails before deployment:** inspect the test, version, or platform package matrix job in `package.yml`.
- **Deployment cannot authenticate:** verify the three Azure deployment secrets and the environment-scoped secret visibility.
- **Windows signing fails:** verify the Key Vault variables and signing secrets, then rerun the package workflow.
- **A GitHub release is missing:** inspect `github-release` and `edit-release`; both depend on the Sandbox deployment, not Production.

## Related workflows

- [`test.yml`](test.yml): cross-platform build and test workflow.
- [`package.yml`](package.yml): versioning and platform packaging.
- [`deploy.yml`](deploy.yml): package storage deployment.
- [`github-release.yml`](github-release.yml): GitHub release ZIP creation.