# Editor regression tests

Install Unity's **Test Framework** (`com.unity.test-framework`) through Package Manager, then open **Window > General > Test Runner** and run the `ArcweavePlugin.Editor.Tests` assembly in **EditMode**.

The test assembly is editor-only and is not referenced by the plugin or included in player builds. It is enabled only when the Test Framework package is installed, so importing the plugin does not require NUnit.

The tests live outside `Assets/Arcweave` so they are not distributed with the plugin. Do not move them into the plugin folder.

For a Unity project containing the plugin, tests can also run from the command line:

```text
Unity -batchmode -nographics -projectPath <project-path> -runTests -testPlatform EditMode -assemblyNames ArcweavePlugin.Editor.Tests -testResults <results.xml> -logFile <editor.log>
```

## Media download tests

`Arcweave.Tests.MediaDownloadTests` imports from web through `StubApiServer`, a local HTTP stub of the project json and asset file endpoints of Arcweave's web API, so they need no API key or network access. `StubbedWebProjectAsset` points the project asset at the stub, which serves the project json set by the test, so the plugin's own requests and their `Authorization` headers are checked. Each test creates its project asset and downloaded media in a temporary folder under `Assets` and deletes it afterward.

They cover the API key the project and its media are downloaded with (a key passed to the import, its priority over the key saved in the asset, trimming, an empty or blank key, an import joined with another key and an import without any key), media being disabled by default, the project being published only after its media is linked, in-place updates and cleanup on reimport, failed and rate-limited downloads, read-only files, files moved during a download, keeping media when an import cannot download it, disabling media downloads, asset ids not being used in paths, unsaved project asset instances, and files that could not be deleted. They also cover how files are named:
- after their assets, directly in the media folder, without Arcweave's asset folders
- sanitized and shortened names
- assets with the same name
- files the downloader doesn't own at the name, including names that differ only in case
- renaming or moving an asset in Arcweave, which keeps the file, its name and its GUID
- extension changes
- the one-time renaming of media downloaded by 3.1.0 (named after asset ids), keeping its GUID and the references to it (through `MediaReferences`)
- files the user renamed or moved
- never overwriting a file another editor extension saves at a planned path during a rename (simulated with `MoveCallback`, an `AssetModificationProcessor` that only acts while a test sets its callback)

`Arcweave.Tests.MediaNameTests` checks how asset names are sanitized: invalid characters, control characters, trailing dots and spaces, `.` and `..`, Windows reserved names, names Unity would hide, and shortening long names to 100 characters before the extension.

## API key tests

`Arcweave.Tests.ApiKeyTests` checks where a web import finds its API key: the key in the user settings, the `ARCWEAVE_API_KEY` environment variable, and a key saved in the asset by an older version of the plugin, in that order, and the error when there is none. They also cover reading a key from a copy of the legacy fixture (serialized by an older version of the plugin) with a key added, moving a key out of the asset (to the user settings, keeping the key already in the user settings or the environment variable, a blank key, and a user settings file that can't be written, which keeps the key in the asset), checking that it is removed from the saved asset file, trimming a key edited into the settings file, and the build check that finds every project asset still containing one, including one added to another asset's file.

Every test that imports from web uses `TemporaryUserSettings`, which points the user settings at a temporary file and clears `ARCWEAVE_API_KEY` for the test, so the tests never read or change your own API key.

## Inputs tests

`Arcweave.Tests.ElementInputsTests` imports a project where an element has inputs from another element and from a branch condition, a branch has an input, and a jumper to that element has its own input. They cover the order of the inputs, that they are the same connections as their source's outputs, that a connection to a jumper is an input of the jumper and not of its target, and that inputs are linked again when a project asset is deserialized.

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
