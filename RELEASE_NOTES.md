# RDP Launcher v1.3.0 — Dark dashboard and optional password

Created by hn24987.

- Dark navy dashboard with sidebar navigation and separate connection, session, transfer and activity cards. Token and Settings dialogs use the same dark design.
- English is the first-launch default. A previously saved English/Vietnamese choice is preserved.
- Optional password field. Leave it blank for **123456**. The workflow no longer requires RDP_PASSWORD or a 14-character complex password.
- Windows password complexity is configured on the temporary runner to accept the selected password. RDP remains scoped to Tailscale.
- Custom passwords are encrypted into per-session GitHub secrets, not public workflow inputs. Custom passwords need token Secrets read/write permission. Temporary secrets are removed after the connection is ready; resume the launcher or delete them in repository Settings if it was closed early.

The Windows account accepts up to its native 127-character limit. Passwords are not saved beside the app. Existing session, update, Taildrop and bilingual features remain.

Builds/tests do not substitute for live RDP, transfer or ARM64 device testing. The EXE remains unsigned.
