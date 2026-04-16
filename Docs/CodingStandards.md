# Coding Standards

## Strict Separation of Concerns (SoC)

To maintain codebase health and readability, adhere to the following principles:

1. **Single Responsibility Principle (SRP):**
   - Each script or class should only have one primary responsibility.
   - For example, if a class scans a room, it should not also be responsible for instantiating UI, placing game objects, or saving JSON data.

2. **File Size Limits:**
   - Avoid creating files that exceed 100-200 lines if the logic can be subdivided into discrete modules.
   - Large monolithic managers (e.g., God Objects) are heavily discouraged.
   - Instead, create small, focused MonoBehaviours that connect via events or component references.

3. **Modularity:**
   - Components should ideally function independently or gracefully degradable.
   - Rely on Coordinator/Manager classes merely for orchestrating flow, not executing the granular logic.
