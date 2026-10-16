using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>Imports from web through a local stub of Arcweave's web API and checks how the media is downloaded and linked.</summary>
    public class MediaDownloadTests
    {
        private const string CoverId = "cover-asset";
        private const string HeroId = "hero-asset";
        private const string MusicId = "music-asset";
        private const double TimeoutSeconds = 30;

        private StubApiServer server;
        private string temporaryFolder;
        private StubbedWebProjectAsset asset;

        [SetUp]
        public void CreateProjectAsset()
        {
            server = new StubApiServer();
            server.SetFile(CoverId, Png(Color.red));
            server.SetFile(HeroId, Png(Color.green));
            server.SetFile(MusicId, Wav(100));

            var folderName = "ArcweaveMediaTest_" + Guid.NewGuid().ToString("N");
            temporaryFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);

            asset = ScriptableObject.CreateInstance<StubbedWebProjectAsset>();
            asset.importSource = ArcweaveProjectAsset.ImportSource.FromWeb;
            asset.userAPIKey = "test-key";
            asset.projectHash = "test-hash";
            asset.apiUrl = server.ApiUrl;
            asset.projectJson = ProjectJson();
            AssetDatabase.CreateAsset(asset, temporaryFolder + "/Project.asset");
        }

        [TearDown]
        public void DeleteProjectAsset()
        {
            server.Dispose();
            if ( Directory.Exists(temporaryFolder) ) {
                foreach ( var file in Directory.GetFiles(temporaryFolder, "*", SearchOption.AllDirectories) ) {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
            }
            AssetDatabase.DeleteAsset(temporaryFolder);
        }

        [UnityTest]
        public IEnumerator MediaIsNotDownloadedByDefault()
        {
            Assert.That(ScriptableObject.CreateInstance<ArcweaveProjectAsset>().downloadMedia, Is.False);
            Assert.That(asset.downloadMedia, Is.False);

            yield return Import();

            Assert.That(asset.IsImported, Is.True);
            Assert.That(server.RequestCount, Is.Zero);
            Assert.That(Cover().downloadedImage, Is.Null);
            Assert.That(Music().downloadedClip, Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(MediaFolder), Is.False);
        }

        [UnityTest]
        public IEnumerator ProjectIsPublishedOnlyAfterItsMediaIsLinked()
        {
            asset.downloadMedia = true;
            server.Hold();
            var import = asset.ImportProjectAsync();
            yield return WaitUntil(() => server.RequestCount > 0);

            Assert.That(asset.IsImporting, Is.True);
            Assert.That(asset.IsImported, Is.False, "The project must not be playable before its media is linked.");

            server.Release();
            yield return Await(import);

            Assert.That(asset.IsImported, Is.True);
            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/Cover.png"));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(MediaFolder + "/Hero.png"));
            Assert.That(Path(Music().downloadedClip), Is.EqualTo(MediaFolder + "/Music.wav"));
            Assert.That(Cover().ResolveImage(), Is.SameAs(Cover().downloadedImage));
            Assert.That(Music().TryGetAudioClip(), Is.SameAs(Music().downloadedClip));
            Assert.That(server.Authorizations, Is.All.EqualTo("Bearer test-key"));
        }

        [UnityTest]
        public IEnumerator ReimportUpdatesFilesInPlaceAndDeletesUnusedOnes()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);
            var coverGuid = AssetDatabase.AssetPathToGUID(coverPath);
            var musicPath = Path(Music().downloadedClip);

            var updatedCover = Png(Color.blue);
            server.SetFile(CoverId, updatedCover);
            asset.projectJson = ProjectJson(withMusic: false);
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(coverPath));
            Assert.That(AssetDatabase.AssetPathToGUID(coverPath), Is.EqualTo(coverGuid));
            Assert.That(File.ReadAllBytes(coverPath), Is.EqualTo(updatedCover));
            Assert.That(File.Exists(musicPath), Is.False);
        }

        [UnityTest]
        public IEnumerator FailedDownloadKeepsThePreviousFile()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);
            var coverBytes = File.ReadAllBytes(coverPath);

            server.Enqueue(CoverId, new StubApiServer.Response { status = 500 });
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(coverPath));
            Assert.That(File.ReadAllBytes(coverPath), Is.EqualTo(coverBytes));
        }

        [UnityTest]
        public IEnumerator RateLimitedDownloadIsRetried()
        {
            asset.downloadMedia = true;
            server.Enqueue(CoverId, new StubApiServer.Response { status = 429, retryAfter = "1" });

            yield return Import();

            Assert.That(server.RequestCountFor(CoverId), Is.EqualTo(2));
            Assert.That(Cover().downloadedImage, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ReadOnlyFileKeepsThePreviousCopyAndTheRestIsStillLinked()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);
            var coverBytes = File.ReadAllBytes(coverPath);
            var musicPath = Path(Music().downloadedClip);
            File.SetAttributes(coverPath, FileAttributes.ReadOnly);

            server.SetFile(CoverId, Png(Color.blue));
            var updatedMusic = Wav(200);
            server.SetFile(MusicId, updatedMusic);
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(coverPath));
            Assert.That(File.ReadAllBytes(coverPath), Is.EqualTo(coverBytes));
            Assert.That(Path(Hero().downloadedImage), Is.Not.Null);
            Assert.That(Path(Music().downloadedClip), Is.EqualTo(musicPath));
            Assert.That(File.ReadAllBytes(musicPath), Is.EqualTo(updatedMusic));
        }

        [UnityTest]
        public IEnumerator FileMovedDuringTheDownloadIsFollowedAndTheFileAtItsOldPathIsKept()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);

            var updatedCover = Png(Color.blue);
            server.SetFile(CoverId, updatedCover);
            var requests = server.RequestCount;
            server.Hold();
            var import = asset.ImportProjectAsync();
            yield return WaitUntil(() => server.RequestCount > requests);

            var movedPath = temporaryFolder + "/Moved.png";
            Assert.That(AssetDatabase.MoveAsset(coverPath, movedPath), Is.Empty);
            var unrelatedFile = Png(Color.yellow);
            File.WriteAllBytes(coverPath, unrelatedFile);
            AssetDatabase.ImportAsset(coverPath, ImportAssetOptions.ForceSynchronousImport);

            server.Release();
            yield return Await(import);

            Assert.That(File.ReadAllBytes(coverPath), Is.EqualTo(unrelatedFile));
            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(movedPath));
            Assert.That(File.ReadAllBytes(movedPath), Is.EqualTo(updatedCover));
        }

        [UnityTest]
        public IEnumerator ImportThatCannotDownloadKeepsTheMediaOfThePreviousImport()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverImage = Cover().downloadedImage;
            var heroImage = Hero().downloadedImage;
            var musicClip = Music().downloadedClip;
            var requests = server.RequestCount;

            // Like a reimport in a player, where media cannot be downloaded
            asset.importSource = ArcweaveProjectAsset.ImportSource.FromJson;
            asset.projectJsonFile = new TextAsset(asset.projectJson);
            yield return Import();

            Assert.That(server.RequestCount, Is.EqualTo(requests));
            Assert.That(Cover().downloadedImage, Is.SameAs(coverImage));
            Assert.That(Hero().downloadedImage, Is.SameAs(heroImage));
            Assert.That(Music().downloadedClip, Is.SameAs(musicClip));
        }

        [UnityTest]
        public IEnumerator DisablingDownloadMediaStopsUsingTheDownloadedMedia()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);
            var requests = server.RequestCount;

            asset.downloadMedia = false;
            yield return Import();

            Assert.That(server.RequestCount, Is.EqualTo(requests));
            Assert.That(Cover().downloadedImage, Is.Null);
            Assert.That(Music().downloadedClip, Is.Null);
            Assert.That(File.Exists(coverPath), Is.True, "Disabling Download Media does not delete downloaded files.");
        }

        [UnityTest]
        public IEnumerator AssetIdIsNotUsedInPaths()
        {
            asset.downloadMedia = true;
            // Not a valid file name, which 3.1.0 skipped since it named the file
            const string id = "cover:asset*";
            asset.projectJson = ProjectJson(coverId: id);
            server.SetFile(id, Png(Color.red));

            yield return Import();

            Assert.That(server.RequestCountFor(id), Is.EqualTo(1));
            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/Cover.png"));
            var files = Directory.GetFiles(MediaFolder, "*", SearchOption.AllDirectories).Where(file => !file.EndsWith(".meta")).Select(file => file.Replace('\\', '/'));
            Assert.That(files, Is.EquivalentTo(new[] { MediaFolder + "/Cover.png", MediaFolder + "/Hero.png", MediaFolder + "/Music.wav" }));
        }

        [UnityTest]
        public IEnumerator UnsavedInstanceKeepsTheMediaOfThePreviousImport()
        {
            asset.downloadMedia = true;
            yield return Import();
            var copy = UnityEngine.Object.Instantiate(asset);
            var coverImage = copy.Project.Boards.Single().Nodes.OfType<Element>().Single().cover.downloadedImage;
            Assert.That(coverImage, Is.Not.Null);
            var requests = server.RequestCount;

            try {
                yield return Await(copy.ImportProjectAsync());

                Assert.That(server.RequestCount, Is.EqualTo(requests));
                var element = copy.Project.Boards.Single().Nodes.OfType<Element>().Single();
                Assert.That(element.cover.downloadedImage, Is.SameAs(coverImage));
                Assert.That(element.AudioAssets.Single().downloadedClip, Is.SameAs(Music().downloadedClip));
            }
            finally {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [UnityTest]
        public IEnumerator FileThatCouldNotBeDeletedIsDeletedLaterAndNeverLinkedAgain()
        {
            asset.downloadMedia = true;
            yield return Import();
            var pngPath = Path(Cover().downloadedImage);

            // The cover becomes a jpg while the old png can't be deleted
            LogAssert.ignoreFailingMessages = true;
            asset.projectJson = ProjectJson(coverName: "Cover.jpg");
            server.SetFile(CoverId, Jpg(Color.blue));
            using ( new FileStream(pngPath, FileMode.Open, FileAccess.Read, FileShare.None) ) {
                yield return Import();
            }
            LogAssert.ignoreFailingMessages = false;
            var jpgPath = Path(Cover().downloadedImage);
            Assert.That(jpgPath, Does.EndWith(".jpg"));
            Assert.That(File.Exists(pngPath), Is.True, "The locked png should still exist for this test to be meaningful.");

            server.Enqueue(CoverId, new StubApiServer.Response { status = 500 });
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(jpgPath));
            Assert.That(File.Exists(pngPath), Is.False);
        }

        [UnityTest]
        public IEnumerator FilesAreNamedAfterTheirAssetsInTheMediaFolderWithoutItsArcweaveFolders()
        {
            asset.downloadMedia = true;

            yield return Import();

            var files = Directory.GetFiles(MediaFolder, "*", SearchOption.AllDirectories).Where(file => !file.EndsWith(".meta")).Select(file => file.Replace('\\', '/'));
            Assert.That(files, Is.EquivalentTo(new[] { MediaFolder + "/Cover.png", MediaFolder + "/Hero.png", MediaFolder + "/Music.wav" }));
            Assert.That(Directory.GetDirectories(MediaFolder), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NamesAreSanitizedAndStayInsideTheMediaFolder()
        {
            asset.downloadMedia = true;
            asset.projectJson = ProjectJson(coverName: "../Act 1: Cover.png", heroName: "con.png");

            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/_.._Act 1_ Cover.png"));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(MediaFolder + "/con_.png"));
            Assert.That(Directory.GetFiles(temporaryFolder).Select(System.IO.Path.GetFileName), Is.EquivalentTo(new[] { "Project.asset", "Project.asset.meta", "Project Media.meta" }));
        }

        [UnityTest]
        public IEnumerator NamesWithCharactersNotAllowedInPathsOrOnlyAnExtensionAreDownloaded()
        {
            asset.downloadMedia = true;
            asset.projectJson = ProjectJson(coverName: "a<b|c>\"d.png", heroName: ".png");

            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/a_b_c__d.png"));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(MediaFolder + "/_.png"));
            Assert.That(Music().downloadedClip, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LongNamesAreShortenedAndStillUnique()
        {
            // Two names that only differ after the first 100 characters
            asset.downloadMedia = true;
            var prefix = new string('a', 100);
            asset.projectJson = ProjectJson(coverName: prefix + "-cover.png", heroName: prefix + "-hero.png");

            yield return Import();

            Assert.That(new[] { Path(Cover().downloadedImage), Path(Hero().downloadedImage) },
                Is.EquivalentTo(new[] { MediaFolder + "/" + prefix + ".png", MediaFolder + "/" + prefix + " 1.png" }));
        }

        [UnityTest]
        public IEnumerator AssetsWithTheSameNameAreBothDownloadedAndKeepTheirPaths()
        {
            asset.downloadMedia = true;
            var heroImage = Png(Color.green);
            asset.projectJson = ProjectJson(heroName: "Cover.png");

            yield return Import();

            var coverPath = Path(Cover().downloadedImage);
            var heroPath = Path(Hero().downloadedImage);
            Assert.That(new[] { coverPath, heroPath }, Is.EquivalentTo(new[] { MediaFolder + "/Cover.png", MediaFolder + "/Cover 1.png" }));
            Assert.That(File.ReadAllBytes(heroPath), Is.EqualTo(heroImage));
            var coverWriteTime = File.GetLastWriteTimeUtc(coverPath);
            var heroWriteTime = File.GetLastWriteTimeUtc(heroPath);

            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(coverPath));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(heroPath));
            Assert.That(File.GetLastWriteTimeUtc(coverPath), Is.EqualTo(coverWriteTime), "Unchanged files are not rewritten.");
            Assert.That(File.GetLastWriteTimeUtc(heroPath), Is.EqualTo(heroWriteTime), "Unchanged files are not rewritten.");
        }

        [UnityTest]
        public IEnumerator FileTheDownloaderDoesNotOwnAtTheNameIsNeverOverwritten()
        {
            asset.downloadMedia = true;
            yield return Import();
            var userPath = MediaFolder + "/Cover.jpg";
            var userFile = Png(Color.yellow);
            File.WriteAllBytes(userPath, userFile);
            AssetDatabase.ImportAsset(userPath, ImportAssetOptions.ForceSynchronousImport);

            // The cover becomes a jpg, which needs a new file
            asset.projectJson = ProjectJson(coverName: "Cover.jpg");
            server.SetFile(CoverId, Jpg(Color.blue));
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/Cover 1.jpg"));
            Assert.That(File.ReadAllBytes(userPath), Is.EqualTo(userFile));
        }

        [UnityTest]
        public IEnumerator NameThatDiffersOnlyInCaseFromAFileTheDownloaderDoesNotOwnGetsANumber()
        {
            asset.downloadMedia = true;
            yield return Import();
            var userPath = MediaFolder + "/cover.jpg";
            var userFile = Png(Color.yellow);
            File.WriteAllBytes(userPath, userFile);
            AssetDatabase.ImportAsset(userPath, ImportAssetOptions.ForceSynchronousImport);

            asset.projectJson = ProjectJson(coverName: "Cover.jpg");
            server.SetFile(CoverId, Jpg(Color.blue));
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/Cover 1.jpg"));
            Assert.That(File.ReadAllBytes(userPath), Is.EqualTo(userFile));
        }

        [UnityTest]
        public IEnumerator RenamingOrMovingInArcweaveKeepsTheFileAndItsGuid()
        {
            asset.downloadMedia = true;
            yield return Import();
            var coverPath = Path(Cover().downloadedImage);
            var coverGuid = AssetDatabase.AssetPathToGUID(coverPath);

            // Renamed and moved to another Arcweave folder
            var updatedCover = Png(Color.blue);
            server.SetFile(CoverId, updatedCover);
            asset.projectJson = ProjectJson(coverName: "Portrait.png", coverInAudio: true);
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(coverPath));
            Assert.That(AssetDatabase.AssetPathToGUID(coverPath), Is.EqualTo(coverGuid));
            Assert.That(File.ReadAllBytes(coverPath), Is.EqualTo(updatedCover));
            Assert.That(File.Exists(MediaFolder + "/Portrait.png"), Is.False);
        }

        [UnityTest]
        public IEnumerator ChangingTheExtensionMakesANewFileWithTheCurrentNameAndDeletesTheOldOne()
        {
            asset.downloadMedia = true;
            yield return Import();
            var pngPath = Path(Cover().downloadedImage);
            var pngGuid = AssetDatabase.AssetPathToGUID(pngPath);

            asset.projectJson = ProjectJson(coverName: "Portrait.jpg");
            server.SetFile(CoverId, Jpg(Color.blue));
            yield return Import();

            var jpgPath = MediaFolder + "/Portrait.jpg";
            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(jpgPath));
            Assert.That(AssetDatabase.AssetPathToGUID(jpgPath), Is.Not.EqualTo(pngGuid));
            Assert.That(File.Exists(pngPath), Is.False);
        }

        [UnityTest]
        public IEnumerator MediaDownloadedByVersion310IsRenamedOnceKeepingItsGuid()
        {
            // The media folder of 3.1.0: files named after their asset ids, tracked by GUID
            AssetDatabase.CreateFolder(temporaryFolder, "Project Media");
            var oldPaths = new[] { CoverId + ".png", HeroId + ".png", MusicId + ".wav" }.Select(name => MediaFolder + "/" + name).ToArray();
            File.WriteAllBytes(oldPaths[0], Png(Color.red));
            File.WriteAllBytes(oldPaths[1], Png(Color.green));
            File.WriteAllBytes(oldPaths[2], Wav(100));
            foreach ( var path in oldPaths ) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
            var guids = oldPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            SetDownloadedMedia(new[] { CoverId, HeroId, MusicId }, guids);

            // The user renamed the hero's file, which keeps its name
            var heroPath = MediaFolder + "/My Hero.png";
            Assert.That(AssetDatabase.MoveAsset(oldPaths[1], heroPath), Is.Empty);

            // A user's asset that references the media, by GUID like scenes and prefabs do
            var referencesPath = temporaryFolder + "/References.asset";
            var references = ScriptableObject.CreateInstance<MediaReferences>();
            references.image = AssetDatabase.LoadAssetAtPath<Texture2D>(oldPaths[0]);
            references.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(oldPaths[2]);
            AssetDatabase.CreateAsset(references, referencesPath);
            AssetDatabase.SaveAssets();

            asset.downloadMedia = true;
            yield return Import();

            var newPaths = new[] { MediaFolder + "/Cover.png", heroPath, MediaFolder + "/Music.wav" };
            Assert.That(new[] { Path(Cover().downloadedImage), Path(Hero().downloadedImage), Path(Music().downloadedClip) }, Is.EqualTo(newPaths));
            Assert.That(newPaths.Select(AssetDatabase.AssetPathToGUID), Is.EqualTo(guids));
            Assert.That(oldPaths.Where(File.Exists), Is.Empty);

            Resources.UnloadAsset(references);
            references = AssetDatabase.LoadAssetAtPath<MediaReferences>(referencesPath);
            Assert.That(Path(references.image), Is.EqualTo(newPaths[0]));
            Assert.That(Path(references.clip), Is.EqualTo(newPaths[2]));

            // Once renamed, the file keeps its name
            asset.projectJson = ProjectJson(coverName: "Portrait.png");
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(newPaths[0]));
        }

        [UnityTest]
        public IEnumerator FileSavedAtAPlannedPathDuringAMoveIsNeverOverwritten()
        {
            // The hero is still named after its asset id by 3.1.0, so it is moved, while the cover needs a new file, in either order
            asset.downloadMedia = true;
            yield return Import();
            var heroPath = Path(Hero().downloadedImage);
            Assert.That(AssetDatabase.MoveAsset(heroPath, MediaFolder + "/" + HeroId + ".png"), Is.Empty);
            var userPath = MediaFolder + "/Cover.jpg";
            var userFile = Png(Color.yellow);
            MoveCallback.onWillMoveAsset = (source, destination) => {
                if ( !File.Exists(userPath) ) { File.WriteAllBytes(userPath, userFile); }
            };

            try {
                asset.projectJson = ProjectJson(coverName: "Cover.jpg");
                server.SetFile(CoverId, Jpg(Color.blue));
                yield return Import();
            }
            finally {
                MoveCallback.onWillMoveAsset = null;
            }

            Assert.That(File.ReadAllBytes(userPath), Is.EqualTo(userFile));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(heroPath));
            Assert.That(Cover().downloadedImage, Is.Not.Null);
            Assert.That(Path(Cover().downloadedImage), Is.Not.EqualTo(userPath));
        }

        [UnityTest]
        public IEnumerator FileTheUserRenamedOrMovedStaysWhereItIs()
        {
            asset.downloadMedia = true;
            yield return Import();
            var renamedPath = MediaFolder + "/Mine.png";
            Assert.That(AssetDatabase.MoveAsset(Path(Cover().downloadedImage), renamedPath), Is.Empty);
            var movedPath = temporaryFolder + "/Hero Elsewhere.png";
            Assert.That(AssetDatabase.MoveAsset(Path(Hero().downloadedImage), movedPath), Is.Empty);

            var updatedCover = Png(Color.blue);
            server.SetFile(CoverId, updatedCover);
            yield return Import();

            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(renamedPath));
            Assert.That(File.ReadAllBytes(renamedPath), Is.EqualTo(updatedCover));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(movedPath));
            Assert.That(File.Exists(MediaFolder + "/Cover.png"), Is.False);
            Assert.That(File.Exists(MediaFolder + "/Hero.png"), Is.False);
        }

        ///----------------------------------------------------------------------------------------------

        private string MediaFolder => temporaryFolder + "/Project Media";

        private Element Element() => asset.Project.Boards.Single().Nodes.OfType<Element>().Single();
        private Cover Cover() => Element().cover;
        private Cover Hero() => asset.Project.Components.Single().cover;
        private AudioAsset Music() => Element().AudioAssets.Single();

        private static string Path(UnityEngine.Object media) => media == null ? null : AssetDatabase.GetAssetPath(media);

        ///<summary>Makes the project asset own the files, as an earlier import would have.</summary>
        private void SetDownloadedMedia(string[] assetIds, string[] guids)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("mediaOwnerGuid").stringValue = AssetDatabase.AssetPathToGUID(temporaryFolder + "/Project.asset");
            serialized.FindProperty("mediaFolderGuid").stringValue = AssetDatabase.AssetPathToGUID(MediaFolder);
            var downloadedMedia = serialized.FindProperty("downloadedMedia");
            downloadedMedia.arraySize = assetIds.Length;
            for ( var i = 0; i < assetIds.Length; i++ ) {
                downloadedMedia.GetArrayElementAtIndex(i).FindPropertyRelative("assetId").stringValue = assetIds[i];
                downloadedMedia.GetArrayElementAtIndex(i).FindPropertyRelative("guid").stringValue = guids[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private IEnumerator Import() => Await(asset.ImportProjectAsync());

        private static IEnumerator Await(Task task)
        {
            yield return WaitUntil(() => task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            var deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            while ( !condition() ) {
                if ( EditorApplication.timeSinceStartup > deadline ) {
                    Assert.Fail("Timed out waiting for the import.");
                }
                yield return null;
            }
        }

        ///<summary>A project whose assets are in Arcweave folders, which downloaded files don't mirror: Images (with the cover and the hero) and Audio (with the music).</summary>
        private static string ProjectJson(bool withMusic = true, string coverId = CoverId, string coverName = "Cover.png",
            string heroName = "Hero.png", bool coverInAudio = false)
        {
            var audio = withMusic ? $@", ""audio"": [{{ ""id"": ""music-instance"", ""mode"": ""loop"", ""asset"": ""{MusicId}"", ""delay"": 0 }}]" : "";
            var imagesChildren = coverInAudio ? new[] { HeroId } : new[] { HeroId, coverId };
            var audioChildren = coverInAudio ? new[] { MusicId, coverId } : new[] { MusicId };
            return $@"{{
                ""name"": ""Media Test"",
                ""startingElement"": ""start"",
                ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start""], ""branches"": [], ""jumpers"": [], ""notes"": [] }} }},
                ""elements"": {{ ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"", ""outputs"": [], ""components"": [""hero""],
                    ""assets"": {{ ""cover"": {{ ""id"": {Quote(coverId)} }}{audio} }} }} }},
                ""branches"": {{}}, ""jumpers"": {{}}, ""conditions"": {{}}, ""connections"": {{}},
                ""components"": {{ ""hero"": {{ ""name"": ""Hero"", ""attributes"": [], ""assets"": {{ ""cover"": {{ ""id"": ""{HeroId}"" }} }} }} }},
                ""attributes"": {{}}, ""variables"": {{}}, ""notes"": {{}},
                ""assets"": {{
                    ""root-folder"": {{ ""root"": true, ""children"": [""images-folder"", ""audio-folder""] }},
                    ""images-folder"": {{ ""name"": ""Images"", ""children"": [{string.Join(", ", imagesChildren.Select(Quote))}] }},
                    ""audio-folder"": {{ ""name"": ""Audio"", ""children"": [{string.Join(", ", audioChildren.Select(Quote))}] }},
                    {Quote(coverId)}: {{ ""name"": {Quote(coverName)}, ""type"": ""image"" }},
                    ""{HeroId}"": {{ ""name"": {Quote(heroName)}, ""type"": ""image"" }},
                    ""{MusicId}"": {{ ""name"": ""Music.wav"", ""type"": ""audio"", ""volume"": 1 }}
                }}
            }}";
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static byte[] Png(Color color)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(Enumerable.Repeat(color, 4).ToArray());
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        private static byte[] Jpg(Color color)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(Enumerable.Repeat(color, 4).ToArray());
            var jpg = texture.EncodeToJPG();
            UnityEngine.Object.DestroyImmediate(texture);
            return jpg;
        }

        ///<summary>A silent 16-bit mono PCM wav file.</summary>
        private static byte[] Wav(int samples)
        {
            const int sampleRate = 8000;
            var dataSize = samples * 2;
            using ( var stream = new MemoryStream() )
            using ( var writer = new BinaryWriter(stream) ) {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(new byte[dataSize]);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
