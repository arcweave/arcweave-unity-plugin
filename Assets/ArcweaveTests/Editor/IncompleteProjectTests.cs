using System;
using System.Collections.Generic;
using System.Linq;
using Arcweave.Interpreter.INodes;
using Arcweave.Project;
using NUnit.Framework;
using UnityEngine;

namespace Arcweave.Tests
{
    ///<summary>Imports projects with null values and ids of items that are not in the project, eg. when an export leaves out some boards.
    /// Checks that they are skipped with a warning instead of failing the import or the story later.</summary>
    public class IncompleteProjectTests
    {
        private readonly List<string> warnings = new List<string>();

        [SetUp]
        public void CollectWarnings()
        {
            warnings.Clear();
            Application.logMessageReceived += OnLogMessage;
        }

        [TearDown]
        public void StopCollectingWarnings()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if ( type == LogType.Warning ) { warnings.Add(message); }
        }

        [Test]
        public void NullValuesAreTreatedAsMissing()
        {
            var project = Import(@"{
                ""name"": ""Null Values Test"",
                ""startingElement"": ""start"",
                ""boards"": { ""main"": { ""name"": ""Main"", ""children"": null, ""elements"": [""start""], ""attributes"": [""title""] } },
                ""elements"": { ""start"": { ""title"": ""Start"", ""components"": [""hero""], ""outputs"": [null],
                    ""assets"": { ""cover"": { ""file"": null, ""id"": null, ""url"": null }, ""audio"": null } } },
                ""components"": { ""hero"": { ""name"": ""Hero"", ""children"": null, ""assets"": { ""cover"": { ""id"": ""portrait"" } } } },
                ""attributes"": { ""title"": { ""name"": ""Title"", ""cType"": ""boards"", ""cId"": ""main"", ""value"": { ""type"": ""string"", ""plain"": null, ""data"": null } } },
                ""variables"": { ""coins"": { ""name"": ""coins"", ""type"": ""integer"", ""value"": null } },
                ""assets"": { ""portrait"": { ""name"": ""Portrait.png"", ""type"": ""image"", ""volume"": null } }
            }");

            // 'children: null' is not a folder
            var board = project.Boards.Single();
            var start = board.NodeWithID<Element>("start");
            Assert.That(project.Components.Single().Name, Is.EqualTo("Hero"));
            Assert.That(start.Components.Single(), Is.SameAs(project.Components.Single()));

            // missing coordinates default to 0
            Assert.That(start.Pos, Is.EqualTo(Vector2Int.zero));
            Assert.That(start.Outputs, Is.Empty);
            Assert.That(start.cover, Is.Null);
            Assert.That(start.AudioAssets, Is.Null);
            Assert.That(project.Components.Single().cover.filePath, Is.EqualTo("Portrait.png"));
            Assert.That(board.Attributes.Single().Type, Is.EqualTo(IAttribute.DataType.StringRichText));

            Assert.That(project.GlobalVariables.Single().Value, Is.EqualTo(0));
            Assert.That(warnings, Has.Some.Contains("variable 'coins' has no value"));
        }

        [Test]
        public void MissingIdsAreSkippedWithAWarning()
        {
            var project = Import(@"{
                ""name"": ""Missing Ids Test"",
                ""startingElement"": ""start"",
                ""boards"": { ""main"": { ""name"": ""Main"", ""elements"": [""start"", ""end"", ""ghost-element""], ""branches"": [""branch""],
                    ""jumpers"": [""jumper"", ""ghost-jumper""], ""notes"": [""ghost-note""], ""attributes"": [""ghost-board-attribute""] } },
                ""elements"": {
                    ""start"": { ""x"": 0, ""y"": 0, ""title"": ""Start"", ""outputs"": [""to-end"", ""to-branch"", ""to-jumper"", ""to-nowhere"", ""ghost-connection""],
                        ""components"": [""ghost-component""], ""attributes"": [""ghost-attribute""], ""assets"": { ""cover"": { ""id"": ""ghost-asset"" } } },
                    ""end"": { ""x"": 200, ""y"": 0, ""title"": ""End"" }
                },
                ""branches"": { ""branch"": { ""x"": 100, ""y"": 0, ""conditions"": { ""ifCondition"": ""ghost-condition"", ""elseIfConditions"": [""ghost-else-if""], ""elseCondition"": ""else"" } } },
                ""jumpers"": { ""jumper"": { ""x"": 100, ""y"": 100, ""elementId"": ""ghost-target"" } },
                ""conditions"": { ""else"": { ""output"": ""else-to-end"", ""script"": null } },
                ""connections"": {
                    ""to-end"": { ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""end"", ""targetType"": ""elements"" },
                    ""to-branch"": { ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""branch"", ""targetType"": ""branches"" },
                    ""to-jumper"": { ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""jumper"", ""targetType"": ""jumpers"" },
                    ""to-nowhere"": { ""sourceid"": ""start"", ""sourceType"": ""elements"", ""targetid"": ""ghost-target"", ""targetType"": ""elements"" },
                    ""else-to-end"": { ""sourceid"": ""else"", ""sourceType"": ""conditions"", ""targetid"": ""end"", ""targetType"": ""elements"" }
                }
            }");

            var board = project.Boards.Single();
            var start = board.NodeWithID<Element>("start");
            var end = board.NodeWithID<Element>("end");
            var branch = board.NodeWithID<Branch>("branch");
            var jumper = board.NodeWithID<Jumper>("jumper");
            Assert.That(board.Nodes, Has.None.Null);
            Assert.That(board.Nodes, Is.EquivalentTo(new INode[] { start, end, branch, jumper, branch.Conditions.Single() }));
            Assert.That(board.Notes, Is.Empty);
            Assert.That(board.Attributes, Is.Empty);

            Assert.That(start.Outputs.Select(output => output.Id), Is.EqualTo(new[] { "to-end", "to-branch", "to-jumper", "to-nowhere" }));
            Assert.That(start.Components, Is.Empty);
            Assert.That(start.Attributes, Is.Empty);
            Assert.That(start.cover, Is.Null);
            Assert.That(branch.Conditions.Single().Id, Is.EqualTo("else"));
            Assert.That(jumper.Target, Is.Null);

            foreach ( var warning in new[] {
                "element 'ghost-element'", "jumper 'ghost-jumper'", "note 'ghost-note'", "attribute 'ghost-board-attribute'",
                "connection 'ghost-connection'", "component 'ghost-component'", "attribute 'ghost-attribute'",
                "condition 'ghost-condition'", "condition 'ghost-else-if'", "element 'ghost-target'",
                "connection 'to-nowhere' has no target", "asset 'ghost-asset'" } ) {
                Assert.That(warnings, Has.Some.Contains(warning));
            }

            // Only the paths that lead to an element are options: directly and through the branch's else condition
            project.Initialize();
            var options = start.GetOptions();
            Assert.That(options.Paths.Select(path => path.TargetElement), Is.EqualTo(new[] { end, end }));
        }

        [TestCase(@"null", "The project has no starting element", TestName = "ProjectWithoutAStartingElementFailsWithAClearMessage")]
        [TestCase(@"""ghost""", "The project's starting element 'ghost' was not found", TestName = "ProjectWithAMissingStartingElementFailsWithAClearMessage")]
        public void ProjectWithoutAStartingElementFailsWithAClearMessage(string startingElement, string message)
        {
            var json = @"{ ""name"": ""No Start"", ""startingElement"": " + startingElement + @",
                ""boards"": { ""main"": { ""name"": ""Main"", ""elements"": [""start""] } },
                ""elements"": { ""start"": { ""x"": 0, ""y"": 0, ""title"": ""Start"" } } }";

            var exception = Assert.Throws<InvalidOperationException>(() => Import(json));
            Assert.That(exception.Message, Does.StartWith(message));
        }

        private static Arcweave.Project.Project Import(string json) => new ProjectMaker(json, null).MakeProject();
    }
}
