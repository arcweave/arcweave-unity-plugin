# Editor regression tests

Install Unity's **Test Framework** (`com.unity.test-framework`) through Package Manager, then open **Window > General > Test Runner** and run `Arcweave.Tests.GlobalVariablesUpgradeTests` in **EditMode**.

The test assembly is editor-only and is not referenced by the plugin or included in player builds. It is enabled only when the Test Framework package is installed, so importing the plugin does not require NUnit.

The tests live outside `Assets/Arcweave` so they are not distributed with the plugin. Do not move them into the plugin folder.

For a Unity project containing the plugin, tests can also run from the command line:

```text
Unity -batchmode -nographics -projectPath <project-path> -runTests -testPlatform EditMode -testFilter Arcweave.Tests.GlobalVariablesUpgradeTests -testResults <results.xml> -logFile <editor.log>
```

## Legacy fixture

`Editor/Fixtures/ProjectBeforeGlobalVariables.asset.txt` was serialized by Unity 6000.5.7f1 with the plugin at commit `97582c542cdc8ce25257f2a2f79932186e7b8d0a`, before `Project.Variables` was renamed. It contains:

- Global `coins = 7`, ID `coins-id`.
- Board `intro` with `health = 11`, ID `board-health`.
- Component `hero` with `health = 13`, ID `hero-health`.

These are synthetic values. The fixture has no API credentials or external asset dependencies. Keep it as text: each test copies it to a temporary `.asset`, lets Unity deserialize it, and deletes the temporary asset afterward. This prevents Unity from silently migrating the checked-in fixture and hiding upgrade regressions.

The tests cover preservation of variables and scopes, interpreter evaluation, save/load and reset, resaving upgraded assets, both public property names, and an implementation of the original `IProject` interface. To regenerate the fixture, serialize an `ArcweaveProjectAsset` containing the values and IDs above using the old commit; do not generate it with the current property name.
