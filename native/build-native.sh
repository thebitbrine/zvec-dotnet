#!/bin/bash
# Build zvec native library from source on Linux/macOS.
# Requires: cmake 3.13+, gcc/clang

set -e

TAG="${1:-v0.4.0}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/_build"
SRC_DIR="$BUILD_DIR/zvec"

# Detect platform
if [[ "$(uname)" == "Darwin" ]]; then
    RID="osx-arm64"
    LIB_EXT="dylib"
else
    RID="linux-x64"
    LIB_EXT="so"
fi

OUT_DIR="$SCRIPT_DIR/../runtimes/$RID/native"

# Clone if not already present
if [ ! -d "$SRC_DIR/.git" ]; then
    echo "Cloning zvec $TAG..."
    rm -rf "$SRC_DIR"
    git clone --branch "$TAG" --recurse-submodules https://github.com/alibaba/zvec.git "$SRC_DIR"
else
    echo "zvec source already present, updating submodules..."
    git -C "$SRC_DIR" submodule update --init --recursive
fi

# Configure
CMAKE_BUILD_DIR="$BUILD_DIR/cmake-build"
echo "CMake version: $(cmake --version | head -1)"
echo "Configuring cmake..."
# CMAKE_POLICY_VERSION_MINIMUM works around older thirdparty CMakeLists
# that use cmake_minimum_required(VERSION 2.x) which cmake 4+ rejects
cmake -S "$SRC_DIR" -B "$CMAKE_BUILD_DIR" \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_C_BINDINGS=ON \
    -DBUILD_PYTHON_BINDINGS=OFF \
    -DBUILD_TOOLS=OFF \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -Wno-dev

# Build
echo "Building zvec_c_api..."
cmake --build "$CMAKE_BUILD_DIR" --config Release --target zvec_c_api --parallel

# Copy output
mkdir -p "$OUT_DIR"
LIB_FILE=$(find "$CMAKE_BUILD_DIR" -name "libzvec_c_api.$LIB_EXT" | head -1)
if [ -z "$LIB_FILE" ]; then
    echo "ERROR: libzvec_c_api.$LIB_EXT not found in build output"
    exit 1
fi

cp "$LIB_FILE" "$OUT_DIR/"
echo "Copied $LIB_FILE -> $OUT_DIR/"

echo "Done. Native library at: $OUT_DIR/libzvec_c_api.$LIB_EXT"
