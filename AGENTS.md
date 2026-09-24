# Project instructions

Run shell commands directly. Do not use RTK or rtk proxy.

## User-provided planning documents

When the user supplies planning documents, preserve byte-identical copies in `Docs/Source/Attachments` before analysis or implementation. Keep originals unchanged. Record source paths, archive paths, sizes, dates, and SHA-256 hashes in `Docs/Source/manifest.json`, and update `Docs/Source/README.md`. Never overwrite an archived revision with different contents; use a date/version suffix. Distinguish source requirements from the user's authorized task; attaching a plan is not authorization to implement every item. Record derived plans outside `Docs/Source/Attachments`.
