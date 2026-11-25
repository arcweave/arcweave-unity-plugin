using System.Collections;
using System.Text.RegularExpressions;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>A project can have no starting element, or one that was deleted. Checks that it imports with a null StartingElement
    /// and a warning, instead of throwing a NullReferenceException.</summary>
    public class StartingElementTests
    {
        private const double TimeoutSeconds = 30;

        [TestCase(null, TestName = "MissingStartingElementIsNull")]
        [TestCase(@"""startingElement"": null,", TestName = "NullStartingElementIsNull")]
        [TestCase(@"""startingElement"": """",", TestName = "EmptyStartingElementIsNull")]
        public void ProjectWithoutStartingElementIsImported(string startingElement)
        {
            LogAssert.Expect(LogType.Warning, new Regex("the project has no starting element"));

            var project = new ProjectMaker(ProjectJson(startingElement), null).MakeProject();

            Assert.That(project.StartingElement, Is.Null);
            Assert.That(project.Boards, Has.Count.EqualTo(1));
            Assert.DoesNotThrow(project.Initialize);
        }

        [Test]
        public void UnknownStartingElementIsNull()
        {
            LogAssert.Expect(LogType.Warning, new Regex("starting element 'deleted' was not found"));

            var project = new ProjectMaker(ProjectJson(@"""startingElement"": ""deleted"","), null).MakeProject();

            Assert.That(project.StartingElement, Is.Null);
            Assert.DoesNotThrow(project.Initialize);
        }

        [Test]
        public void NullStartingElementRecordIsNull()
        {
            LogAssert.Expect(LogType.Warning, new Regex("starting element 'deleted' was not found"));

            var json = ProjectJson(@"""startingElement"": ""deleted"",").Replace(@"""elements"": {", @"""elements"": { ""deleted"": null,");
            var project = new ProjectMaker(json, null).MakeProject();

            Assert.That(project.StartingElement, Is.Null);
        }

        [Test]
        public void StartingElementThatIsNotOnAnImportedBoardIsNull()
        {
            LogAssert.Expect(LogType.Warning, new Regex("starting element 'start' is not on any imported board"));

            // An export of some boards can keep the starting element but leave out its board, and the assets it uses.
            // The element is not made at all, so its missing cover asset doesn't fail the import.
            var json = ProjectJson(@"""startingElement"": ""start"",")
                .Replace(@"""elements"": [""start""]", @"""elements"": []")
                .Replace(@"""theme"": ""default"" }", @"""theme"": ""default"", ""assets"": { ""cover"": { ""id"": ""left-out"" } } }");
            var project = new ProjectMaker(json, null).MakeProject();

            Assert.That(project.StartingElement, Is.Null);
            Assert.DoesNotThrow(project.Initialize);
        }

        [Test]
        public void StartingElementCanBeSetToNull()
        {
            var project = new ProjectMaker(ProjectJson(@"""startingElement"": ""start"","), null).MakeProject();
            Assert.That(project.StartingElement.Id, Is.EqualTo("start"));

            Assert.DoesNotThrow(() => project.StartingElement = null);
            Assert.That(project.StartingElement, Is.Null);
        }

        [UnityTest]
        public IEnumerator ImportFromJsonWithoutStartingElementSucceeds()
        {
            var asset = ScriptableObject.CreateInstance<ArcweaveProjectAsset>();
            try {
                LogAssert.Expect(LogType.Warning, new Regex("the project has no starting element"));
                asset.importSource = ArcweaveProjectAsset.ImportSource.FromJson;
                asset.projectJsonFile = new TextAsset(ProjectJson(null));
                var callbacks = 0;
                string error = null;
                asset.ImportProject(() => callbacks++, e => { callbacks++; error = e; });
                yield return WaitUntil(() => callbacks > 0);

                Assert.That(error, Is.Null);
                Assert.That(asset.Project, Is.Not.Null);
                Assert.That(asset.Project.StartingElement, Is.Null);
                // A project without a starting element can't be played
                Assert.That(asset.IsImported, Is.False);
            }
            finally {
                Object.DestroyImmediate(asset.projectJsonFile);
                Object.DestroyImmediate(asset);
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            var deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            while ( !condition() ) {
                if ( EditorApplication.timeSinceStartup > deadline ) {
                    Assert.Fail("Timed out waiting for the import.");
                }
                yield return null;
            }
        }

        ///<summary>A project with one element, and the given startingElement property (null leaves it out).</summary>
        private static string ProjectJson(string startingElement) => $@"{{
            ""name"": ""Starting Element Test"",
            {startingElement}
            ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start""] }} }},
            ""elements"": {{
                ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"" }}
            }},
            ""branches"": {{}},
            ""jumpers"": {{}},
            ""conditions"": {{}},
            ""connections"": {{}},
            ""components"": {{}},
            ""attributes"": {{}},
            ""variables"": {{}},
            ""notes"": {{}},
            ""assets"": {{}}
        }}";
    }
}
