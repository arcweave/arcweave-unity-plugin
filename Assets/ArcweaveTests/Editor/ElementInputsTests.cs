using System.Collections;
using System.Linq;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>Checks the Inputs of elements, branches and jumpers: the connections that end at them.</summary>
    public class ElementInputsTests
    {
        [Test]
        public void InputsAreTheConnectionsThatEndAtTheNode()
        {
            var project = new ProjectMaker(ProjectJson, null).MakeProject();
            var board = project.Boards.Single();
            var start = board.NodeWithID<Element>("start");
            var post = board.NodeWithID<Element>("post");
            var comment = board.NodeWithID<Element>("comment");
            var far = board.NodeWithID<Element>("far");
            var branch = board.NodeWithID<Branch>("branch");
            var jumper = board.NodeWithID<Jumper>("jumper");
            var condition = branch.Conditions.First();

            Assert.That(start.Inputs, Is.Empty);
            Assert.That(far.Inputs, Is.Empty);
            Assert.That(post.Inputs.Single(), Is.SameAs(start.Outputs.Single()));
            Assert.That(post.Inputs.Single().Source, Is.SameAs(start));

            // Ordered by board, then by the source's outputs: the element comes before the branch condition
            Assert.That(comment.Inputs.Select(input => input.Id), Is.EqualTo(new[] { "post-comment", "condition-comment" }));
            Assert.That(comment.Inputs[0], Is.SameAs(post.Outputs.Single(output => output.Id == "post-comment")));
            Assert.That(comment.Inputs[1], Is.SameAs(condition.Output));
            Assert.That(comment.Inputs[1].Source, Is.SameAs(condition));
            Assert.That(comment.Inputs.All(input => input.Target == comment), Is.True);

            Assert.That(branch.Inputs.Single().Id, Is.EqualTo("post-branch"));
            Assert.That(branch.Inputs.Single().Source, Is.SameAs(post));

            // A connection to a jumper is an input of the jumper, not of the element it jumps to
            Assert.That(jumper.Inputs.Single().Id, Is.EqualTo("far-jumper"));
            Assert.That(jumper.Inputs.Single().Source, Is.SameAs(far));
            Assert.That(jumper.Target, Is.SameAs(comment));
        }

        [Test]
        public void LinkingInputsAgainFollowsChangedOutputs()
        {
            var project = new ProjectMaker(ProjectJson, null).MakeProject();
            var board = project.Boards.Single();
            var post = board.NodeWithID<Element>("post");
            var comment = board.NodeWithID<Element>("comment");
            var far = board.NodeWithID<Element>("far");
            var toComment = post.Outputs.Single(output => output.Id == "post-comment");

            toComment.Set(toComment.RawLabel, post, far);
            project.LinkInputs();

            Assert.That(comment.Inputs.Select(input => input.Id), Is.EqualTo(new[] { "condition-comment" }));
            Assert.That(far.Inputs.Single(), Is.SameAs(toComment));
        }

        [UnityTest]
        public IEnumerator InputsAreLinkedAgainAfterTheProjectAssetIsDeserialized()
        {
            var asset = ScriptableObject.CreateInstance<ArcweaveProjectAsset>();
            ArcweaveProjectAsset copy = null;
            try {
                asset.importSource = ArcweaveProjectAsset.ImportSource.FromJson;
                asset.projectJsonFile = new TextAsset(ProjectJson);
                // The import continues on the main thread, so it is awaited by yielding instead of blocking
                var import = asset.ImportProjectAsync();
                var deadline = EditorApplication.timeSinceStartup + 30;
                while ( !import.IsCompleted ) {
                    if ( EditorApplication.timeSinceStartup > deadline ) { Assert.Fail("Timed out waiting for the import."); }
                    yield return null;
                }
                import.GetAwaiter().GetResult();

                // Instantiating copies the asset through serialization, which doesn't keep the inputs, and enables the copy, like loading the asset
                copy = Object.Instantiate(asset);

                var board = copy.Project.Boards.Single();
                var post = board.NodeWithID<Element>("post");
                var comment = board.NodeWithID<Element>("comment");
                var far = board.NodeWithID<Element>("far");
                var branch = board.NodeWithID<Branch>("branch");
                var jumper = board.NodeWithID<Jumper>("jumper");
                Assert.That(comment.Inputs.Select(input => input.Id), Is.EqualTo(new[] { "post-comment", "condition-comment" }));
                Assert.That(comment.Inputs[0], Is.SameAs(post.Outputs.Single(output => output.Id == "post-comment")));
                Assert.That(comment.Inputs[0].Source, Is.SameAs(post));
                Assert.That(branch.Inputs.Single().Source, Is.SameAs(post));
                Assert.That(jumper.Inputs.Single().Source, Is.SameAs(far));
                // The else condition has no output, which Unity deserializes as a connection without an id that must not break linking
                Assert.That(far.Inputs, Is.Empty);
            }
            finally {
                if ( copy != null ) { Object.DestroyImmediate(copy); }
                Object.DestroyImmediate(asset.projectJsonFile);
                Object.DestroyImmediate(asset);
            }
        }

        ///<summary>start → post; post → comment and post → branch; the branch's if condition → comment, its else condition has no output; far → jumper, which jumps to comment.</summary>
        private const string ProjectJson = @"{
            ""name"": ""Inputs Test"",
            ""startingElement"": ""start"",
            ""boards"": { ""main"": { ""name"": ""Main"", ""elements"": [""start"", ""post"", ""comment"", ""far""], ""branches"": [""branch""], ""jumpers"": [""jumper""] } },
            ""elements"": {
                ""start"": { ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"", ""outputs"": [""start-post""] },
                ""post"": { ""x"": 100, ""y"": 0, ""title"": ""Post"", ""content"": """", ""theme"": ""default"", ""outputs"": [""post-comment"", ""post-branch""] },
                ""comment"": { ""x"": 200, ""y"": 0, ""title"": ""Comment"", ""content"": """", ""theme"": ""default"" },
                ""far"": { ""x"": 300, ""y"": 0, ""title"": ""Far"", ""content"": """", ""theme"": ""default"", ""outputs"": [""far-jumper""] }
            },
            ""branches"": { ""branch"": { ""x"": 150, ""y"": 100, ""theme"": ""default"", ""conditions"": { ""ifCondition"": ""if"", ""elseCondition"": ""else"" } } },
            ""jumpers"": { ""jumper"": { ""x"": 400, ""y"": 0, ""elementId"": ""comment"" } },
            ""conditions"": { ""if"": { ""output"": ""condition-comment"", ""script"": ""true"" }, ""else"": { ""script"": """" } },
            ""connections"": {
                ""start-post"": { ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""post"", ""targetType"": ""elements"" },
                ""post-comment"": { ""sourceid"": ""post"", ""sourceType"": ""elements"", ""targetid"": ""comment"", ""targetType"": ""elements"" },
                ""post-branch"": { ""sourceid"": ""post"", ""sourceType"": ""elements"", ""targetid"": ""branch"", ""targetType"": ""branches"" },
                ""condition-comment"": { ""sourceid"": ""if"", ""sourceType"": ""conditions"", ""targetid"": ""comment"", ""targetType"": ""elements"" },
                ""far-jumper"": { ""sourceid"": ""far"", ""sourceType"": ""elements"", ""targetid"": ""jumper"", ""targetType"": ""jumpers"" }
            },
            ""components"": {},
            ""attributes"": {},
            ""variables"": {},
            ""notes"": {},
            ""assets"": {}
        }";
    }
}
