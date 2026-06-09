# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

### Added

- Added board-scoped variable support through `Board.Variables`, board `CustomId` scopes, and the shared `IHasVariables` interface.
- Added variable identity metadata via `Variable.Id` and `Variable.Parent` so runtime updates and saved state can resolve variables unambiguously.
- Added `Project.SetVariable(string name, object value, string scope)` for setting board-scoped variables from code.
- Added `ArcweavePlugin.asmdef` to give the plugin its own Unity assembly definition.

### Changed

- Updated `Project.GetVariable` to accept an optional board scope and documented the difference between global/project variables and board variables.
- Updated variable reset and save/load flows to include both global and board-scoped variables.
- Updated Arcscript parsing and runtime resolution to support scoped variable references in the form `boardCustomId.variableName`.
- Updated internal Arcscript state mutation to resolve variables by Arcweave variable id instead of by name.
- Updated imported float variables to use `double` consistently across parsing, evaluation, and state restoration.

### Deprecated

- Deprecated legacy `Board` constructors that do not accept a `customId`. Use the overloads that include `customId` so board-scoped variables can resolve correctly.
- Deprecated legacy `Variable` constructors that do not accept an explicit Arcweave variable id. Use the id-aware overloads so runtime state and save/load remain stable.

### Fixed

- Fixed runtime application of Arcscript variable changes for board-scoped variables.
- Fixed variable save/load restoration to target variables by id rather than by name.
