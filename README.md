# Windows RDP via GitHub Actions & Tailscale

A simple GitHub Actions workflow to run a temporary Windows Remote Desktop (RDP) environment connected via Tailscale VPN.

## Setup Instructions

### 1. Add Tailscale Key
1. Get an Auth Key from your [Tailscale Admin Console](https://login.tailscale.com/admin/settings/keys).
2. Go to **Settings** > **Secrets and variables** > **Actions** in this repo.
3. Create a secret named `TAILSCALE_AUTH_KEY` and paste your key.

### 2. Run the RDP
1. Go to the **Actions** tab.
2. Select **RDP** workflow and click **Run workflow**.

## How to Connect

1. Ensure Tailscale is running on your device.
2. Open Remote Desktop Client (RDP) and use:
   - **Address:** `github-max-pc` (or the Tailscale IP from workflow logs)
   - **Username:** `RDP`
   - **Password:** `Password123456!`

> **Note:** The RDP service is only accessible through your private Tailscale network.
> ## Session Duration
- **Maximum Runtime:** 6 hours per run (GitHub Actions limitation).
- **Behavior:** The instance will automatically shut down and reset when the 6-hour limit is reached.
- ## Disclaimer
This project is created for **educational and testing purposes only**. 

The author is not responsible for any misuse of this workflow or any account actions taken by GitHub due to violations of GitHub Actions Terms of Service. Users are solely responsible for how they use this environment.
