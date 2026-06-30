# Build zvec native library (zvec_c_api.dll) from source on Windows.
# Requires: cmake 3.13+, Visual Studio 2022 (MSVC)

param(
    [string]$Tag = "v0.5.1",
    [string]$OutDir = "$PSScriptRoot\..\runtimes\win-x64\native"
)

$ErrorActionPreference = "Stop"

$BuildDir = "$PSScriptRoot\_build"
$SrcDir = "$BuildDir\zvec"

# Clone if not already present
if (-not (Test-Path "$SrcDir\.git")) {
    Write-Host "Cloning zvec $Tag..."
    if (Test-Path $SrcDir) { Remove-Item $SrcDir -Recurse -Force }
    git clone --branch $Tag --recurse-submodules https://github.com/alibaba/zvec.git $SrcDir
    if ($LASTEXITCODE -ne 0) { throw "git clone failed" }
} else {
    Write-Host "zvec source already present at $SrcDir, updating submodules..."
    git -C $SrcDir submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) { throw "submodule update failed" }
}

# Configure
$CmakeBuildDir = "$BuildDir\cmake-build"
Write-Host "Configuring cmake..."
cmake -S $SrcDir -B $CmakeBuildDir `
    -A x64 `
    -DCMAKE_BUILD_TYPE=Release `
    -DBUILD_C_BINDINGS=ON `
    -DBUILD_PYTHON_BINDINGS=OFF `
    -DBUILD_TOOLS=OFF `
    "-DCMAKE_POLICY_VERSION_MINIMUM=3.5" `
    -Wno-dev

if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }

# Build
Write-Host "Building zvec_c_api..."
cmake --build $CmakeBuildDir --config Release --target zvec_c_api --parallel 2
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

# Copy output
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Find the DLL -- could be in Release subdir or directly in build dir
$dll = Get-ChildItem $CmakeBuildDir -Recurse -Filter "zvec_c_api.dll" | Select-Object -First 1
if (-not $dll) { throw "zvec_c_api.dll not found in build output" }

Copy-Item $dll.FullName "$OutDir\zvec_c_api.dll" -Force
Write-Host "Copied $($dll.FullName) -> $OutDir\zvec_c_api.dll"

# Also copy any dependency DLLs that might be needed
$dllDir = $dll.DirectoryName
Get-ChildItem $dllDir -Filter "*.dll" | Where-Object { $_.Name -ne "zvec_c_api.dll" } | ForEach-Object {
    Copy-Item $_.FullName "$OutDir\$($_.Name)" -Force
    Write-Host "Copied dependency: $($_.Name)"
}

Write-Host "Done. Native library at: $OutDir\zvec_c_api.dll"
