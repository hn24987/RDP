<div align="center">

# RDP Launcher

### A simpler way to start your Windows remote desktop.

Launch a temporary Windows session, follow its startup, and get your connection IP — all from one desktop app.

**Created by [hn24987](https://github.com/hn24987)**

[**Download for Windows**](https://github.com/hn24987/RDP/releases/latest/download/RdpLauncher.exe) · [Latest release](https://github.com/hn24987/RDP/releases/latest) · [Setup guide](docs/SETUP.md)

**Windows 10 / 11 · x64 · English interface · No installer required**

</div>

---

## Meet RDP Launcher

RDP Launcher brings your GitHub Actions RDP workflow into a Windows app. Connect your GitHub account, add the link to **your own fork**, and press **Get RDP**. The app checks your setup, starts the workflow, and displays the IP when your remote desktop is ready.

Your PC and the remote machine connect through **Tailscale**, a private network that lets your devices reach each other. Once the session is ready, open Windows Remote Desktop straight from the app.

## What you get

- **One place to launch and connect.** Start the workflow, see its progress, copy the IP, and open Remote Desktop.
- **Checks before startup.** Find missing secrets, disabled workflows, or GitHub access problems before starting a session.
- **Tailscale readiness checks.** See whether Tailscale is installed, signed in, connected, and online.
- **Your setup, remembered.** Your fork and session details are saved. Your GitHub token is encrypted for your Windows account and loaded on the next launch.
- **Session controls.** Choose a duration, resume monitoring an existing run, or stop the workflow when you finish.

## Get started

### 1. Download the app

**[Download RdpLauncher.exe](https://github.com/hn24987/RDP/releases/latest/download/RdpLauncher.exe)** and place it in a folder you can write to, such as a new folder in Downloads. Open it to get started. You do not need to install Python or .NET.

Want the app together with instructions and the workflow file? Get the **[complete ZIP from Releases](https://github.com/hn24987/RDP/releases/latest)**.

### 2. Prepare your fork — once

**[Fork this project](https://github.com/hn24987/RDP/fork)** into your GitHub account, then set up:

| What you need | What it does |
| --- | --- |
| GitHub Actions enabled in your fork | Runs the temporary Windows machine. |
| `TAILSCALE_AUTH_KEY` repository secret | Connects the remote machine to your Tailscale network. |
| `RDP_PASSWORD` repository secret | Sets the password you will use to sign in to Windows. |
| Tailscale installed and connected on your PC | Lets your PC reach the remote machine. |
| A GitHub token with access to your fork | Lets the app check your setup and start the workflow. |

**New here? Follow the [step-by-step setup guide](docs/SETUP.md#first-time-setup).** It shows where to create each key, which token permissions to choose, and where to add your secrets.

> Use **your fork's URL** in the app, such as `https://github.com/YOUR-USERNAME/RDP`. GitHub does not copy secrets when you fork, so you must add your own. Both devices need to use the same Tailscale network.

### 3. Get your RDP session

1. Open `RdpLauncher.exe` and enter your GitHub token when prompted on the first launch.
2. Paste **your fork URL**, choose a session duration, and click **Get RDP**.
3. Wait for the IP, then click **Open Remote Desktop**.
4. Sign in as **`.\RDP`** using the password you saved in **`RDP_PASSWORD`**.

After the initial setup, the app remembers your token and fork. You can also use **Check setup** to check your configuration without starting a machine.

## When you're finished

Click **Stop workflow** to end the remote session. Closing the app or choosing **Stop monitoring** leaves the workflow running; use **Resume run** if you want to return to it.

Sessions last **10–300 minutes**, depending on your selection. The remote machine and its files are temporary, so save anything you need before the session ends. Runner availability and usage depend on your GitHub account's limits and policies.

## Need a hand?

- [Full setup and connection guide](docs/SETUP.md)
- [Troubleshooting](docs/SETUP.md#troubleshooting)
- [Token storage and privacy](docs/SETUP.md#token-storage)
- [Build from source](docs/SETUP.md#build-from-source)
- [Release notes](RELEASE_NOTES.md)
- [Report an issue](https://github.com/hn24987/RDP/issues)

Use this project for testing in accordance with GitHub Actions and Tailscale terms.
