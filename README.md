<div align="center">

# RDP Launcher

### Connect to a temporary Windows computer from your own PC.

Create a Windows desktop on GitHub Actions, access it through Tailscale, and control it using Remote Desktop. RDP Launcher helps you set up the connection and start each session.

**Created by [hn24987](https://github.com/hn24987)**

[**Download for Windows**](https://github.com/hn24987/RDP-Launcher/releases/latest/download/RdpLauncher.exe) · [Latest release](https://github.com/hn24987/RDP-Launcher/releases/latest) · [Setup guide](docs/SETUP.md)

**Windows 10 / 11 · x64 · English interface · No installer required**

</div>

---

## What is RDP?

**RDP stands for Remote Desktop Protocol.** It lets you see and control another computer over a network. You get a Windows desktop in a window on your own PC, where you can open applications, work with files, and use the mouse and keyboard as if you were sitting in front of that remote machine.

The applications run on the **remote computer**. Your PC sends your keyboard and mouse input and displays the remote screen. Both computers need a working network connection.

## What can you use this project for?

This project gives you a **temporary Windows environment** for short learning, development, and testing sessions. For example, you can:

- **Test Windows applications and scripts** in a fresh environment without changing your own Windows installation.
- **Check Windows-specific behavior**, such as file paths, command-line tools, and application compatibility.
- **Learn how remote desktops work**, including connecting, signing in, and managing a remote session.
- **Practice connecting devices with Tailscale** and checking whether your remote desktop is reachable.

The machine is temporary. Save any files you need before the session ends; a new workflow run creates a fresh environment. Available resources and runner usage depend on GitHub's runner configuration, your account limits, and applicable usage policies.

## Where does the Windows computer come from?

Your fork contains a **GitHub Actions workflow** that starts a Windows runner — a temporary machine hosted by GitHub. The workflow enables Remote Desktop, creates your Windows sign-in account, and connects the runner to your Tailscale network.

**Tailscale** gives your PC and the runner private network addresses so they can reach each other. You then use **Windows Remote Desktop** to connect to the runner's address and use its desktop.

The connection looks like this:

**Your PC → Tailscale network → Windows runner on GitHub Actions**

Your own computer runs the launcher and the Remote Desktop client. The remote Windows environment runs on GitHub's infrastructure.

## What does RDP Launcher do?

RDP Launcher is the Windows app that manages this process. Once you have prepared **a repository you control**, you can press **Get RDP** to check your setup, start the workflow, follow its progress, and get the remote machine's IP address.

When the machine is ready, click **Open Remote Desktop** and sign in. The launcher also helps you return to an existing session or stop its workflow when you finish.

## What you get

- **One place to launch and connect.** Start the workflow, see its progress, copy the IP, and open Remote Desktop.
- **Checks before startup.** Find missing secrets, disabled workflows, or GitHub access problems before starting a session.
- **Tailscale readiness checks.** See whether Tailscale is installed, signed in, connected, and online.
- **Your setup, remembered.** Your fork and session details are saved. Your GitHub token is encrypted for your Windows account and loaded on the next launch.
- **Session controls.** Choose a duration, resume monitoring an existing run, or stop the workflow when you finish.

## Get started

### 1. Download the app

**[Download RdpLauncher.exe](https://github.com/hn24987/RDP-Launcher/releases/latest/download/RdpLauncher.exe)** and place it in a folder you can write to, such as a new folder in Downloads. Open it to get started. You do not need to install Python or .NET.

Want the app together with instructions and the workflow file? Get the **[complete ZIP from Releases](https://github.com/hn24987/RDP-Launcher/releases/latest)**.

### 2. Prepare your repository — once

**Project owner:** use `https://github.com/hn24987/RDP-Launcher` directly with a GitHub token that has access to it. You do not need to fork your own project.

**Other users:** [fork this project](https://github.com/hn24987/RDP-Launcher/fork) into your account. Then set up the following in the repository you will run:

| What you need | What it does |
| --- | --- |
| GitHub Actions enabled in your fork | Runs the temporary Windows machine. |
| `TAILSCALE_AUTH_KEY` repository secret | Connects the remote machine to your Tailscale network. |
| `RDP_PASSWORD` repository secret | Sets the password you will use to sign in to Windows. |
| Tailscale installed and connected on your PC | Lets your PC reach the remote machine. |
| A GitHub token with access to your fork | Lets the app check your setup and start the workflow. |

**New here? Follow the [step-by-step setup guide](docs/SETUP.md#first-time-setup).** It shows where to create each key, which token permissions to choose, and where to add your secrets.

> Use the **source repository URL (with write access) or your fork URL** in the app, such as `https://github.com/YOUR-USERNAME/RDP`. GitHub does not copy secrets when you fork, so you must add your own. Both devices need to use the same Tailscale network.

### 3. Get your RDP session

1. Open `RdpLauncher.exe` and enter your GitHub token when prompted on the first launch.
2. Paste **your repository URL**, choose a session duration, and click **Get RDP**.
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
- [Report an issue](https://github.com/hn24987/RDP-Launcher/issues)

Use this project for testing in accordance with GitHub Actions and Tailscale terms.
