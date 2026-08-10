# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

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

- Fixed runtime application of Arcscript variable changes for board-scoped variables.
- Fixed variable save/load restoration to target variables by id rather than by name.
- Fixed `resetAll` to include scoped variables and exclude variables by stable ID rather than by name.
- Fixed Unity variable deserialization so current and default values remain distinct.
