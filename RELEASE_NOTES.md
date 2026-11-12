# RDP Launcher v1.3.0 — Dark dashboard and optional password

Created by [hn24987](https://github.com/hn24987). [Source code](https://github.com/hn24987/RDP-Launcher).

- Dark navy dashboard with sidebar navigation and separate connection, session, transfer and activity cards. Token and Settings dialogs use the same dark design.
- English is the first-launch default. A previously saved English/Vietnamese choice is preserved.
- Optional password field. Leave it blank for **123456**. The workflow no longer requires RDP_PASSWORD or a 14-character complex password.
- Windows password complexity is configured on the temporary runner to accept the selected password. RDP remains scoped to Tailscale.
- Custom passwords are encrypted into per-session GitHub secrets, not public workflow inputs. Custom passwords need token Secrets read/write permission. Temporary secrets are removed after the connection is ready; resume the launcher or delete them in repository Settings if it was closed early.

The Windows account accepts up to its native 127-character limit. Passwords are not saved beside the app. Existing session, update, Taildrop and bilingual features remain.

Builds/tests do not substitute for live RDP, transfer or ARM64 device testing. The EXE remains unsigned.

## Update

Replace the old EXE with the v1.3.0 build for your Windows architecture. Keep the existing encrypted token file beside it if you want to reuse your saved sign-in on the same Windows account. Sync your fork's `.github/workflows/main.yml` from this release before using the new launcher. The workflow only requires `TAILSCALE_AUTH_KEY`; a password secret is no longer a prerequisite. Start a new workflow run after updating, rather than re-running a failed run from an older commit.

The token needs Secrets read permission for setup checks. Custom passwords additionally need Secrets write permission; leave the password blank if you want the default without secret writes. A custom password is not retained locally after the app closes.

## Verification

Automated checks cover API contracts, sealed password encryption, English/Vietnamese preferences, native Windows UI construction and token persistence. Windows account creation was tested with both the default password and a one-character custom password using the policy block from the actual workflow. Both x64 and ARM64 packages build successfully; ARM64 is cross-compiled. Native UI previews are captured on Windows. Live remote desktop login and file transfer were not tested in this change.
