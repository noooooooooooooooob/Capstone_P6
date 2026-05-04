# Dimensional Machine Intro Setup Guide

This guide details the step-by-step process for configuring the **Intro Sequence** of Stage 1 inside the Unity Editor. By the end of this guide, pressing a button in VR will trigger a synchronized "wall break" event across the network, followed by a simulated battery failure that plunges Player A's room into darkness.

---

## 1. Environment & Lighting Setup
Before configuring the machine, we need the environments ready.

1. **Build the Rooms:** Create Room A and Room B side-by-side. 
2. **The Dividing Wall:** Place a specific wall GameObject between the two rooms. This is the wall that will "break" or shift dimensions.
   - *Tip:* For a simple start, this can just be a regular Cube. Later, you can replace it with a destructible prefab or a fancy shader.
3. **Room A Lighting:** 
   - Place your `Directional Light` or `Point Lights` inside Room A.
   - Create an empty GameObject named `LightingController`.
   - Attach the `RoomLightingController.cs` script to it.
   - Drag all the lights that belong to Room A into the `Room Lights` array in the Inspector.

## 2. Setting up the Dimensional Machine
This is the core object that manages the sequence state over the network.

1. **Create the Machine:** In Room A, create a 3D object (like a Cylinder or a custom sci-fi console model) and name it `DimensionalMachine_Obj`.
2. **Add Network Components:**
   - Attach a `NetworkObject` component (required for Fusion to sync it).
   - Attach the `DimensionalMachine.cs` script.
3. **Add the Interaction Button:**
   - On the machine, create a child object to act as the Start Button.
   - Add the Meta XR Interaction SDK components for a button (e.g., `Interactable Unity Event Wrapper`, `Pointable Unity Event Wrapper`, or a basic physical button script depending on your exact Meta XR setup).
   - Locate the UnityEvent (e.g., `When Select` or `On Click`) on your button component.
   - Drag the `DimensionalMachine_Obj` into the event target.
   - Select the function: `DimensionalMachine -> StartRecovery()`.

## 3. Creating the "Wall Break" Effect
When the machine starts, the wall needs to disappear so players can see each other.

1. **The Effect Logic:**
   - Select your `DimensionalMachine_Obj`.
   - In the `DimensionalMachine` script, find the `On Machine Started ()` UnityEvent.
   - **Action 1 (Hide Wall):** Drag the Dividing Wall GameObject into the event, and select `GameObject -> SetActive (false)`.
   - **Action 2 (Audio):** Add an AudioSource with an explosion or heavy mechanical sound to the machine. Add another event action to call `AudioSource -> Play()`.
   - **Action 3 (VFX):** If you have a particle system (dust/debris), add an action to call `ParticleSystem -> Play()`.

> [!NOTE]
> Because `DimensionalMachine.cs` uses `[Rpc(RpcSources.StateAuthority, RpcTargets.All)]`, this `OnMachineStarted` UnityEvent will fire automatically on **both** Player A and Player B's headsets at the exact same time!

## 4. The Blackout Event
The machine runs out of battery shortly after starting, which plunges Player A into darkness.

1. **Creating the Blackout Trigger:**
   - We need something to trigger the blackout a few seconds after the machine starts.
   - *Easy Method:* Create a simple script (or use a Unity Timeline/Animation) that waits 3-5 seconds and then calls `DimensionalMachine.TriggerBlackout()`.
2. **Hooking up the Blackout:**
   - Select your `DimensionalMachine_Obj` and locate the `On Machine Blackout ()` UnityEvent.
   - **Action 1 (Dim Lights):** Drag the `LightingController` GameObject into the event.
   - **Action 2:** Select `RoomLightingController -> DimLights()`.
   - **Action 3 (Spooky Audio):** Add a power-down sound effect and trigger it here.
   - **Action 4 (Disable Machine):** Disable any glowing emissive materials on the machine to show it's completely dead.

## 5. Transitioning the Stage State
Finally, the Stage Manager needs to know the intro is over so puzzle logic can begin.

1. In the `On Machine Blackout ()` event, add one final action.
2. Drag the `Stage1Manager` GameObject into the target.
3. Select `Stage1Manager -> TransitionToState(StageState)` and set the argument to `Illumination`. 

---

> [!TIP]
> **Testing this locally:**
> You don't need two headsets to test the initial logic! You can place your VR rig in Room A, press the button, and watch the wall disappear and the lights turn off. Once that feels good, you can test it in a networked session to ensure Player B sees the wall drop at the same time.
