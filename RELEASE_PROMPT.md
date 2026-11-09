Write clear, concise GitHub Release notes in English for "RDP Launcher v1.1.0 — Bring Your Own Fork".

Use only the facts in README.md and the current implementation. Include:
- Created by hn24987 and source link https://github.com/hn24987/RDP.
- The download name RDP-Launcher-v1.1.0-Windows-x64.zip and supported Windows 10/11 x64 platform.
- The required first-time flow: fork the source project, enable Actions in the fork, add TAILSCALE_AUTH_KEY and RDP_PASSWORD repository secrets, install/connect Tailscale on the same tailnet, create a GitHub token scoped to the fork, paste the fork URL and click Get RDP.
- Token permissions: Actions read/write; Contents, Secrets and Administration read-only.
- Fork verification, parallel preflight, Tailscale installation/login/connection checks, fully English UI, automatic run monitoring and IP retrieval.
- First-launch token prompt; Windows DPAPI-encrypted github-token.dat next to the EXE; automatic reload; Change/Forget token; not portable across Windows accounts/machines.
- Update instructions for older forks and workflows.
- A short explanation that closing the app does not stop the workflow and that secrets do not carry over when forking.
- Only testing that actually ran. Distinguish successful builds/contract tests from Windows UI, DPAPI and live RDP validation. Do not claim production-ready or fully end-to-end tested without evidence.
- Relevant limits: unsigned EXE; temporary sessions/storage; preflight cannot inspect secret values or guarantee quota, billing, key validity, device approval or ACL connectivity.

Use a descriptive title, short introduction, What's new, Quick start, Updating and Notes. Return ready-to-paste Markdown. Never include real tokens, keys, passwords or account-specific settings.
