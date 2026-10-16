# Editor regression tests

Install Unity's **Test Framework** (`com.unity.test-framework`) through Package Manager, then open **Window > General > Test Runner** and run the `ArcweavePlugin.Editor.Tests` assembly in **EditMode**.

The test assembly is editor-only and is not referenced by the plugin or included in player builds. It is enabled only when the Test Framework package is installed, so importing the plugin does not require NUnit.

The tests live outside `Assets/Arcweave` so they are not distributed with the plugin. Do not move them into the plugin folder.

For a Unity project containing the plugin, tests can also run from the command line:

```text
Unity -batchmode -nographics -projectPath <project-path> -runTests -testPlatform EditMode -assemblyNames ArcweavePlugin.Editor.Tests -testResults <results.xml> -logFile <editor.log>
```

## Media download tests

`Arcweave.Tests.MediaDownloadTests` imports from web through `StubApiServer`, a local HTTP stub of the asset file endpoint of Arcweave's web API, so they need no API key or network access. `StubbedWebProjectAsset` points the project asset at the stub and returns the project json from the test. Each test creates its project asset and downloaded media in a temporary folder under `Assets` and deletes it afterward.

They cover media being disabled by default, the project being published only after its media is linked, in-place updates and cleanup on reimport, failed and rate-limited downloads, read-only files, files moved during a download, keeping media when an import cannot download it, disabling media downloads, asset ids not being used in paths, unsaved project asset instances, and files that could not be deleted. They also cover the readable paths: folders that mirror the Arcweave asset folders, sanitized names, assets with the same name, renaming and moving an asset in Arcweave (keeping the file's GUID), extension changes, moving media downloaded by 3.1.0 (named after asset ids) to readable paths while keeping its GUID and the references to it (through `MediaReferences`), files the downloader doesn't own at the desired path, files the user moved inside or out of the media folder, deleting empty folders while keeping folders with other files, folders and files with the same name, names that differ only in case, and never overwriting a file another editor extension saves at a planned path during a move (simulated with `MoveCallback`, an `AssetModificationProcessor` that only acts while a test sets its callback).

`Arcweave.Tests.MediaNameTests` checks how asset and folder names are sanitized: invalid characters, control characters, trailing dots and spaces, `.` and `..`, Windows reserved names, and names Unity ignores.

## Omitted lists tests

`Arcweave.Tests.OmittedListsTests` imports a project whose empty lists are omitted, as Arcweave exports them (for example, an ending element has no `outputs`), or `null`. They cover element outputs, components and attributes; board jumpers, notes and attributes; component attributes; component-list attribute data; and branch else-if and else conditions, through `ProjectMaker` and through `ImportProject` from json.

## Audio numbers tests

`Arcweave.Tests.AudioNumbersTests` imports a project whose starting element plays one audio asset. Arcweave exports may write the audio `delay` as a string (for example, `"delay": "1200"` in The Castle template). They cover delays and volumes written as integers (including zero, negative and large ones), decimals, numeric strings read regardless of the current culture, `null` or omitted, and values that are not numbers, which are reported and replaced by the default (a delay of `0` and a volume of `1`) instead of failing the import.

## Legacy fixture

`Editor/Fixtures/ProjectBeforeGlobalVariables.asset.txt` was serialized by Unity 6000.5.7f1 with the plugin at commit `97582c542cdc8ce25257f2a2f79932186e7b8d0a`, before `Project.Variables` was renamed. It contains:

- Global `coins = 7`, ID `coins-id`.
- Board `intro` with `health = 11`, ID `board-health`.
- Component `hero` with `health = 13`, ID `hero-health`.

These are synthetic values. The fixture has no API credentials or external asset dependencies. Keep it as text: each test copies it to a temporary `.asset`, lets Unity deserialize it, and deletes the temporary asset afterward. This prevents Unity from silently migrating the checked-in fixture and hiding upgrade regressions.

The tests cover preservation of variables and scopes, interpreter evaluation, save/load and reset, resaving upgraded assets, both public property names, and an implementation of the original `IProject` interface. To regenerate the fixture, serialize an `ArcweaveProjectAsset` containing the values and IDs above using the old commit; do not generate it with the current property name.
