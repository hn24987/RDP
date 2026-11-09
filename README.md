# RDP Launcher v1.1

**Created by hn24987** · Source: https://github.com/hn24987/RDP

A Windows desktop launcher for temporary RDP sessions on **your own GitHub fork**, connected through Tailscale. The entire app interface is in English. The standalone Windows x64 EXE does not require Python or a separate .NET installation.

## First-time setup

1. **Fork https://github.com/hn24987/RDP into your account.** Do not enter the source repository URL in the launcher. If your fork predates this release, sync it with the updated source or replace `.github/workflows/main.yml` with the workflow included in this download.
2. Open **your fork → Actions** and enable workflows. In **Settings → Actions → General**, allow Actions and `actions/upload-artifact@v4`. Check your GitHub runner quota/billing.
3. Add these **repository secrets** under **your fork → Settings → Secrets and variables → Actions**. GitHub does **not** copy secrets when you fork:
   - `TAILSCALE_AUTH_KEY`: a valid **Auth Key**, not an API access token, from https://login.tailscale.com/admin/settings/keys. Use a reusable key for repeated launches; an ephemeral key is recommended for temporary runners. If device approval is required, pre-approve the key/devices. Configure any tags and ACLs/grants for your tailnet.
   - `RDP_PASSWORD`: at least **14 characters**, including uppercase, lowercase, a digit, and a symbol. Keep it in your password manager; you will type it into Remote Desktop.
4. Install Tailscale on your Windows PC: https://tailscale.com/download/windows. Sign in to the **same tailnet** as the Auth Key, connect, and ensure your PC is online. ACLs/grants must allow your PC to access the runner on **TCP 3389**.
5. Create a **fine-grained GitHub personal access token**: https://github.com/settings/personal-access-tokens/new.
   - Select the owner of your fork, then select **only your fork** under repository access.
   - Repository permissions: **Actions: Read and write**, **Contents: Read-only**, **Secrets: Read-only**, **Administration: Read-only**. Metadata is automatic.
   - Complete organization approval/SSO if applicable. Your GitHub account must have write access to the fork.
   - Administration read checks whether Actions is enabled; Secrets read checks secret names. The launcher does not require Contents write and does not push changes to GitHub.

## Launch a session

1. Extract the download into a **writable folder** and open `RdpLauncher.exe`.
2. On first launch, the app asks for a GitHub token. It validates the token and saves it as **`github-token.dat` beside the EXE**, encrypted using Windows DPAPI for your Windows account. On later launches, it loads automatically. Use **Add / change token** or **Forget token** to replace/delete it. Expired/revoked tokens still need replacing.
3. Paste **your fork URL**, e.g. `https://github.com/YOUR-USERNAME/RDP`. Repository names may differ; the app verifies the fork network through GitHub. The source repository and unrelated/copied repositories are rejected.
4. Choose 10–300 minutes. Optionally click **Check setup**, or click **Get RDP**, which always checks first. The app remembers your fork and restores its last run; after reopening, it automatically checks a saved fork when a token is available.
5. Wait for the IP, then click **Copy IP** or **Open Remote Desktop**. Sign in as `.\RDP` using your `RDP_PASSWORD` secret. Match any certificate prompt to the displayed session IP.
6. Click **Stop workflow** when finished. Closing the app or clicking **Stop monitoring** does not stop GitHub Actions. Temporary runner data is lost when the session ends.

To return to a session, reopen the app and click **Resume run**. You can also enter its Run ID from `https://github.com/OWNER/REPO/actions/runs/RUN_ID`. Run IDs and request IDs are kept separately per fork in `%LOCALAPPDATA%\RdpLauncher\session-v2.json`; this file contains no token or RDP password. To create another session after the old one ends, clear Run ID. If a request has an uncertain result, the app asks you to check Actions before discarding that request ID, preventing accidental duplicate launches.

## Preflight checks

Independent GitHub and local checks run in parallel. Before dispatch, the app checks:

- Your fork URL uses HTTPS on github.com and targets a repository rather than a branch/file/Actions page.
- Token authentication, repo access, writable fork membership in the `hn24987/RDP` network, and archived/disabled status.
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

## Build from source

With .NET 8 SDK:

```powershell
dotnet run --project tests/ContractTests.csproj
dotnet run --project tests/WindowsTokenTests/WindowsTokenTests.csproj # Windows only
dotnet publish desktop/RdpLauncher/RdpLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o artifacts
```

Or, after adding this source to your repository's default branch, open **Actions → Build Windows app → Run workflow**, then download **RdpLauncher-win-x64**. That build also runs the Windows token-storage smoke test. See `RELEASE_NOTES.md` for ready-to-paste release text and `RELEASE_PROMPT.md` for a reusable release-writing prompt.

The workflow creates a standard RDP user with NLA/TLS, limits its RDP firewall rule to Tailscale addresses, publishes connection metadata before keeping the session alive, and logs out of Tailscale during cleanup. Use this temporary environment for testing in accordance with GitHub Actions and Tailscale terms.
