#!/usr/bin/env python3
"""Download the prebuilt zvec C API library into runtimes/<rid>/native.

Upstream publishes an SDK archive per platform on every release, so nothing is
built from source here. Usage:

    python native/fetch-native.py            # this machine's platform
    python native/fetch-native.py linux-x64  # one or more RIDs
    python native/fetch-native.py all        # everything the NuGet package ships

Only zvec_c_api is taken from the archive: it has zvec itself linked in. The
archives also carry zvec.dll / libzvec.so, and on Windows a native zvec.dll next
to the managed Zvec.dll overwrites it (same name, case-insensitive file system).
"""

import hashlib
import io
import os
import platform
import sys
import tarfile
import urllib.request
import zipfile

TAG = "v0.8.1"
BASE = "https://github.com/alibaba/zvec/releases/download/" + TAG + "/"

# rid -> (release asset, sha256 of the asset, library file inside lib/)
# Bump TAG and refresh the hashes together (sha256sum of each downloaded asset).
ASSETS = {
    "win-x64": (
        "zvec-sdk-windows-amd64.zip",
        "5fdc8d9cdfa6c90002eb27d0fa790649eb837c8e44fc29b143ed6811e2fd2b0f",
        "zvec_c_api.dll",
    ),
    "linux-x64": (
        "zvec-sdk-linux-amd64.tar.gz",
        "cd4e4051b9baddb65896e73f2921a21e27fbf71711bd21eaedb58affaa93d1b9",
        "libzvec_c_api.so",
    ),
    "linux-arm64": (
        "zvec-sdk-linux-arm64.tar.gz",
        "12444390d1ce352c60b7b798ba6255e862255fb922d7ac23db791ec436b9f3a5",
        "libzvec_c_api.so",
    ),
    "osx-arm64": (
        "zvec-sdk-osx-arm64.tar.gz",
        "583d0cd6a0eca3336121b5d134edc5cdefaf169ac015da35fb0f167df7a52697",
        "libzvec_c_api.dylib",
    ),
}

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def current_rid():
    system = {"Windows": "win", "Linux": "linux", "Darwin": "osx"}[platform.system()]
    machine = platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
    return system + "-" + arch


def extract(data, asset, lib):
    """Return the bytes of lib/<lib> from the archive."""
    if asset.endswith(".zip"):
        with zipfile.ZipFile(io.BytesIO(data)) as zf:
            for info in zf.infolist():
                # the Windows archive uses backslashes as path separators
                if info.filename.replace("\\", "/") == "lib/" + lib:
                    return zf.read(info)
    else:
        with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as tf:
            for member in tf:
                if member.isfile() and member.name.lstrip("./") == "lib/" + lib:
                    return tf.extractfile(member).read()
    raise SystemExit("ERROR: lib/" + lib + " not found in " + asset)


def fetch(rid):
    asset, sha, lib = ASSETS[rid]
    print("Downloading " + asset + " (" + TAG + ")...")
    with urllib.request.urlopen(BASE + asset) as resp:
        data = resp.read()

    got = hashlib.sha256(data).hexdigest()
    if got != sha:
        raise SystemExit("ERROR: sha256 mismatch for " + asset + "\n  expected " + sha + "\n  got      " + got)

    out_dir = os.path.join(ROOT, "runtimes", rid, "native")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, lib)
    with open(out, "wb") as f:
        f.write(extract(data, asset, lib))
    print("  -> " + os.path.relpath(out, ROOT) + " (" + str(os.path.getsize(out) // (1024 * 1024)) + " MB)")


def main():
    rids = sys.argv[1:] or [current_rid()]
    if rids == ["all"]:
        rids = list(ASSETS)
    for rid in rids:
        if rid not in ASSETS:
            raise SystemExit("ERROR: unknown rid '" + rid + "'. Known: " + ", ".join(ASSETS))
    for rid in rids:
        fetch(rid)


if __name__ == "__main__":
    main()
