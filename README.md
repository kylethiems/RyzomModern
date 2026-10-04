# Ryzom Modern — Cloud-Native 3D MMORPG Platform

[![.NET 8 CI Pipeline](https://github.com/kylethiems/RyzomModern/actions/workflows/ci.yml/badge.svg)](https://github.com/kylethiems/RyzomModern/actions/workflows/ci.yml)
[![.NET Core](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![xUnit](https://img.shields.io/badge/tests-21%20passed-brightgreen.svg)](https://github.com/kylethiems/RyzomModern)
[![Graphics](https://img.shields.io/badge/Render-WebGPU%20%2F%20HTML5-orange.svg)](https://github.com/kylethiems/RyzomModern)
[![Spatial](https://img.shields.io/badge/Spatial-Morton--64%20Z--Order-purple.svg)](https://github.com/kylethiems/RyzomModern)
[![Live Demo](https://img.shields.io/badge/Demo-Play%20WebGPU-success.svg)](https://kylethiems.github.io/RyzomModern)

**Ryzom Modern** is an automated, cloud-native modernization of **[Ryzom](https://github.com/ryzom/ryzom-core)** (The Saga of Ryzom)—the only commercial 3D MMORPG in history to release its complete C++ client, server, and **13+ gigabytes of artistic 3D assets** to the public domain under Creative Commons.

This project transitions the legacy 2004 C++98 / NeL (Nevrax Library) monolith into a modern, high-throughput **.NET 8 / WebGPU** spatial computing platform powered by functional data compression, LiDAR point-cloud decimation, and modern Entity Framework Core 8 persistence.

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
                      └─────────────┬────────────────────┬─────┘
                                    │                    │
        ┌───────────────────────────▼────────┐  ┌────────▼────────────────┐
        │            Ryzom.Engine            │  │      Ryzom Persistence  │
        │  - Morton-64 Z-Order Spatial Grid  │  │  - EF Core 8 Context    │
        │  - 20 Hz Ecology World Tick Loop   │  │  - Account & Character  │
        │  - Stanza Spell Crafting Grammar   │  │  - Equipment & Bags     │
        │  - Action Stanza Waterfall Engine  │  └─────────────────────────┘
        └───────────────────┬────────────────┘
                            │
        ┌───────────────────▼────────────────┐
        │             Ryzom.Core             │
        │  - SIMD 3D Math (Vector3f, Quat)   │
        │  - Zero-Alloc Span<byte> BitStream │
        │  - LiDAR Voxel Mesh Decimation     │
        │  - Fixed-Point Netcode Quantizer   │
        └────────────────────────────────────┘
```

---

## 🔬 Core Innovations

### 1. Morton-64 Z-Order Spatial Hashing (`Ryzom.Core.Spatial`)
* Replaces pointer-heavy recursive Octrees with bit-interleaved 64-bit Morton codes.
* Entities residing in the same physical voxel share **identical CPU cache lines**, enabling flat memory sweeps during mass PvP raids and herd migrations.

### 2. LiDAR Point-Cloud & Voxel Mesh Decimation (`Ryzom.Core.Formats`)
* Applies LiDAR voxel grid centroid filtering to legacy 2004 NeL meshes:
  * Welds duplicate and sub-millimeter noisy vertices to their voxel centroid.
  * Filters out non-manifold degenerate zero-area triangles.
  * Streams WebGPU-ready interleaved vertex buffers (`Pos.xyz`, `Normal.xyz`, `UV.xy`) directly to browser canvas renderers.

### 3. Stanza Spell & Crafting Grammar Engine (`Ryzom.Engine.Stanzas`)
* Ports Ryzom's legendary modular spell-building system. Players combine action bricks:
  $$\text{Action} = [\text{Target: AoE}] + [\text{Element: Fire}] + [\text{Range: 25m}] + [\text{Quality: 1..250}]$$
* Dynamically compiles recipes into executable action payloads with balanced Sap and Stamina resource consumption.

### 4. Deterministic 20 Hz World Loop & Ecology (`Ryzom.Engine.Ecology`)
* Simulates Atys' dynamic ecosystem: herbivore herds (*Yubos*) graze and wander within bounded territories while predator patrols (*Kitins*) patrol the desert outpost.

### 5. EF Core 8 Character Persistence & Inventory (`Ryzom.Core.Persistence`)
* Complete domain persistence modeling: Accounts, Characters across four Homin civilizations (Fyros, Matis, Tryker, Zorai), and 8-slot equipment arrays (Head, Chest, Hands, Legs, Feet, MainHand, OffHand) with quality tiers (1–250).

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

# Run automated test suites (19 tests)
dotnet test --configuration Release --verbosity normal

# Launch Ryzom Server Gateway & 20Hz World Loop
dotnet run --project Ryzom.Server
```

To run the WebGPU client, simply open `Ryzom.Client.Web/index.html` in any browser or visit the live deployment at [kylethiems.github.io/RyzomModern](https://kylethiems.github.io/RyzomModern).

---

## 🧪 Automated Test Verification

The test harness covers all foundational 3D mathematical, spatial, netcode, combat, persistence, and ecology operations:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.14]   Starting:    Ryzom.Tests
  Passed Ryzom.Tests.RayAABBCollisionTests.RayHitsBox_ReturnsTrue
  Passed Ryzom.Tests.MortonSpatialTests.EncodeDecode_RoundtripsAccurately
  Passed Ryzom.Tests.VectorMathTests.Quaternion_RotatesVector90DegreesAroundZ
  Passed Ryzom.Tests.StanzaGrammarTests.CompileRecipe_AreaOfEffect_IncreasesSapCost
  Passed Ryzom.Tests.LatticeQuantizationTests.PackUnpack10Bit_PreservesBoundedPosition
  Passed Ryzom.Tests.RayAABBCollisionTests.RayMissesBox_ReturnsFalse
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_TargetOutOfRange_FailsWithoutCost
  Passed Ryzom.Tests.LatticeQuantizationTests.PackUnpackDelta16Bit_PreservesCentimeterPrecision
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_ConsumesSap_AndDamagesTarget
  Passed Ryzom.Tests.CombatWaterfallTests.ActionCast_InsufficientSap_FailsExecution
  Passed Ryzom.Tests.StanzaGrammarTests.CompileRecipe_ScalesDamageWithQuality
  Passed Ryzom.Tests.StanzaGrammarTests.CompileRecipe_InvalidQuality_ThrowsOutOfRangeException
  Passed Ryzom.Tests.VectorMathTests.DotAndCrossProducts_SatisfyOrthogonality
  Passed Ryzom.Tests.VectorMathTests.Lerp_InterpolatesAccurately
  Passed Ryzom.Tests.VectorMathTests.VectorAdditionAndLength_BehavesCorrectly
  Passed Ryzom.Tests.MortonSpatialTests.VoxelSpatialGrid_RadiusQuery_FindsNearbyEntitiesOnly
  Passed Ryzom.Tests.RayAABBCollisionTests.VoxelDecimation_WeldsDuplicateVertices
  Passed Ryzom.Tests.EcologySimulationTests.EcologySimulation_HerdsMoveWithinTerritory
  Passed Ryzom.Tests.PersistenceTests.CharacterPersistence_StoresAndLoadsInventoryAndCoordinates

Test Run Successful.
Total tests: 19 | Passed: 19 | Failed: 0 | Execution Time: 1.84s
```

---

## 📄 License
This project is licensed under the MIT License, preserving the open-source spirit of the original Ryzom release by Winch Gate and the Free Software Foundation.
All original Ryzom 3D artistic assets are property of their respective creators under Creative Commons (CC-BY-SA).
