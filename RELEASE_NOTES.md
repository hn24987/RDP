# RDP Launcher v1.1.1 — Source repository support

Created by **hn24987** · https://github.com/hn24987/RDP-Launcher

## Fixed

- The project owner can now run `hn24987/RDP-Launcher` directly. Forking your own project is no longer required.
- Forks of this project remain supported. Both source and fork access require write permission, enabled Actions and the required secrets.
- Repository URL labels and first-run guidance explain both options.
- Manual GitHub Actions runs can leave the session ID blank; the workflow supplies one automatically and prints connection information in the log.

## Download and update

Download **RdpLauncher.exe** or **RDP-Launcher-v1.1.1-Windows-x64.zip** from this release. Close the old launcher and replace its EXE in the same folder; the existing encrypted token file stays there. In the app, enter your repository URL. Owners can enter `https://github.com/hn24987/RDP-Launcher` and select that repository when creating the token.

Your repository still needs `TAILSCALE_AUTH_KEY` and `RDP_PASSWORD`. The latter must have at least 14 characters, uppercase, lowercase, a digit and a symbol. Tokens need Actions read/write and Contents, Secrets and Administration read permissions. Tailscale must be installed and connected to the same tailnet as the runner.

The automated workflow builds on Windows, runs the contract tests and the DPAPI persistence smoke test, and verifies release asset checksums before publishing. Interactive UI use and live RDP login still need an end-to-end check. The EXE is unsigned.
