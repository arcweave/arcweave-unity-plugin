using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcweave.Interpreter.INodes;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using AWComponent = Arcweave.Project.Component;

namespace Arcweave.Tests
{
    ///<summary>Arcweave exports omit empty lists, eg. an ending element has no 'outputs' and most elements have no 'components'.
    /// Checks that missing and null lists are imported as empty instead of failing the import.</summary>
    public class OmittedListsTests
    {
        private const double TimeoutSeconds = 30;

        [TestCase(false, TestName = "OmittedListsAreImportedAsEmpty")]
        [TestCase(true, TestName = "NullListsAreImportedAsEmpty")]
        public void ListsAreImportedAsEmpty(bool nullLists)
        {
            var project = new ProjectMaker(ProjectJson(nullLists), null).MakeProject();

            var board = project.Boards.Single();
            Assert.That(board.Notes, Is.Empty);
            Assert.That(board.Attributes, Is.Empty);

            var start = board.NodeWithID<Element>("start");
            var end = board.NodeWithID<Element>("end");
            var branch = board.NodeWithID<Branch>("branch");
            Assert.That(project.StartingElement, Is.SameAs(start));
            Assert.That(board.Nodes, Is.EquivalentTo(new INode[] { start, end, branch, branch.Conditions.Single() }));

            Assert.That(start.Components, Is.Empty);
            Assert.That(start.Outputs.Single().Target, Is.SameAs(branch));
            Assert.That(end.Outputs, Is.Empty);
            Assert.That(end.Components, Is.Empty);
            Assert.That(end.Attributes, Is.Empty);

            // A branch without else-if or else conditions only has its if condition
            Assert.That(branch.Conditions.Single().Output.Target, Is.SameAs(end));

            var party = start.Attributes.Single();
            Assert.That(party.Type, Is.EqualTo(IAttribute.DataType.ComponentList));
            Assert.That((List<AWComponent>)party.data, Is.Empty);
            Assert.That(project.Components.Single().Attributes, Is.Empty);

            Assert.DoesNotThrow(project.Initialize);
        }

        [UnityTest]
        public IEnumerator ImportFromJsonWithOmittedListsSucceeds()
        {
            var asset = ScriptableObject.CreateInstance<ArcweaveProjectAsset>();
            try {
                asset.importSource = ArcweaveProjectAsset.ImportSource.FromJson;
                asset.projectJsonFile = new TextAsset(ProjectJson(nullLists: false));
                var callbacks = 0;
                string error = null;
                asset.ImportProject(() => callbacks++, e => { callbacks++; error = e; });
                yield return WaitUntil(() => callbacks > 0);

                Assert.That(error, Is.Null);
                Assert.That(asset.IsImported, Is.True);
                Assert.That(asset.Project.StartingElement.Id, Is.EqualTo("start"));
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

        ///<summary>A project whose empty lists are either omitted, as Arcweave exports them, or null.</summary>
        private static string ProjectJson(bool nullLists)
        {
            string List(string key) => nullLists ? $@", ""{key}"": null" : "";
            return $@"{{
                ""name"": ""Omitted Lists Test"",
                ""startingElement"": ""start"",
                ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start"", ""end""], ""branches"": [""branch""]{List("jumpers")}{List("notes")}{List("attributes")} }} }},
                ""elements"": {{
                    ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"", ""outputs"": [""to-branch""], ""attributes"": [""party""]{List("components")} }},
                    ""end"": {{ ""x"": 200, ""y"": 0, ""title"": ""Ending"", ""theme"": ""green""{List("outputs")}{List("components")}{List("attributes")} }}
                }},
                ""branches"": {{ ""branch"": {{ ""x"": 100, ""y"": 0, ""theme"": ""default"", ""conditions"": {{ ""ifCondition"": ""if""{List("elseIfConditions")}{List("elseCondition")} }} }} }},
                ""jumpers"": {{}},
                ""conditions"": {{ ""if"": {{ ""output"": ""to-end"", ""script"": ""true"" }} }},
                ""connections"": {{
                    ""to-branch"": {{ ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""branch"", ""targetType"": ""branches"" }},
                    ""to-end"": {{ ""sourceid"": ""if"", ""sourceType"": ""conditions"", ""targetid"": ""end"", ""targetType"": ""elements"" }}
                }},
                ""components"": {{ ""hero"": {{ ""name"": ""Hero""{List("attributes")} }} }},
                ""attributes"": {{ ""party"": {{ ""name"": ""Party"", ""cType"": ""elements"", ""cId"": ""start"", ""value"": {{ ""type"": ""component-list""{List("data")} }} }} }},
                ""variables"": {{}},
                ""notes"": {{}},
                ""assets"": {{}}
            }}";
        }
    }
}
