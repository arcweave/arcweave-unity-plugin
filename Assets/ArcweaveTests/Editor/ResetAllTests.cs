using System.Collections.Generic;
using System.Linq;
using Arcweave.Interpreter;
using Arcweave.Project;
using NUnit.Framework;

namespace Arcweave.Tests
{
    ///<summary>Checks Arcscript's resetAll(), which resets every variable except the ones it is given,
    /// both with no arguments (which used to throw a NullReferenceException) and with arguments.</summary>
    public class ResetAllTests
    {
        [Test]
        public void ResetAllWithoutArgumentsResetsEveryVariable()
        {
            var project = MakeChangedProject();

            var output = new AwInterpreter(project).RunScript("<pre><code>resetAll()</code></pre>");

            Assert.That(output.Changes, Is.EqualTo(new Dictionary<string, object> {
                ["health-id"] = 100,
                ["name-id"] = "hero",
                ["gold-id"] = 5,
            }));
        }

        [Test]
        public void ResetAllWithArgumentsKeepsThoseVariables()
        {
            var project = MakeChangedProject();

            var output = new AwInterpreter(project).RunScript("<pre><code>resetAll(health)</code></pre>");

            Assert.That(output.Changes, Is.EqualTo(new Dictionary<string, object> {
                ["name-id"] = "hero",
                ["gold-id"] = 5,
            }));
        }

        [Test]
        public void ResetAllWithoutArgumentsInAContentScriptResetsTheVariables()
        {
            var project = MakeChangedProject();
            var start = project.ElementWithId("start");

            start.RunContentScript();

            Assert.That(project.GetVariable("health").Value, Is.EqualTo(100));
            Assert.That(project.GetVariable("name").Value, Is.EqualTo("hero"));
            Assert.That(project.Boards.Single().Variables.Single().Value, Is.EqualTo(5));
        }

        [Test]
        public void FunctionsWithoutArgumentsStillWork()
        {
            var project = MakeChangedProject();

            Assert.DoesNotThrow(() => new AwInterpreter(project).RunScript("<pre><code>random()</code></pre>"));

            var start = project.ElementWithId("start");
            start.Visits = 2;
            new AwInterpreter(project).RunScript("<pre><code>resetVisits()</code></pre>");
            Assert.That(start.Visits, Is.EqualTo(0));

            // visits() without arguments counts the visits of the element the script runs in
            start.Visits = 3;
            var output = new AwInterpreter(project, "start").RunScript("<pre><code>health = visits()</code></pre>");
            Assert.That(output.Changes["health-id"], Is.EqualTo(3));
        }

        [Test]
        public void ResetAllWithoutArgumentsInAProjectWithoutVariablesChangesNothing()
        {
            var json = ProjectJson.Substring(0, ProjectJson.IndexOf(@"""variables"": {")) + @"""variables"": {}, ""notes"": {}, ""assets"": {} }";
            var project = new ProjectMaker(json, null).MakeProject();
            project.Initialize();

            var output = new AwInterpreter(project).RunScript("<pre><code>resetAll()</code></pre>");

            Assert.That(output.Changes, Is.Empty);
        }

        ///<summary>A project with global and board variables that all differ from their default values.</summary>
        private static Project.Project MakeChangedProject()
        {
            var project = new ProjectMaker(ProjectJson, null).MakeProject();
            project.Initialize();
            project.GetVariable("health").Value = 40;
            project.GetVariable("name").Value = "villain";
            project.Boards.Single().Variables.Single().Value = 9;
            return project;
        }

        private const string ProjectJson = @"{
            ""name"": ""Reset All Test"",
            ""startingElement"": ""start"",
            ""boards"": { ""main"": { ""name"": ""Main"", ""customId"": ""main"", ""elements"": [""start""] } },
            ""elements"": {
                ""start"": { ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": ""<pre><code>resetAll()</code></pre>"", ""theme"": ""default"" }
            },
            ""branches"": {},
            ""jumpers"": {},
            ""conditions"": {},
            ""connections"": {},
            ""components"": {},
            ""attributes"": {},
            ""variables"": {
                ""health-id"": { ""name"": ""health"", ""type"": ""integer"", ""value"": 100 },
                ""name-id"": { ""name"": ""name"", ""type"": ""string"", ""value"": ""hero"" },
                ""gold-id"": { ""name"": ""gold"", ""type"": ""integer"", ""value"": 5, ""cType"": ""boards"", ""cId"": ""main"" }
            },
            ""notes"": {},
            ""assets"": {}
        }";
    }
}
