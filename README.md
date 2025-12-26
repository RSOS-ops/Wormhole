Wormhole Console 🌌
Wormhole Sunshine and Tailscale Utility

Wormhole Console is a cinematic, sci-fi themed controller for managing remote access services on Windows. It provides a centralized, audio-reactive interface to toggle Tailscale VPN and Sunshine Streamer with a single click.

Instead of digging through service menus or system trays, Wormhole turns network management into a tactile experience.

***
  
If you're finding this tool useful, consider buying me a coffee!  
Cashapp: [https://cash.me/$tmjiii](https://cash.me/$tmjiii)  
Or via Venmo: [Venmo](https://venmo.com/tmjiii)  
or via PayPal: [PayPal](https://paypal.me/chipjohnson)  

---- OR - If you're on the right side of the new world order ---  
  
ETH: `0xA6921320f87Ba53f40570edfbc8584b11F43613D`  
BTC: `bc1q3f6kfh7nmyuh4wwl09a7sseha2avqjngprp3h8`  
  
Much love! ❤️💖💕💓💜💝  
**OPS at The Royal Society of Summoners**
  
***  

🚀 Key Features
One-Click Control: Starts/Stops Tailscale and Sunshine in sequence with visual feedback.

Persistent Monitoring: Auto-detects service states. If a service crashes or is stopped externally, the console updates instantly.

Cinematic HUD: A WebView2-powered wormhole animation that reacts to service states (static when offline, warp-speed when online).

Atmospheric Audio: A Python-based audio engine that plays randomized, reverb-processed sound effects during interactions.

Service Enforcement: Automatically sets Sunshine and Tailscale to "Manual" startup in Windows so they don't consume resources when not in use.

System Tray Integration: Minimizes silently to the system tray to keep your desktop clean.

🛠 Prerequisites
Before running the console, ensure these 3 components are installed. The app controls these services but does not install them for you.

Tailscale: Download Here (Required for secure networking).

Sunshine: Download Here (Required for low-latency streaming).

Python 3.x: Download Here (Required for the audio engine).

Note: During installation, check the box "Add Python to PATH".

📦 Installation & Setup
1. Download & Compile
Since this is open-source, you will compile the application yourself. It takes about 10 seconds.

Download the Source Code (Code -> Download ZIP) and extract it.

Install Python Dependencies: Open the src folder, right-click and select "Open Terminal Here" (or use Command Prompt). Run:

PowerShell

pip install pedalboard
Build the App: Go back to the root folder and double-click BUILD_APP.bat.

This script compiles the C# code into WormholeConsole.exe and links all assets.

2. First Launch
Important: Because Wormhole controls Windows Services, it must be run as Administrator.

Right-click WormholeConsole.exe.

Select Run as Administrator.

The console will launch in "Standby" mode.

🎮 Controls
OPEN WORMHOLE: Starts Tailscale and Sunshine. The animation will accelerate to warp speed.

COLLAPSE WORMHOLE: Stops both services. The animation will decelerate to a halt.

MUTE SFX: Toggles the audio engine if you prefer silence.

MINIMIZE: Clicking the minimize button sends the app to the System Tray. Double-click the tray icon to bring it back.

DRAG: Click and hold any blank area of the window to move it.

⚡ Pro Tip: Creating a UAC-Free Shortcut
Since the app requires Admin rights, Windows will normally ask for permission (the UAC popup) every time you open it. You can bypass this securely:

Create a Task:

Open Task Scheduler in Windows.

Click "Create Task" -> Name it WormholeLauncher.

Check "Run with highest privileges".

Actions Tab: New -> Start a program -> Browse to your WormholeConsole.exe.

Create the Shortcut:

Right-click on your Desktop -> New -> Shortcut.

Paste this into the location box: schtasks /run /tn "WormholeLauncher"

Name it Wormhole Console.

Change Icon (Optional):

Right-click the new shortcut -> Properties -> Change Icon.

Browse to the assets/wormhole.ico file in your project folder.

Result: You now have a desktop shortcut that launches the app instantly with Admin rights and zero popups.

📂 Project Structure
/src: Source code (Wormhole.cs controller and vst_engine.py audio logic).

/assets: HTML/CSS/JS for the visual interface, font files, and icons.

/sounds: MP3 library for the audio engine.

BUILD_APP.bat: The one-click compiler script.

⚖️ License
This project is licensed under the GNU GPLv3.

This software uses the pedalboard library by Spotify, which is also licensed under GPLv3. Accordingly, this software is open-source and must remain so in any distributed modifications.

☕ Support
Wormhole Console is free, open-source software. If you find this tool helpful for your remote gaming or dev setup, consider checking out my other projects or supporting the development!

email: ops@theroyalsummoners.com