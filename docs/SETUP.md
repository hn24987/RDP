# Setup guide

[← Back to RDP Launcher](../README.md)

Set up a repository you control once, then use **Get RDP** to start future sessions. The owner can use the source repository directly; other users use their own fork.

## First-time setup

1. **Select a repository you control.** The project owner can enter `https://github.com/hn24987/RDP-Launcher` directly. Other users should fork the project into their account. If your fork predates this release, sync it with the updated source or replace `.github/workflows/main.yml` with the workflow included in this download.
2. Open **your fork → Actions** and enable workflows. In **Settings → Actions → General**, allow Actions and `actions/upload-artifact@v4`. Check your GitHub runner quota/billing.
3. Add these **repository secrets** under **your fork → Settings → Secrets and variables → Actions**. GitHub does **not** copy secrets when you fork:
   - `TAILSCALE_AUTH_KEY`: a valid **Auth Key**, not an API access token, from https://login.tailscale.com/admin/settings/keys. Use a reusable key for repeated launches; an ephemeral key is recommended for temporary runners. If device approval is required, pre-approve the key/devices. Configure any tags and ACLs/grants for your tailnet.
   - RDP password is optional in launcher v1.3. Leave the field blank for `123456`, or enter your own (up to the Windows limit of 127 characters). No minimum length or complexity is imposed. Custom values are encrypted into a per-session GitHub secret by the app; they are never sent as workflow inputs. The app removes this temporary secret after connection info is ready. If you close the app early, resume it to clean up or remove `RDP_PASSWORD_<session-id>` in repository Settings.
4. Install Tailscale on your Windows PC: https://tailscale.com/download/windows. Sign in to the **same tailnet** as the Auth Key, connect, and ensure your PC is online. ACLs/grants must allow your PC to access the runner on **TCP 3389**.
5. Create a **fine-grained GitHub personal access token**: https://github.com/settings/personal-access-tokens/new.
   - Select the owner of your fork, then select **only your fork** under repository access.
   - Repository permissions: **Actions: Read and write**, **Contents: Read-only**, **Secrets: Read and write** (for custom RDP passwords), **Administration: Read-only**. Metadata is automatic.
   - Complete organization approval/SSO if applicable. Your GitHub account must have write access to the fork.
   - Administration read checks whether Actions is enabled; Secrets read checks secret names. The launcher does not require Contents write and does not push changes to GitHub.

## Launch a session

1. Extract the download into a **writable folder** and open `RdpLauncher.exe`.
2. On first launch, the app asks for a GitHub token. It validates the token and saves it as **`github-token.dat` beside the EXE**, encrypted using Windows DPAPI for your Windows account. On later launches, it loads automatically. Use **Add / change token** or **Forget token** to replace/delete it. Expired/revoked tokens still need replacing.
3. Paste **your repository URL**, e.g. `https://github.com/YOUR-USERNAME/RDP`. Repository names may differ; the app verifies the fork network through GitHub. The source repository requires write access; unrelated/copied repositories are rejected.
4. Choose 10–300 minutes. Optionally click **Check setup**, or click **Get RDP**, which always checks first. The app remembers your fork and restores its last run; after reopening, it automatically checks a saved fork when a token is available.
5. Wait for the IP, then click **Copy IP** or **Open Remote Desktop**. Sign in as `.\RDP` using your selected password, or `123456` if left blank. Match any certificate prompt to the displayed session IP.
6. Click **Stop workflow** when finished. Closing the app or clicking **Stop monitoring** does not stop GitHub Actions. Temporary runner data is lost when the session ends.

To return to a session, reopen the app and click **Resume run**. You can also enter its Run ID from `https://github.com/OWNER/REPO/actions/runs/RUN_ID`. Run IDs and request IDs are kept separately per fork in `%LOCALAPPDATA%\RdpLauncher\session-v2.json`; this file contains no token or RDP password. To create another session after the old one ends, clear Run ID. If a request has an uncertain result, the app asks you to check Actions before discarding that request ID, preventing accidental duplicate launches.

## Updates and language

Use **Check for updates** to compare the installed version with the latest stable release on the official GitHub repository. If a newer version is found, the app offers to open the release page. It does not silently replace the EXE or run a downloaded file. An update check needs Internet access; a network failure does not stop a running RDP session.

In **Settings**, choose **English** or **Tiếng Việt**. The language preference is saved locally. Repository names, IP addresses, paths and GitHub/Tailscale responses remain their original values.

When updating, close the old launcher and replace its EXE with the matching architecture from Releases. Keep `github-token.dat` in the same folder to retain sign-in under the same Windows account.

## Session countdown

While the launcher is open and has connection information for a monitored session, it shows the time remaining and warns once when the session reaches **10 minutes** and **5 minutes** remaining. The warnings include a sound and remind you to save files. Reopening or reconnecting to a session should not be treated as a fresh timer; the runner's published expiry time is the reference.

The clock reflects the published schedule. A workflow can still end early because it was cancelled, failed or reached a GitHub limit. Closing the launcher or suspending the local PC prevents timely reminders. The launcher cannot recover files after the runner has been removed.

## Receive files with Taildrop

Taildrop transfers files over Tailscale rather than publishing them as GitHub artifacts. This version receives files already sent to your PC's Taildrop inbox; it does not browse or pull arbitrary files from the remote filesystem automatically.

1. Keep both your PC and the RDP runner connected to Tailscale. Taildrop must be enabled by your tailnet administrator. The sending and receiving devices must be owned by the **same Tailscale user**; tagged devices and cross-user/device-sharing arrangements may not support Taildrop. Check [Tailscale's Taildrop documentation](https://tailscale.com/kb/1106/taildrop) for the current restrictions.
2. On your local PC, obtain its Tailscale IPv4 address from the Tailscale app or run `tailscale ip -4`. Use your **local PC's** address as the destination.
3. Inside the RDP session, open PowerShell and send a file you chose. Replace the sample path and destination with your own values:

   ```powershell
   & "$env:ProgramFiles\Tailscale\tailscale.exe" file cp "C:\Users\RDP\Desktop\report.zip" "100.100.100.100:"
   ```

4. Back in RDP Launcher on your PC, click **Receive files**, choose a local destination folder, and wait for the receive result. The app invokes `tailscale file get` against that folder; it does not need your GitHub token for this transfer.
5. Open the received file locally and confirm it is complete **before** stopping the workflow or letting the session end. A countdown reminder does not automatically send or back up files.

On Windows, the Tailscale desktop app may already save incoming files to Downloads. If the inbox is empty, check that folder first, then confirm that the remote send succeeded, that you used the local PC's address, and that both devices meet Taildrop's ownership/policy requirements. Treat incoming files as files received from another computer; only open files you recognize and intended to send.

## Preflight checks

Independent GitHub and local checks run in parallel. Before dispatch, the app checks:

- Your fork URL uses HTTPS on github.com and targets a repository rather than a branch/file/Actions page.
- Token authentication, repo access, writable fork membership in the `hn24987/RDP-Launcher` network, and archived/disabled status.
- Actions enabled, `main.yml` active, and launcher-compatible workflow on the default branch.
- Presence of **both repository secrets**. Organization/environment secrets are not checked by this version.
- Read access to workflow runs; existing sessions are reported in the log.
- Tailscale installed; CLI/service responsive; signed in, switched on and online. Messages distinguish signed-out, switched-off, device-approval and offline states. Use **Install Tailscale** or **Open Tailscale** to fix setup.
- After startup: connection artifact matches its run/request, its IP is in the Tailscale range, its session has not expired, and your PC can reach TCP 3389.

GitHub does not reveal secret values. Preflight cannot prove an Auth Key is still valid, the password meets Windows policy, ACLs allow access, devices are approved, runner quota/billing is sufficient, or all runtime policies allow execution. The runner and connection checks confirm those later. Actions write permission is confirmed by the actual dispatch, not by a read request. The app never silently repeats a dispatch after an uncertain network response.

## Token storage

Only encrypted token bytes are stored beside the EXE. DPAPI ties decryption to your Windows account; copying `github-token.dat` to another user or machine is not a portable login. This protects the file at rest; software running under your Windows account can still access your account's secrets. Do not upload this file with your release or commit it. It is excluded by `.gitignore` and packaging scripts. If the folder is read-only, move the app to a writable folder and retry.

## Troubleshooting

- **403 / missing permissions:** check PAT repository selection, permission levels, organization approval/SSO, and API limits.
- **Workflow missing/disabled on a fork:** enable Actions in your fork and sync the updated workflow. Forking alone does not enable every workflow or copy secrets.
- **IP appears but connection fails:** check the tailnet, device approval, ACLs/grants and TCP 3389. Use Resume run to refresh; displayed connection details reflect the last check.
- **Expired session:** clear Run ID and launch again. A session lasts up to your chosen duration; the job has a 360-minute overall cap including setup.
- **Saved token cannot be decrypted:** use Add / change token under the Windows account now running the app.

Connection artifacts contain the IP, username and session timing, never the RDP password. On public forks, users who can read Actions may see this metadata. The EXE is unsigned; Windows SmartScreen may ask you to verify its publisher. Source is included so you can inspect and build it yourself.

## Verify your download and SmartScreen

The current EXE is **unsigned**. A checksum helps detect a damaged or changed download, but it does not replace a publisher signature or establish that a file is safe by itself.

1. Download only from [the official Releases page](https://github.com/hn24987/RDP-Launcher/releases). Confirm that the repository owner is `hn24987` and the repository is `RDP-Launcher`.
2. Download `SHA256.txt` from the same release. In PowerShell, calculate the hash of the file you downloaded:

   ```powershell
   Get-FileHash .\RdpLauncher.exe -Algorithm SHA256
   ```

   Compare it with the matching filename in `SHA256.txt`. For an architecture-specific EXE or ZIP, calculate that file's hash and compare its exact filename instead. Stop if the hashes differ.
3. After verifying the source and checksum, if Windows shows **Windows protected your PC** for this download, select **More info → Run anyway** only if you trust this project and intend to run it. Some managed PCs block this action; follow your administrator's policy.

Do not disable Microsoft Defender or SmartScreen globally. If the file has a malware detection rather than an unknown-publisher prompt, investigate the specific detection before running it. Source is available for inspection and self-building.

## Build from source

With .NET 8 SDK, from the repository root:

```powershell
dotnet run --project tests/ContractTests.csproj
dotnet run --project tests/WindowsTokenTests/WindowsTokenTests.csproj # Windows only
dotnet publish desktop/RdpLauncher/RdpLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o artifacts/win-x64
dotnet publish desktop/RdpLauncher/RdpLauncher.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o artifacts/win-arm64
```

Or, after adding this source to your repository's default branch, open **Actions → Build Windows app → Run workflow**, then choose the **win-x64** or **win-arm64** artifact. ZIP names use the validated tag or the version in the project file. That build also runs the Windows token-storage smoke test on x64; an ARM64 cross-build is not a native ARM64 runtime test. See [CONTRIBUTING.md](../CONTRIBUTING.md) for the complete contribution and validation workflow. See [release notes](../RELEASE_NOTES.md) and the [release-writing prompt](../RELEASE_PROMPT.md).

The workflow creates a standard RDP user with NLA/TLS, limits its RDP firewall rule to Tailscale addresses, publishes connection metadata before keeping the session alive, and logs out of Tailscale during cleanup. Use this temporary environment for testing in accordance with GitHub Actions and Tailscale terms.
