# RDP Launcher v1.2.0 — Session tools and two languages

Created by **hn24987** · https://github.com/hn24987/RDP-Launcher

## Added

- **Check for updates** checks the latest stable GitHub release, compares numeric versions and offers to open its release page. Updates are never downloaded or executed automatically.
- **Session countdown** shows the expected remaining time and alerts at 10 and 5 minutes with a popup and sound. Keep the launcher open to receive reminders. Resuming the same run preserves notifications even if a refresh fails.
- **Receive files** retrieves this PC's Tailscale/Taildrop inbox into a chosen local folder. Send selected files from the remote desktop first. Same-owner and Taildrop device restrictions apply. This does not browse the remote filesystem or back up files automatically; Windows Tailscale may already save incoming files to Downloads.
- **Settings → Language** switches between English and Tiếng Việt immediately and remembers the choice. App strings live in `.resx` resources; existing log entries and external CLI messages retain their original language.
- Native **win-arm64** packages alongside **win-x64** packages. ARM64 binaries are cross-compiled and their PE architecture is checked; native ARM64 execution still needs device validation.

## Build and community

- Build versions come from `.csproj` or a matching version tag; ZIP filenames follow the version automatically.
- Both architectures include documentation and the MIT license. Releases include architecture-specific EXEs and SHA256 checksums; `RdpLauncher.exe` remains the x64 alias.
- MIT LICENSE, CI/release/license badges, contribution guide and YAML issue forms for bugs/setup help.
- SmartScreen documentation explains how to verify the official source/checksum and use More info → Run anyway when available. The EXE remains unsigned; no security settings need to be disabled.

The RDP workflow now uses the same expiry timestamp for connection metadata and session shutdown. Manual runs, source repository access and encrypted token storage remain supported. Existing repository secrets are still required.

## Verification scope

Automated checks cover repository access rules, release metadata/version parsing, notification thresholds, Taildrop literal arguments, language resources, Windows UI construction and DPAPI token storage. Interactive expiry popup/audio, live Taildrop transfers and live RDP login still need validation with configured devices. ARM64 builds are not executed on x64 runners.
