using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>Checks where a web import finds its API key, and moving a key saved in the asset by older versions of the plugin to the user settings.</summary>
    public class ApiKeyTests
    {
        private const double TimeoutSeconds = 30;
        // Fixtures/ProjectBeforeGlobalVariables.asset.txt, serialized by an older version of the plugin
        private const string LegacyFixtureGuid = "4aa480916f3d46ad8c34694108227c26";
        private const string ProjectJson = @"{
            ""name"": ""Api Key Test"",
            ""startingElement"": ""start"",
            ""boards"": { ""main"": { ""name"": ""Main"", ""elements"": [""start""], ""branches"": [], ""jumpers"": [], ""notes"": [] } },
            ""elements"": { ""start"": { ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"", ""outputs"": [], ""components"": [], ""assets"": {} } },
            ""branches"": {}, ""jumpers"": {}, ""conditions"": {}, ""connections"": {},
            ""components"": {}, ""attributes"": {}, ""variables"": {}, ""notes"": {}, ""assets"": {}
        }";

        private TemporaryUserSettings userSettings;
        private string temporaryFolder;
        private string assetPath;
        private StubbedWebProjectAsset asset;

        [SetUp]
        public void CreateProjectAsset()
        {
            userSettings = new TemporaryUserSettings();

            var folderName = "ArcweaveApiKeyTest_" + Guid.NewGuid().ToString("N");
            temporaryFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);

            asset = ScriptableObject.CreateInstance<StubbedWebProjectAsset>();
            asset.importSource = ArcweaveProjectAsset.ImportSource.FromWeb;
            asset.projectHash = "test-hash";
            asset.projectJson = ProjectJson;
            assetPath = temporaryFolder + "/Project.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        [TearDown]
        public void DeleteProjectAsset()
        {
            userSettings.Dispose();
            AssetDatabase.DeleteAsset(temporaryFolder);
        }

        [Test]
        public void ApiKeyIsSavedInTheUserSettingsFile()
        {
            ArcweaveUserSettings.ApiKey = "  settings-key  ";

            Assert.That(ArcweaveUserSettings.ApiKey, Is.EqualTo("settings-key"));
            Assert.That(File.ReadAllText(userSettings.FilePath), Does.Contain("settings-key"));
            Assert.That(ArcweaveUserSettings.filePath, Does.Not.StartWith("Assets"));
        }

        [Test]
        public void KeyInAnEditedUserSettingsFileIsTrimmed()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(userSettings.FilePath));
            File.WriteAllText(userSettings.FilePath, "{ \"apiKey\": \"settings-key\\n\" }");

            Assert.That(ArcweaveUserSettings.ApiKey, Is.EqualTo("settings-key"));
        }

        [Test]
        public void KeyThatCannotBeSavedIsNotReportedAsSaved()
        {
            // A folder where the file should be makes writing it fail
            Directory.CreateDirectory(userSettings.FilePath);

            Assert.That(() => ArcweaveUserSettings.ApiKey = "settings-key", Throws.Exception);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.Empty);
        }

        [Test]
        public void DefaultUserSettingsFileIsInTheUserSettingsFolder()
        {
            userSettings.Dispose();
            try {
                Assert.That(ArcweaveUserSettings.filePath, Is.EqualTo("UserSettings/ArcweaveSettings.json"));
            }
            finally {
                userSettings = new TemporaryUserSettings();
            }
        }

        [UnityTest]
        public IEnumerator ImportUsesTheKeyInTheUserSettings()
        {
            ArcweaveUserSettings.ApiKey = "settings-key";

            yield return Await(asset.ImportProjectAsync());

            Assert.That(asset.IsImported, Is.True);
            Assert.That(asset.fetchedWithApiKey, Is.EqualTo("settings-key"));
            Assert.That(LegacyKey(), Is.Null.Or.Empty);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Assert.That(File.ReadAllText(assetPath), Does.Not.Contain("settings-key"));
        }

        [UnityTest]
        public IEnumerator ImportUsesTheEnvironmentVariableWhenTheUserSettingsHaveNoKey()
        {
            userSettings.SetEnvironmentApiKey("environment-key");

            yield return Await(asset.ImportProjectAsync());

            Assert.That(asset.fetchedWithApiKey, Is.EqualTo("environment-key"));
        }

        [UnityTest]
        public IEnumerator KeyInTheUserSettingsTakesPriorityOverTheEnvironmentVariable()
        {
            ArcweaveUserSettings.ApiKey = "settings-key";
            userSettings.SetEnvironmentApiKey("environment-key");

            yield return Await(asset.ImportProjectAsync());

            Assert.That(asset.fetchedWithApiKey, Is.EqualTo("settings-key"));
        }

        [UnityTest]
        public IEnumerator KeySavedInTheAssetStillImportsWithAWarning()
        {
            SaveLegacyKey("legacy-key");
            LogAssert.Expect(LogType.Warning, new Regex("imports with an API key saved in the asset"));

            yield return Await(asset.ImportProjectAsync());

            Assert.That(asset.fetchedWithApiKey, Is.EqualTo("legacy-key"));
        }

        [UnityTest]
        public IEnumerator KeyInTheUserSettingsTakesPriorityOverTheKeySavedInTheAsset()
        {
            SaveLegacyKey("legacy-key");
            userSettings.SetEnvironmentApiKey("environment-key");
            ArcweaveUserSettings.ApiKey = "settings-key";

            yield return Await(asset.ImportProjectAsync());

            Assert.That(asset.fetchedWithApiKey, Is.EqualTo("settings-key"));
        }

        [Test]
        public void ImportWithoutAnyKeyExplainsWhereToPutIt()
        {
            var import = asset.ImportProjectAsync();

            Assert.That(import.IsFaulted, Is.True);
            Assert.That(import.Exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(import.Exception.InnerException.Message, Does.Contain(ArcweaveUserSettings.API_KEY_ENVIRONMENT_VARIABLE));
            Assert.That(asset.fetchedWithApiKey, Is.Null);
        }

        [Test]
        public void MovingTheKeyOutOfTheAssetSavesItInTheUserSettingsAndRemovesItFromTheAssetFile()
        {
            SaveLegacyKey("legacy-key");
            Assume.That(File.ReadAllText(assetPath), Does.Contain("legacy-key"), "The asset must be saved as text for this test to be meaningful.");

            var moved = ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset);

            Assert.That(moved, Is.True);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.EqualTo("legacy-key"));
            Assert.That(LegacyKey(), Is.Empty);
            Assert.That(File.ReadAllText(assetPath), Does.Not.Contain("legacy-key"));
        }

        [Test]
        public void MovingTheKeyOutOfTheAssetKeepsTheKeyAlreadyInTheUserSettings()
        {
            ArcweaveUserSettings.ApiKey = "settings-key";
            SaveLegacyKey("legacy-key");

            var moved = ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset);

            Assert.That(moved, Is.False);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.EqualTo("settings-key"));
            Assert.That(LegacyKey(), Is.Empty);
            Assert.That(File.ReadAllText(assetPath), Does.Not.Contain("legacy-key"));
        }

        [Test]
        public void MovingTheKeyOutOfTheAssetKeepsTheKeyInTheEnvironmentVariable()
        {
            userSettings.SetEnvironmentApiKey("environment-key");
            SaveLegacyKey("legacy-key");

            var moved = ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset);

            Assert.That(moved, Is.False);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.Empty, "The asset's key must not take priority over the environment variable.");
            Assert.That(ArcweaveUserSettings.ResolveApiKey(), Is.EqualTo("environment-key"));
            Assert.That(LegacyKey(), Is.Empty);
        }

        [Test]
        public void MovingTheKeyKeepsItInTheAssetWhenTheUserSettingsCannotBeSaved()
        {
            SaveLegacyKey("legacy-key");
            Directory.CreateDirectory(userSettings.FilePath);

            Assert.That(() => ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset), Throws.Exception);

            Assert.That(LegacyKey(), Is.EqualTo("legacy-key"));
            Assert.That(File.ReadAllText(assetPath), Does.Contain("legacy-key"));
            Assert.That(ArcweaveUserSettings.ApiKey, Is.Empty);
            Assert.That(ArcweaveUserSettings.HasApiKeySavedInAsset(asset), Is.True, "The inspector must still offer to move the key.");
        }

        [Test]
        public void BlankKeySavedInTheAssetIsRemovedWithoutBeingMoved()
        {
            SaveLegacyKey("   ");

            var moved = ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset);

            Assert.That(moved, Is.False);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.Empty);
            Assert.That(LegacyKey(), Is.Empty);
        }

        [Test]
        public void KeySavedByAnOlderVersionIsReadAndMovedOutOfTheFile()
        {
            // An asset serialized by the plugin before the key moved to the user settings, with a key added
            var fixturePath = AssetDatabase.GUIDToAssetPath(LegacyFixtureGuid);
            Assume.That(fixturePath, Is.Not.Empty, "The legacy fixture is missing.");
            var fixture = File.ReadAllText(fixturePath);
            var withKey = Regex.Replace(fixture, @"^(\s*userAPIKey:)[^\r\n]*", "$1 legacy-fixture-key", RegexOptions.Multiline);
            Assume.That(withKey, Does.Contain("userAPIKey: legacy-fixture-key"));
            var legacyPath = temporaryFolder + "/Legacy.asset";
            File.WriteAllText(legacyPath, withKey);
            AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceSynchronousImport);
            var legacy = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(legacyPath);
            Assert.That(legacy, Is.Not.Null);

            Assert.That(ArcweaveUserSettings.HasApiKeySavedInAsset(legacy), Is.True);
            Assert.That(legacy.StoredApiKey(), Is.EqualTo("legacy-fixture-key"));
            Assert.That(ArcweaveApiKeyBuildCheck.AssetsWithSavedApiKey(), Has.Member(legacy));

            Assert.That(ArcweaveUserSettings.MoveApiKeyOutOfAsset(legacy), Is.True);
            Assert.That(ArcweaveUserSettings.ApiKey, Is.EqualTo("legacy-fixture-key"));
            Assert.That(File.ReadAllText(legacyPath), Does.Not.Contain("legacy-fixture-key"));

            AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            legacy = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(legacyPath);
            Assert.That(ArcweaveUserSettings.HasApiKeySavedInAsset(legacy), Is.False);
        }

        [Test]
        public void BuildCheckFindsAssetsThatStillContainAKey()
        {
            Assert.That(ArcweaveApiKeyBuildCheck.AssetsWithSavedApiKey(), Has.No.Member(asset));

            SaveLegacyKey("legacy-key");
            Assert.That(ArcweaveApiKeyBuildCheck.AssetsWithSavedApiKey(), Has.Member(asset));

            ArcweaveUserSettings.MoveApiKeyOutOfAsset(asset);
            Assert.That(ArcweaveApiKeyBuildCheck.AssetsWithSavedApiKey(), Has.No.Member(asset));
        }

        [Test]
        public void BuildCheckFindsAKeyInAnyProjectAssetOfAFile()
        {
            var second = ScriptableObject.CreateInstance<ArcweaveProjectAsset>();
            second.name = "Second";
            AssetDatabase.AddObjectToAsset(second, assetPath);
#pragma warning disable 618
            second.userAPIKey = "second-key";
#pragma warning restore 618
            EditorUtility.SetDirty(second);
            AssetDatabase.SaveAssets();

            var found = ArcweaveApiKeyBuildCheck.AssetsWithSavedApiKey().ToList();

            Assert.That(found, Has.Member(second));
            Assert.That(found, Has.No.Member(asset));
        }

        ///----------------------------------------------------------------------------------------------

#pragma warning disable 618 // The tests save keys the way older versions of the plugin did
        private void SaveLegacyKey(string key)
        {
            asset.userAPIKey = key;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private string LegacyKey() => asset.userAPIKey;
#pragma warning restore 618

        private static IEnumerator Await(Task task)
        {
            var deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            while ( !task.IsCompleted ) {
                if ( EditorApplication.timeSinceStartup > deadline ) {
                    Assert.Fail("Timed out waiting for the import.");
                }
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }
}
