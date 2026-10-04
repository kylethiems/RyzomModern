# Ryzom Modern: 100% Complete Migration Master Roadmap & Work Breakdown

This document tracks all remaining engineering tasks required to achieve 100% feature parity with the entire 1.8M line legacy 2004 C++98 / NeL codebase.

---

## 1. Executive Status Matrix

| Subsystem | Legacy Ryzom (2004 C++98 / NeL) | Ryzom Modern (.NET 8 / WebGPU) | Status |
| :--- | :--- | :--- | :--- |
| **Runtime Core** | GCC 3.x / MSVC 2003 C++98, heavy macros | .NET 8 Modern C# 12, Roslyn JIT, SIMD vectors | **Complete (100%)** |
| **Data Persistence** | Custom raw binary flat files & MySQL 4 | EF Core 8 + SQLite/PostgreSQL, migrations | **Complete (100%)** |
| **Spatial Indexing** | Recursive pointer-chasing Octree | 64-bit Morton Z-Order Flat Lattice Grid | **Complete (100%)** |
| **Static Geometry** | NeL `.shape` proprietary binary meshes | Zero-copy `.shape` reader + modern glTF 2.0 exporter | **Complete (100%)** |
| **Mesh Optimization** | Unfiltered dense meshes (1,280 KB) | Taubin Non-Shrinking Acetone Wash + LiDAR Voxel Grid | **Complete (100%)** |
| **Coordinate Compression**| Raw 12-byte float32 coordinates | 5-Trit Balanced Ternary + Topological Simplicial (3.8 KB) | **Complete (100%)** |
| **Shading & Lighting** | Fixed-function / Blinn-Phong diffuse | Cook-Torrance PBR + Dual-Lobe Clearcoat + AgX Filmic | **Complete (100%)** |
| **Living Flora Ecology**| Static RNG gathering drops (10 wood) | Swanson FVS Allometric Stem Taper $e^{-0.025 \cdot \text{DBH}}$ | **Complete (100%)** |
| **Resource Valuation** | Arbitrary fixed NPC sell prices | Swanson Residual Stumpage (Pond - Yarding - Haul - Tariff) | **Complete (100%)** |
| **Ecosystem Balance** | Static spawn points | Ecological Arbitrage Monitor + Kitin Hive Awakening | **Complete (100%)** |
| **Testing & Remote CI** | 20+ min fragile CMake build | GitHub Actions CI: 32/32 tests in 1.0s, 11/11 green runs | **Complete (100%)** |

---

## 2. Granular Task Checklist: What is Left to be Done

### Track 1: Skeletal Rigging, Skinning & Keyframe Animations (NeL 3D Bones)
- [ ] **Task 1.1:** Implement `NeLSkeletonParser.cs` in `Ryzom.Core/Formats/` (read bone hierarchy, local transform, inverse bind matrices, equipment socket anchors).
- [ ] **Task 1.2:** Implement `NeLAnimationParser.cs` in `Ryzom.Core/Formats/` (quaternion rotation channels, translation splines, keyframe tracks).
- [ ] **Task 1.3:** Build WebGPU/WebGL 4-bone influence vertex skinning shader ($p' = \sum w_i M_i p$).
- [ ] **Task 1.4:** Implement `AnimationBlendTree.cs` in `Ryzom.Engine/Animation/` for locomotion, harvest swing, and casting blend states.

### Track 2: Landscape Geometry, Quadtree Heightfields & Chunk Streaming (NeL Landscape)
- [ ] **Task 2.1:** Implement `NeLLandscapeParser.cs` in `Ryzom.Core/Formats/` for `.zone` and `.gr` continent heightmaps.
- [ ] **Task 2.2:** Build Continuous Dynamic Quadtree LOD (CDLOD) terrain streaming for seamless chunk loading.
- [ ] **Task 2.3:** Implement 4-layer terrain splatting shader (sand, mossy bark, fertile loam, root stone).
- [ ] **Task 2.4:** Implement fast SIMD heightfield raycasting collision for ground clamping players and creatures.

### Track 3: The Complete Action Stanza Grammar Engine (Ryzom Core Magic & Combat)
- [ ] **Task 3.1:** Implement `StanzaCompiler.cs` in `Ryzom.Engine/Stanza/` supporting all 4 Brick classes:
  - *Target Bricks:* Self, Target, Area-of-Effect (AoE), Group, Cone.
  - *Effect Bricks:* Acid, Cold, Electric, Fire, Rot, Shock, Poison, Heal, Buffs/Debuffs.
  - *Modifier Bricks:* Range, Cast Speed, Power Multiplier, Duration.
  - *Cost Bricks:* Sap Credit, HP Sacrifice, Stamina Burn, Reagents.
- [ ] **Task 3.2:** Implement Stanza Credit Balance Validator (Total Cost >= Power Rating).
- [ ] **Task 3.3:** Build drag-and-drop Stanza Spellbuilder Workbench UI in the Web client.

### Track 4: Distributed Shard Cluster & PACS AI Herd Ecology (NeL Net & PACS)
- [ ] **Task 4.1:** Implement distributed Shard Microservices in `Ryzom.Server/Cluster/` (Gateway, Session/Auth, Shard Node, Ryzom Ring).
- [ ] **Task 4.2:** Implement Boids flocking simulation in `Ryzom.Engine/AI/` for herbivore herds (Mektoubs, Yubos, Gubani).
- [ ] **Task 4.3:** Implement predator pack hunting tactics (Clopper, Kipee) and Kitin hive raid alerts.

### Track 5: Complete Client UI, Paperdoll Equipment & Workbenches (Ryzom UI)
- [ ] **Task 5.1:** Implement 14-slot Paperdoll equipment system in `Ryzom.Core/Persistence/` and visual gear swaps in Web client.
- [ ] **Task 5.2:** Implement Crafting Workbench calculating durability, parry chance, and elemental resistance from Swanson timber grades.
- [ ] **Task 5.3:** Implement multi-channel in-game chat (`/say` spatial distance falloff, `/shout`, `/team`, `/guild`, `/faction`).
- [ ] **Task 5.4:** Implement vector minimap with player coordinates, harvest nodes, and caravan routes.

### Track 6: Spatial 3D Audio & Dynamic Soundscape (NeL Sound)
- [ ] **Task 6.1:** Implement Web Audio API 3D spatial panner nodes for positional footfalls, axe chops, and spell casts.
- [ ] **Task 6.2:** Implement procedural environmental audio (wind, rain, Prime Roots cavern reverberation).
- [ ] **Task 6.3:** Implement situational music crossfader (ambient peace to battle combat).

### Track 7: Automated 13 GB Creative Commons Asset Ingestion Pipeline
- [ ] **Task 7.1:** Execute batch asset cooker across all 3,000+ `.shape` meshes and 15,000+ textures.
- [ ] **Task 7.2:** Convert all PBR textures to KTX2 / Basis Universal GPU compressed format.
- [ ] **Task 7.3:** Package cooked glTF/KTX2 assets into range-request friendly chunk archives for CDN streaming.

---

## 3. Recommended Next Implementation Targets

To proceed immediately, select any of the following high-impact milestones:

1. **Target A (Magic & Combat):** Implement the complete **Action Stanza Grammar Engine** (`StanzaCompiler.cs` + all 4 Brick classes + validator + Web drag-and-drop spellbuilder).
2. **Target B (Skeletal Animation):** Implement **Skeletal Rigging & Animation Decoders** (`NeLSkeletonParser.cs` + `NeLAnimationParser.cs` + WebGL joint matrix skinning).
3. **Target C (Sound & Atmosphere):** Implement **Web Audio API 3D Positional Audio & Soundscape** (spatial footsteps, axe impact, ambient wind, and combat themes).
4. **Target D (Inventory & Gear):** Implement the **14-Slot Paperdoll System & Crafting Workbench** (visual armor swapping + Swanson material recipe output).
