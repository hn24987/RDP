# RDP Launcher v1.1.0 — Bring Your Own Fork

Created by **hn24987** · [Source code](https://github.com/hn24987/RDP)

Launch a temporary Windows RDP session from an English desktop app using **your own GitHub fork** and Tailscale.

## What's new

- **Your fork comes first:** paste your fork's GitHub URL before using Get RDP. The launcher verifies it belongs to the hn24987/RDP fork network and that your GitHub account can write to it.
- **Faster setup checks:** local and GitHub checks run in parallel before starting a workflow. Clear messages identify missing secrets, disabled Actions, outdated workflows, or token/access problems.
- **Tailscale readiness checks:** detect installation, service responsiveness, login, connection state, device approval and online status. Shortcuts help you install or open Tailscale.
- **Sign in once:** the first launch asks for your GitHub token. It is encrypted with Windows DPAPI and saved in `github-token.dat` next to the EXE, then loaded automatically on future launches. Change or forget the token from the app.
- **English throughout:** interface, instructions, checks and app error messages are in English.
- **Creator and source links:** Created by hn24987, with direct links to the project and GitHub's fork page.

## Download

Download `RDP-Launcher-v1.1.0-Windows-x64.zip`, extract it into a writable folder, and open `RdpLauncher.exe`. No Python or separate .NET installation is required.

**Supported:** Windows 10/11 x64 with Internet access and Tailscale.

## Required setup — once per fork

1. [Fork hn24987/RDP](https://github.com/hn24987/RDP/fork), then enable Actions in **your fork**.
2. Add repository secrets `TAILSCALE_AUTH_KEY` and `RDP_PASSWORD`. Secrets are **not copied** when forking. Use a Tailscale **Auth Key**, not an API token. The password needs 14+ characters, uppercase, lowercase, a digit and a symbol.
3. Install Tailscale, sign in to the same tailnet, and connect. Allow TCP 3389 through your tailnet ACLs/grants.
4. Create a fine-grained GitHub token for your fork: **Actions: Read and write**; **Contents, Secrets, Administration: Read-only**. Complete any organization approval/SSO.
5. Start the app, save your token, paste your fork URL, and click **Get RDP**.

When the IP appears, use **Open Remote Desktop**, sign in as `.\RDP`, and enter the password you set in `RDP_PASSWORD`. Use **Stop workflow** when finished. Closing the app only stops monitoring.

## Updating from an older version

Sync your fork with the updated project or replace `.github/workflows/main.yml` with the version included in this release. Older workflows do not support the launcher's connection artifact protocol. You must supply your own secrets in the fork. Version 1.1 stores session history in a new per-fork file; use Resume run and enter an older Run ID manually if necessary.

## Notes

- The EXE is unsigned. Windows SmartScreen may ask you to verify the source.
- Saved tokens are tied to your Windows account; copying the token file to another account/machine does not transfer sign-in. Do not share or upload that file.
- Preflight checks secret presence, not secret values. Auth Key validity, runner availability/quota/billing, runtime policy, device approval and ACL access are confirmed during startup/connection.
- Sessions and runner storage are temporary. Save important files before stopping the workflow.
- Build and automated contract checks were completed in Linux. Windows UI, DPAPI behavior and live RDP login still need Windows end-to-end validation; a Windows token-storage test is included in the build workflow.
