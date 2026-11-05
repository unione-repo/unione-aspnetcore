# Changelog

All notable changes of this package. Versions follow [Semantic Versioning](https://semver.org/).

## [2.3.1](https://github.com/unione-repo/unione-aspnetcore/releases/tag/v2.3.1) - 2026-11-05

### Changed

- The package requires the exact SDK version it is released and tested with (was a range from it).

### Fixed

- The tests of the CI workflow retry the installation of the dependencies until the mirrors of the registry serve the new SDK version.

## [2.3.0](https://github.com/unione-repo/unione-aspnetcore/releases/tag/v2.3.0) - 2026-11-05

### Added

- Tests of the package (offline: the API is faked, no request leaves the machine).
- A CI workflow (`.github/workflows/test.yml`) runs the tests on every push and pull request over the supported runtimes; a release is published only when they pass.
- The tests and the release of the package wait until the SDK version it requires is in the registry, so the SDK and the package can be released together.
- README: how send errors surface (the SDK error with its kind, retryable and hint).

### Changed

- Requires the new minor version of the SDK: its errors tell what failed, whether a repeat may succeed and how to fix an API error.
- `UNIONE_TIMEOUT` and `UNIONE_WEBHOOK_PATH` fill only the options the configuration does not set. The webhook endpoint allows anonymous requests (the signature checks them) under a fallback authorization policy.

Earlier releases: the [tags](https://github.com/unione-repo/unione-aspnetcore/tags) of the repository.
