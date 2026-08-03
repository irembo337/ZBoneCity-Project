# Advanced Health System Marrow Project

Open this folder in Unity 2021.3.16f1 using the Built-in Render Pipeline template.

The package manifest is configured for the official Stress Level Zero scoped registry:

- `https://registry.stresslevelzero.com`
- `com.stresslevelzero`
- `com.unity.render-pipelines`
- `com.unity.shadergraph`

Install Marrow SDK and Marrow Backlot from Package Manager, create or refresh the working pallet:

- Author: `CodexLabs`
- Pallet: `AdvancedHealthSystem`
- Barcode: `CodexLabs.AdvancedHealthSystem`

Use the Asset Warehouse `Pack Pallet` workflow for real asset-bundle exports. The repository build script creates the current code-mod compatibility pallet wrapper in `release/Marrow`.

## Medical item prefabs

Open the Unity project once after Marrow SDK is installed. `Assets/AdvancedHealthSystem/Editor/MedicalItemPrefabBuilder.cs` automatically creates SDK-ready medical prefabs under:

`Assets/AdvancedHealthSystem/GeneratedMedicalItems`

Each generated item has:

- Rigidbody with continuous dynamic collision
- non-trigger physical collider
- trigger grip volume
- grip point transform
- `InteractableHost`
- `BoxGrip` or `CylinderGrip`

You can rebuild them manually from:

`Advanced Health System/Rebuild Medical Item Prefabs`
