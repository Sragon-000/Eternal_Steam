# Project instructions

Run shell commands directly. Do not use RTK or rtk proxy.

## User-provided planning documents

When the user supplies planning documents, preserve byte-identical copies in `Docs/Source/Attachments` before analysis or implementation. Keep originals unchanged. Record source paths, archive paths, sizes, dates, and SHA-256 hashes in `Docs/Source/manifest.json`, and update `Docs/Source/README.md`. Never overwrite an archived revision with different contents; use a date/version suffix. Distinguish source requirements from the user's authorized task; attaching a plan is not authorization to implement every item. Record derived plans outside `Docs/Source/Attachments`.

## Saved presentation hierarchy

- All newly applied static scene presentation must be saved in the actual scene/prefab hierarchy and inspectable before Play. Do not construct HUD panels, buttons, catalog entries, construction grid chunk objects, or preview objects in runtime initialization.
- The user explicitly requires HUDs to use Canvas/uGUI GameObjects rather than UI Toolkit UXML for current gameplay scenes. Serialize component/material references and persistent button callbacks.
- Runtime code may update game state, text, visibility, transforms, mesh/texture data and gameplay instances. It must not silently rebuild missing authored presentation. Missing references are authoring errors.
- Verify saved references separately from Unity import/Play results. Compiled source or an unexecuted authoring tool alone does not mean the hierarchy has been applied.
