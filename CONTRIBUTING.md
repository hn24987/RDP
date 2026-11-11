# Contributing to RDP Launcher

Thanks for helping improve the launcher. Small, focused changes are easier to review and test.

## Before you start

Use [bug reports or setup-help forms](https://github.com/hn24987/RDP-Launcher/issues/new/choose) for problems. For a larger feature, open an issue describing the behavior and tradeoffs before writing a large patch.

Never commit `github-token.dat`, personal session files, GitHub tokens, Tailscale keys, RDP passwords or files transferred from a remote session. Use redacted errors in issues and pull requests.

## Development environment

- .NET 8 SDK and Git.
- Windows 10/11 for running the WinForms app and the Windows DPAPI persistence test.
- Linux/macOS can run the platform-neutral tests and cross-build the Windows app, but cannot validate the Windows UI or DPAPI behavior.
- Tailscale and a configured test repository are needed only for a live RDP/Taildrop integration check. Running the unit tests does not start a remote machine.

Clone your fork and open a branch:

```powershell
git clone https://github.com/YOUR-USERNAME/RDP-Launcher.git
cd RDP-Launcher
git switch -c fix/describe-your-change
```

## Build and run

```powershell
dotnet build desktop/RdpLauncher/RdpLauncher.csproj -c Release
dotnet run --project desktop/RdpLauncher/RdpLauncher.csproj
```

The second command runs the UI and requires Windows. For a standalone build, choose the architecture of the user's PC:

```powershell
dotnet publish desktop/RdpLauncher/RdpLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o artifacts/win-x64
dotnet publish desktop/RdpLauncher/RdpLauncher.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o artifacts/win-arm64
```

An ARM64 cross-build does not establish that the executable runs correctly on ARM64 hardware. Report a native ARM64 check separately.

## Tests

Run the platform-neutral contract tests:

```powershell
dotnet run --project tests/ContractTests.csproj
dotnet run --project tests/FeatureTests/FeatureTests.csproj
dotnet run --project tests/LocalizationTests/LocalizationTests.csproj
dotnet run --project tests/WindowsUiTests/WindowsUiTests.csproj # Windows only
```

Run any additional feature test projects added under `tests/`, and on Windows run the DPAPI test:

```powershell
dotnet run --project tests/WindowsTokenTests/WindowsTokenTests.csproj
```

The build workflow runs the repository's required test projects before packaging. Test changes in both languages when editing UI resources. Check that a language change preserves the repository URL, current run, token and connection information.

For update checks, prefer a fake HTTP handler; do not require a live release to test comparison logic. For countdowns, pass a controlled clock and test threshold crossings, reconnects, sleep/resume and expiry. For transfers, test argument construction without sending personal files.

## Pull requests

1. Keep one change or closely related set of changes per PR.
2. Preserve manual workflow dispatch as well as app-driven dispatch.
3. Put visible UI text in the English/Vietnamese resource files. Keep resource keys aligned and avoid hardcoded translated strings.
4. Explain the problem and resulting behavior; include an example if it helps.
5. List the checks you actually ran. Distinguish compilation, automated tests and live Windows/RDP/ARM64 checks.
6. Include screenshots for material UI changes when practical, with tokens, IPs and personal information removed.

The Windows build is defined in `.github/workflows/build-app.yml`. Versions and package names are resolved from the git tag or project version; do not introduce another hardcoded release number into build steps. A normal PR should produce build artifacts, not publish a release. Release publishing is reserved for the maintainer's tagged or explicitly requested release flow.

## License

By submitting a contribution, you agree that it may be distributed under this project's [MIT License](LICENSE). Keep attribution and third-party notices intact.

### Requesting a release from main

Maintainers may also commit a version-only `.github/release-request` file on `main`. Its value must match `<Version>` in the project. A change to that file triggers publication with the same validation, Windows builds and checksum checks as tag/manual releases. Changing ordinary source files does not trigger publication. Published release assets are never replaced.
