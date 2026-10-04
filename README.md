# Ryzom Modern — Cloud-Native 3D MMORPG Platform

[![.NET 8 CI Pipeline](https://github.com/kylethiems/RyzomModern/actions/workflows/ci.yml/badge.svg)](https://github.com/kylethiems/RyzomModern/actions/workflows/ci.yml)
[![.NET Core](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![xUnit](https://img.shields.io/badge/tests-14%20passed-brightgreen.svg)](https://github.com/kylethiems/RyzomModern)
[![Graphics](https://img.shields.io/badge/Render-WebGPU%20%2F%20HTML5-orange.svg)](https://github.com/kylethiems/RyzomModern)
[![Spatial](https://img.shields.io/badge/Spatial-Morton--64%20Z--Order-purple.svg)](https://github.com/kylethiems/RyzomModern)

**Ryzom Modern** is an automated, cloud-native modernization of **[Ryzom](https://github.com/ryzom/ryzom-core)** (The Saga of Ryzom)—the only commercial 3D MMORPG in history to release its complete C++ client, server, and **13+ gigabytes of artistic 3D assets** to the public domain under Creative Commons.

This project transitions the legacy 2004 C++98 / NeL (Nevrax Library) monolith into a modern, high-throughput **.NET 8 / WebGPU** spatial computing architecture powered by functional data compression and LiDAR point-cloud decimation techniques.

---

## 🏛️ System Architecture

```
                      ┌────────────────────────────────────────┐
                      │          Ryzom WebGPU Client           │
                      │  (HTML5 / WebAssembly / WebGPU Canvas) │
                      └───────────────────▲────────────────────┘
                                          │ WebSockets (Delta Packets)
                      ┌───────────────────▼────────────────────┐
                      │          Ryzom.Server Gateway          │
                      │   (ASP.NET Core 8 / Kestrel / gRPC)    │
                      └───────────────────┬────────────────────┘
                                          │
                      ┌───────────────────▼────────────────────┐
                      │              Ryzom.Engine              │
                      │  - Morton-64 Z-Order Spatial Grid      │
                      │  - Fast Slab Ray-AABB Collision        │
                      │  - Action Stanza Waterfall Engine      │
                      └───────────────────┬────────────────────┘
                                          │
                      ┌───────────────────▼────────────────────┐
                      │               Ryzom.Core               │
                      │  - SIMD 3D Math (Vector3f, Quat, AABB) │
                      │  - Zero-Alloc Span<byte> BitStream     │
                      │  - LiDAR Voxel Mesh Decimation Engine  │
                      └────────────────────────────────────────┘
```

---

## 🔬 Core Innovations

### 1. Morton-64 Z-Order Spatial Hashing (`Ryzom.Core.Spatial`)
* Eliminates pointer-heavy recursive Octrees (`Node->Children[8]`) that cause CPU cache thrashing during mass player battles.
* 3D coordinates $(x,y,z)$ are bit-interleaved into a single 64-bit integer, guaranteeing that physically adjacent entities reside on **the same CPU cache line**.
* Enables $O(1)$ spatial radius queries across thousands of active entities.

### 2. LiDAR Point-Cloud & Voxel Mesh Decimation (`Ryzom.Core.Formats`)
* Applies LiDAR voxel grid centroid filtering to legacy 2004 NeL meshes:
  * Welds duplicate and sub-millimeter noisy vertices to their voxel centroid.
  * Filters out non-manifold degenerate zero-area triangles.
  * Streams WebGPU-ready interleaved vertex buffers (`Pos.xyz`, `Normal.xyz`, `UV.xy`) directly to browser canvas renderers.

### 3. Zero-Allocation Delta Netcode (`Ryzom.Core.Compression`)
* Fixed-point 3D lattice quantizer packs 12-byte float32 coordinates into **3-to-4 byte delta offsets**.
* Operates strictly over `Span<byte>` and `ReadOnlySpan<byte>` with zero garbage collector allocations during real-time network ticks.

---

## 🚀 Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Modern web browser with WebGPU/WebGL support

### Build & Run
```bash
# Clone the repository
git clone https://github.com/kylethiems/RyzomModern.git
cd RyzomModern

# Restore and build solution
dotnet restore
dotnet build --configuration Release --no-restore

# Run automated test suites (14 tests)
dotnet test --configuration Release --verbosity normal

# Launch Ryzom Server Gateway
dotnet run --project Ryzom.Server
```

To run the WebGPU client, simply open `Ryzom.Client.Web/index.html` in any modern browser.

---

## 🧪 Automated Test Verification

The test harness covers all foundational 3D mathematical, spatial, netcode, and combat operations:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.13]   Starting:    Ryzom.Tests
  Passed Ryzom.Tests.VectorMathTests.Quaternion_RotatesVector90DegreesAroundZ
  Passed Ryzom.Tests.RayAABBCollisionTests.RayHitsBox_ReturnsTrue
  Passed Ryzom.Tests.LatticeQuantizationTests.PackUnpack10Bit_PreservesBoundedPosition
  Passed Ryzom.Tests.MortonSpatialTests.EncodeDecode_RoundtripsAccurately
  Passed Ryzom.Tests.RayAABBCollisionTests.RayMissesBox_ReturnsFalse
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_TargetOutOfRange_FailsWithoutCost
  Passed Ryzom.Tests.LatticeQuantizationTests.PackUnpackDelta16Bit_PreservesCentimeterPrecision
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_ConsumesSap_AndDamagesTarget
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_InsufficientSap_FailsExecution
  Passed Ryzom.Tests.VectorMathTests.DotAndCrossProducts_SatisfyOrthogonality
  Passed Ryzom.Tests.VectorMathTests.Lerp_InterpolatesAccurately
  Passed Ryzom.Tests.VectorMathTests.VectorAdditionAndLength_BehavesCorrectly
  Passed Ryzom.Tests.RayAABBCollisionTests.VoxelDecimation_WeldsDuplicateVertices
  Passed Ryzom.Tests.MortonSpatialTests.VoxelSpatialGrid_RadiusQuery_FindsNearbyEntitiesOnly

Test Run Successful.
Total tests: 14 | Passed: 14 | Failed: 0 | Execution Time: 0.81s
```

---

## 📄 License
This project is licensed under the MIT License, preserving the open-source spirit of the original Ryzom release by Winch Gate and the Free Software Foundation.
All original Ryzom 3D artistic assets are property of their respective creators under Creative Commons (CC-BY-SA).
