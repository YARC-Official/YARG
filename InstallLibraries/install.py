# pip install requests

import requests, zipfile, io, os, shutil

PATH = os.path.dirname(os.path.realpath(__file__))
DEST_PATH = f"{PATH}/../Assets/Libraries/BassNative"
URL = "https://www.un4seen.com/files/"

def download(name, prefix=""):
	req = requests.get(f"{URL}/{prefix}{name}")
	zip = zipfile.ZipFile(io.BytesIO(req.content))
	zip.extractall(f"{PATH}/{name}")

def move(start, to):
	shutil.copy(f"{PATH}/{start}", f"{DEST_PATH}/{to}")

# Check if we are in the repo

if not os.path.exists(DEST_PATH):
	print("You are not in the YARG repo. Make sure you keep the script in the `InstallLibraries` folder.")
	exit()

# Install BASS

download("bass24.zip")
move("bass24.zip/bass.dll", "Windows/x86/bass.dll")
move("bass24.zip/x64/bass.dll", "Windows/x86_64/bass.dll")

download("bass24-linux.zip")
move("bass24-linux.zip/libs/x86_64/libbass.so", "Linux/x86_64/libbass.so")

download("bass24-osx.zip")
move("bass24-osx.zip/libbass.dylib", "Mac/libbass.dylib")

# Install BASSOPUS

download("bassopus24.zip")
move("bassopus24.zip/bassopus.dll", "Windows/x86/bassopus.dll")
move("bassopus24.zip/x64/bassopus.dll", "Windows/x86_64/bassopus.dll")

download("bassopus24-linux.zip")
move("bassopus24-linux.zip/libs/x86_64/libbassopus.so", "Linux/x86_64/libbassopus.so")

download("bassopus24-osx.zip")
move("bassopus24-osx.zip/libbassopus.dylib", "Mac/libbassopus.dylib")

# Install BASSmix

download("bassmix24.zip")
move("bassmix24.zip/bassmix.dll", "Windows/x86/bassmix.dll")
move("bassmix24.zip/x64/bassmix.dll", "Windows/x86_64/bassmix.dll")

download("bassmix24-linux.zip")
move("bassmix24-linux.zip/libs/x86_64/libbassmix.so", "Linux/x86_64/libbassmix.so")

download("bassmix24-osx.zip")
move("bassmix24-osx.zip/libbassmix.dylib", "Mac/libbassmix.dylib")

# Install BASS FX

download("bass_fx24.zip", "z/0/")
move("bass_fx24.zip/bass_fx.dll", "Windows/x86/bass_fx.dll")
move("bass_fx24.zip/x64/bass_fx.dll", "Windows/x86_64/bass_fx.dll")

download("bass_fx24-linux.zip", "z/0/")
move("bass_fx24-linux.zip/libs/x86_64/libbass_fx.so", "Linux/x86_64/libbass_fx.so")

download("bass_fx24-osx.zip", "z/0/")
move("bass_fx24-osx.zip/libbass_fx.dylib", "Mac/libbass_fx.dylib")

# Clean up

shutil.rmtree(f"{PATH}/bass24.zip")
shutil.rmtree(f"{PATH}/bass24-linux.zip")
shutil.rmtree(f"{PATH}/bass24-osx.zip")
shutil.rmtree(f"{PATH}/bassopus24.zip")
shutil.rmtree(f"{PATH}/bassopus24-linux.zip")
shutil.rmtree(f"{PATH}/bassopus24-osx.zip")
shutil.rmtree(f"{PATH}/bassmix24.zip")
shutil.rmtree(f"{PATH}/bassmix24-linux.zip")
shutil.rmtree(f"{PATH}/bassmix24-osx.zip")
shutil.rmtree(f"{PATH}/bass_fx24.zip")
shutil.rmtree(f"{PATH}/bass_fx24-linux.zip")
shutil.rmtree(f"{PATH}/bass_fx24-osx.zip")

print("Done!")