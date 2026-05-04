# Stage 1: Implementation Plan

This document outlines the technical architecture and the required C# scripts to implement the Stage 1 asymmetric co-op puzzles. Based on the project structure and technical stack decisions, we will use **Photon Fusion** for multiplayer synchronization and **Meta XR Interaction SDK** for physical interactions.

## Architecture Overview
The stage relies on a central `Stage1Manager` to track the overall puzzle progression. Individual puzzle elements will be implemented as `NetworkBehaviour` scripts to ensure state is synchronized across the network between Player A and Player B.

## Required Scripts (Saved in Assets/Scripts/Stage1/)

### 1. Core Management
- **`Stage1Manager.cs`**
  - **Responsibility:** Manages the overall stage state machine (Intro -> Illumination -> Battery -> Pipe -> Password -> Outro). Synchronizes the current puzzle state across clients.
- **`PlayerRoleManager.cs`**
  - **Responsibility:** Assigns and tracks which player is Player A (Dark room/Machine side) and Player B (Diary/Light side).

### 2. Intro Sequence
- **`DimensionalMachine.cs`**
  - **Responsibility:** Handles the initial button press, triggers the wall-breaking sequence, and manages the blackout event for Player A.
- **`RoomLightingController.cs`**
  - **Responsibility:** Controls the directional and ambient lights in Player A's room. Listens to events from the `DimensionalMachine` to trigger the blackout.

### 3. Illumination Puzzle
- **`FireSphere.cs`** (Networked Grab Interactable)
  - **Responsibility:** The portable light source. Synchronizes position and rotation over the network so Player A can see where Player B moves it.
- **`LightReceptacle.cs`**
  - **Responsibility:** An interaction socket where Player B can place the `FireSphere` to effectively cast light into A's room.

### 4. Battery Thawing Puzzle
- **`FrozenBattery.cs`** (Networked Grab Interactable)
  - **Responsibility:** Represents the battery Player A finds. Contains a networked boolean state (e.g., `IsThawed`).
- **`ThawingDevice.cs`**
  - **Responsibility:** Contains two interaction sockets (one for A's frozen battery, one for B's fire sphere). Checks if both are slotted, waits for a duration, and changes the battery's state to thawed.
- **`MainPowerSocket.cs`**
  - **Responsibility:** A socket that accepts the thawed battery. Once inserted, it triggers the power restoration event and moves the stage to the Pipe puzzle.

### 5. Pipe Repair Puzzle
- **`PipeSystemManager.cs`**
  - **Responsibility:** Orchestrates the pipe burst event. Tracks overall system pressure and the status of the repair.
- **`PressureValve.cs`** (Networked Interactable)
  - **Responsibility:** Player A turns this valve. Modifies a networked `CurrentPressure` variable in the `PipeSystemManager`.
- **`PipeRepairNode.cs`** (Networked Interactable)
  - **Responsibility:** Player B interacts with this to physically fix the pipe. It only allows a successful repair if `CurrentPressure` is below a safe threshold.

### 6. Password Puzzle
- **`LiquidBottle.cs`** (Grab Interactable)
  - **Responsibility:** Detects rapid movement (shaking via velocity/angular velocity). If marked as `IsFull`, plays a sloshing audio clip and triggers controller haptics.
- **`PasswordKeypad.cs`**
  - **Responsibility:** Handles Player A's input. Validates the inputted code against the target code (Date + Full Bottles) and triggers the Outro sequence upon success.
- **`ResearcherDiary.cs`** (Grab Interactable)
  - **Responsibility:** Object for Player B that displays the UI/Text hint for the password.
