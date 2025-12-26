import sys
import os
import time
import threading
import numpy as np # Added for data type enforcement
from pedalboard import Pedalboard, Gain, Limiter
from pedalboard.io import AudioFile
import sounddevice as sd

# --- CONFIGURATION ---
# Force 'high' latency and a fixed blocksize to prevent buffer underruns (crackling)
sd.default.latency = 'high'
sd.default.blocksize = 2048 
sd.default.dtype = 'float32'

# --- INTELLIGENT PATH DETECTION ---
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SOUNDS_DIR = os.path.join(SCRIPT_DIR, "sounds")

if not os.path.exists(SOUNDS_DIR):
    SOUNDS_DIR = os.path.join(SCRIPT_DIR, "..", "sounds")

SOUNDS_DIR = os.path.abspath(SOUNDS_DIR)

# --- AUDIO ENGINE SETUP ---
# REMOVED: HighpassFilter (Often causes phase crackle on deep bass sounds)
# UPDATED: Gain lowered to -8.0dB for extra headroom
board = Pedalboard([
    Gain(gain_db=-8.0),
    Limiter(threshold_db=-0.5)
])

def play_specific_file(filename):
    try:
        # 1. IMMEDIATE INTERRUPT
        # Stop current sound instantly so the new button press takes over
        sd.stop()

        filepath = os.path.join(SOUNDS_DIR, filename)
        if not os.path.exists(filepath):
            return

        # 2. LOAD & FORMAT
        with AudioFile(filepath) as f:
            audio = f.read(f.frames)
            samplerate = f.samplerate
        
        # Force float32 to ensure smooth processing
        audio = audio.astype(np.float32)

        # 3. PROCESS
        effected = board(audio, samplerate)

        # 4. PLAYBACK
        # We allow the driver to handle the sample rate.
        # blocking=True ensures the thread stays alive to finish playback.
        sd.play(effected.T, samplerate=samplerate, blocking=True)

    except Exception:
        pass

# --- COMMAND LISTENER ---
def main():
    sys.stdout.write("READY\n")
    sys.stdout.flush()

    while True:
        try:
            line = sys.stdin.readline()
            if not line:
                break
            
            cmd = line.strip().upper()
            
            if cmd == "PLAY_OPEN":
                t = threading.Thread(target=play_specific_file, args=("WormholeOpen.mp3",))
                t.daemon = True
                t.start()

            elif cmd == "PLAY_CLOSE":
                t = threading.Thread(target=play_specific_file, args=("WormholeClose.mp3",))
                t.daemon = True
                t.start()
                
            elif cmd == "MUTE_TOGGLE":
                sd.stop()
                pass
                
        except KeyboardInterrupt:
            break

if __name__ == "__main__":
    main()