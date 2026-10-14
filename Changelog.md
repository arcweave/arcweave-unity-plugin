# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

### Added

- Added `ArcweaveProjectAsset.ImportProjectAsync()`, `EnsureImportedAsync()`, `IsImported` and `IsImporting`. Concurrent imports of the same asset share a single download and parse.
- Added `ArcweavePlayer.PlayProjectAsync()`. Overlapping calls, including calls made from the player's own event handlers or import callbacks, share one pending start, so the project only starts once.
- Added optional media downloads for web imports in the Unity Editor. With the new **Download Media** inspector toggle enabled, importing from web downloads the cover images and audio the project uses through Arcweave's web API into a `<Project Asset Name> Media` folder and links them to the project, so they don't need to be copied into a `Resources` folder. The toggle is disabled by default, including for existing project assets, so projects only rely on media downloaded from Arcweave when you enable it.
- Added `Cover.assetId`, `Cover.downloadedImage`, and `AudioAsset.downloadedClip`.

### Changed

- `ArcweavePlayer.PlayProject()` now imports the project first when the asset has not been imported yet.
- `Cover.ResolveImage()` and `AudioAsset.TryGetAudioClip()` return the downloaded media when available and fall back to `Resources.Load<T>()` otherwise. With **Download Media** enabled, downloaded media takes priority over files with the same name in a `Resources` folder.
- Renamed the project-level variable list to `Project.GlobalVariables` to distinguish it from board and component variables. Use `GetAllVariables()` to enumerate every scope.
- Preserved global variables in previously imported Unity assets when migrating the serialized property name.

### Deprecated

- Deprecated `Project.Variables` in favor of `Project.GlobalVariables`, retaining it as an alias to the same list. The `IProject.Variables` contract remains compatible with existing callers and implementations.

### Fixed

- Fixed a `NullReferenceException` when importing projects whose elements, boards, components, or branches omit empty lists (for example, an ending element with no `outputs` or an element without `components`). Missing or `null` lists are now treated as empty.
- Fixed `ImportProject` never calling `onError` when the import source is not configured or the json fails to parse, which could leave the inspector stuck on "Importing Project...". The inspector now also refreshes as soon as an import finishes.

## 3.0.0

### Added

- Added attribute-backed board and component variable support through container `CustomId` scopes and the shared `IHasVariables` interface.
- Added boolean, integer, float, and plain-string attribute value support.
- Added variable identity metadata via `Variable.Id` and `Variable.Parent` so runtime updates and saved state can resolve variables unambiguously.
- Added `Project.SetVariable(string name, string scope, object value)` for setting board- or component-scoped variables from code.
- Added `ArcweavePlugin.asmdef` to give the plugin its own Unity assembly definition.

### Changed

- Updated `Project.GetVariable` to accept an optional board or component scope.
- Updated variable reset and save/load flows to include global, board-scoped, and component-scoped variables.
- Updated Arcscript parsing and runtime resolution to support scoped variable references in the form `containerCustomId.variableName`.
- Updated the JSON importer to create scoped variables from eligible board and component attributes while retaining legacy board-variable imports.
- Updated internal Arcscript state mutation to resolve variables by Arcweave variable id instead of by name.
- Updated imported float variables to use `double` consistently across parsing, evaluation, and state restoration.

### Deprecated

- Deprecated legacy `Board` constructors that do not accept a `customId`. Use the overloads that include `customId` so board-scoped variables can resolve correctly.
- Deprecated legacy `Variable` constructors that do not accept an explicit Arcweave variable id. Use the id-aware overloads so runtime state and save/load remain stable.

### Fixed

- Fixed interpreter results so Arcscript conditions are identified correctly.
- Fixed the project viewer to use Unity's `EntityId` APIs on Unity 6000.5 and newer while retaining compatibility with earlier editor versions.
- Fixed runtime application of Arcscript variable changes for board-scoped variables.
- Fixed variable save/load restoration to target variables by id rather than by name.
- Fixed `resetAll` to include scoped variables and exclude variables by stable ID rather than by name.
- Fixed Unity variable deserialization so current and default values remain distinct.
