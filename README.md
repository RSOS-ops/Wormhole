# Wormhole Console 🌌
Wormhole Sunshine and Tailscale Utility

Wormhole Console is a cinematic, sci-fi themed controller for managing remote access services on Windows. It provides a centralized, audio-reactive interface to toggle Tailscale VPN and Sunshine Streamer with a single click.

🚀 Overview
Managing remote desktop services often involves digging through system trays and service menus. Wormhole turns this into a cinematic experience.

When you "Open the Wormhole," the console handles the heavy lifting of starting background services, while providing real-time visual and auditory feedback through a high-performance WebGL animation and a native DSP audio engine.

Key Features
Persistent Monitoring: Auto-detects the state of Tailscale and Sunshine. If services are manually stopped elsewhere, the console reflects it instantly.

Cinematic HUD: A WebView2-powered wormhole animation that reacts to service states (static when offline, warp-speed when online).

Atmospheric Audio: A Python-based audio engine that plays randomized, cathedral-reverb processed sound effects upon state changes.

Service Enforcement: Automatically sets Sunshine and Tailscale to "Manual" startup in Windows to ensure they only run when you want them to.

Native Dragging: A borderless, minimalist UI that can be moved anywhere on your desktop.

🛠 Prerequisites
Before running the console, ensure the following are installed on your system:

Tailscale: Download here (Required for secure networking).

Sunshine: Download here (Required for low-latency streaming).

WebView2 Runtime: Most modern Windows systems have this, but you can get it here if needed.

📦 Installation & Usage
1. Build the Application
If you are downloading the source code:

Navigate to the root folder.

Run BUILD_APP.bat. This will compile the C# source into WormholeConsole.exe and embed the custom wormhole icon.

2. Launching
Run as Administrator: Because the application controls Windows Services, it must be launched with Administrator privileges.

The console will launch in a "Standby" state. If your services are currently off, you will see the SYSTEMS OFFLINE status.

3. Controls
OPEN WORMHOLE: Starts Tailscale and Sunshine in sequence.

COLLAPSE WORMHOLE: Stops both services and puts the interface back into standby.

MUTE SFX: Toggles the atmospheric audio engine if you prefer silence.

DRAG: Click and hold any black area of the console to move the window.

📂 Project Structure
/src: Contains Wormhole.cs (C# Controller) and vst_engine.py (Audio Engine).

/assets: HTML/CSS/JS for the wormhole animation.

/sounds: MP3 library for the audio engine.

Check_Prerequisites.ps1: Utility to verify if required services are installed.

AUTO_INSTALL_DEPENDENCIES.ps1: Automated installer using Winget.

⚖️ License
This project is licensed under the GNU GPLv3.

This project utilizes the pedalboard library by Spotify, which is also licensed under GPLv3. Accordingly, this software is open-source and must remain so in any distributed modifications.

☕ Support / Donationware
Wormhole Console is free to use and open-source. If you find this tool helpful for your remote gaming or dev setup, consider supporting the developer:

[Insert your Ko-fi/PayPal/Patreon link here]