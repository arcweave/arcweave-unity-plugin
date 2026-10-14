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
            Assert.That(Path(Cover().downloadedImage), Is.EqualTo(MediaFolder + "/" + CoverId + ".png"));
            Assert.That(Path(Hero().downloadedImage), Is.EqualTo(MediaFolder + "/" + HeroId + ".png"));
            Assert.That(Path(Music().downloadedClip), Is.EqualTo(MediaFolder + "/" + MusicId + ".wav"));
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
        public IEnumerator AssetIdThatIsNotAFileNameIsSkipped()
        {
            asset.downloadMedia = true;
            asset.projectJson = ProjectJson(coverId: "nested/escape");
            server.SetFile("nested/escape", Png(Color.red));

            yield return Import();

            Assert.That(server.RequestCountFor("nested/escape"), Is.Zero);
            Assert.That(Cover().downloadedImage, Is.Null);
            Assert.That(Hero().downloadedImage, Is.Not.Null);
            Assert.That(Directory.Exists(MediaFolder + "/nested"), Is.False);
        }

        ///----------------------------------------------------------------------------------------------

        private string MediaFolder => temporaryFolder + "/Project Media";

        private Element Element() => asset.Project.Boards.Single().Nodes.OfType<Element>().Single();
        private Cover Cover() => Element().cover;
        private Cover Hero() => asset.Project.Components.Single().cover;
        private AudioAsset Music() => Element().AudioAssets.Single();

        private static string Path(UnityEngine.Object media) => media == null ? null : AssetDatabase.GetAssetPath(media);

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

        private static string ProjectJson(bool withMusic = true, string coverId = CoverId)
        {
            var audio = withMusic ? $@", ""audio"": [{{ ""id"": ""music-instance"", ""mode"": ""loop"", ""asset"": ""{MusicId}"", ""delay"": 0 }}]" : "";
            return $@"{{
                ""name"": ""Media Test"",
                ""startingElement"": ""start"",
                ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start""], ""branches"": [], ""jumpers"": [], ""notes"": [] }} }},
                ""elements"": {{ ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"", ""outputs"": [], ""components"": [""hero""],
                    ""assets"": {{ ""cover"": {{ ""id"": ""{coverId}"" }}{audio} }} }} }},
                ""branches"": {{}}, ""jumpers"": {{}}, ""conditions"": {{}}, ""connections"": {{}},
                ""components"": {{ ""hero"": {{ ""name"": ""Hero"", ""attributes"": [], ""assets"": {{ ""cover"": {{ ""id"": ""{HeroId}"" }} }} }} }},
                ""attributes"": {{}}, ""variables"": {{}}, ""notes"": {{}},
                ""assets"": {{
                    ""{coverId}"": {{ ""name"": ""Cover.png"", ""type"": ""image"" }},
                    ""{HeroId}"": {{ ""name"": ""Hero.png"", ""type"": ""image"" }},
                    ""{MusicId}"": {{ ""name"": ""Music.wav"", ""type"": ""audio"", ""volume"": 1 }}
                }}
            }}";
        }

        private static byte[] Png(Color color)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(Enumerable.Repeat(color, 4).ToArray());
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
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
