using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcweave.Interpreter;
using Arcweave.Interpreter.INodes;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AWProject = Arcweave.Project.Project;
using AWComponent = Arcweave.Project.Component;

namespace Arcweave.Tests
{
    public class GlobalVariablesUpgradeTests
    {
        private const string FixtureGuid = "4aa480916f3d46ad8c34694108227c26";
        private string temporaryFolder;
        private string assetPath;
        private ArcweaveProjectAsset asset;

        [SetUp]
        public void LoadAssetSerializedBeforeTheRename()
        {
            var fixturePath = AssetDatabase.GUIDToAssetPath(FixtureGuid);
            Assert.That(fixturePath, Is.Not.Empty, "The legacy serialization fixture is missing.");

            var folderName = "ArcweaveUpgradeTest_" + Guid.NewGuid().ToString("N");
            temporaryFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
            assetPath = temporaryFolder + "/Project.asset";

            // Import the old bytes on every run; never let Unity rewrite the source fixture.
            File.Copy(fixturePath, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(assetPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Project, Is.Not.Null);
        }

        [TearDown]
        public void DeleteTemporaryAsset()
        {
            if (asset != null) Resources.UnloadAsset(asset);
            if (!string.IsNullOrEmpty(temporaryFolder)) AssetDatabase.DeleteAsset(temporaryFolder);
        }

        [Test]
        public void UpgradedAssetPreservesVariablesAndTheirScopes()
        {
            var project = asset.Project;
            Assert.That(project.GlobalVariables.Select(variable => variable.Id), Is.EqualTo(new[] { "coins-id" }));
            Assert.That(project.GetAllVariables().Select(variable => variable.Id),
                Is.EquivalentTo(new[] { "coins-id", "board-health", "hero-health" }));
            AssertValues(project, 7, 11, 13);
            Assert.That(project.GetVariable("coins").Parent, Is.Null);
            Assert.That(project.GetVariable("health", "intro").Parent, Is.SameAs(project.Boards.Single()));
            Assert.That(project.GetVariable("health", "hero").Parent, Is.SameAs(project.Components.Single()));
        }

        [TestCase("coins == 7")]
        [TestCase("intro.health == 11")]
        [TestCase("hero.health == 13")]
        public void InterpreterResolvesUpgradedVariables(string condition)
        {
            var output = new AwInterpreter(asset.Project, "start").RunScript("<pre><code>" + condition + "</code></pre>");
            Assert.That(output.IsCondition, Is.True);
            Assert.That(output.Result, Is.EqualTo(true));
        }

        [Test]
        public void UpgradedVariablesRetainSaveLoadAndResetBehavior()
        {
            var project = asset.Project;
            Assert.That(project.SetVariable("coins", 17), Is.True);
            Assert.That(project.SetVariable("health", "intro", 21), Is.True);
            Assert.That(project.SetVariable("health", "hero", 23), Is.True);
            var save = project.SaveVariables();
            StringAssert.Contains("\"id\":\"coins-id\"", save);
            StringAssert.Contains("\"id\":\"board-health\"", save);
            StringAssert.Contains("\"id\":\"hero-health\"", save);

            project.ResetVariablesToDefaultValues();
            AssertValues(project, 7, 11, 13);
            project.LoadVariables(save);
            AssertValues(project, 17, 21, 23);
        }

        [Test]
        public void UpgradedAssetCanBeSavedAndReloaded()
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Resources.UnloadAsset(asset);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(assetPath);
            AssertValues(asset.Project, 7, 11, 13);
            Assert.That(asset.Project.GetAllVariables().Count(), Is.EqualTo(3));
        }

        [Test]
        public void NewProjectsExposeGlobalsThroughBothPublicNames()
        {
            var globals = new List<Variable> { new Variable("fresh-id", "score", 5) };
            var project = new AWProject("Fresh", new Element(), new List<Board>(), new List<AWComponent>(), globals);
            Assert.That(project.GlobalVariables, Is.SameAs(globals));
#pragma warning disable CS0618 // Compile an existing consumer's use of the deprecated API.
            Assert.That(project.Variables, Is.SameAs(globals));
#pragma warning restore CS0618
            Assert.That(((IProject)project).Variables, Is.SameAs(globals));
            Assert.That(project.GetVariable("score").Value, Is.EqualTo(5));
            Assert.That(new AwInterpreter(project).RunScript("<pre><code>score == 5</code></pre>").Result, Is.EqualTo(true));
        }

        [Test]
        public void ExistingIProjectImplementationsRemainUsableByTheInterpreter()
        {
            IProject legacy = new LegacyProject(asset.Project);
            Assert.That(legacy.Variables, Is.SameAs(asset.Project.GlobalVariables));
            Assert.That(new AwInterpreter(legacy).RunScript("<pre><code>coins == 7</code></pre>").Result, Is.EqualTo(true));
        }

        private static void AssertValues(AWProject project, int coins, int boardHealth, int componentHealth)
        {
            Assert.That(project.GetVariable("coins")?.Value, Is.EqualTo(coins));
            Assert.That(project.GetVariable("health", "intro")?.Value, Is.EqualTo(boardHealth));
            Assert.That(project.GetVariable("health", "hero")?.Value, Is.EqualTo(componentHealth));
        }

        // Deliberately implements only the pre-rename interface, with no GlobalVariables member.
        private sealed class LegacyProject : IProject
        {
            private readonly AWProject project;
            public LegacyProject(AWProject project) { this.project = project; }
            public List<Variable> Variables => project.GlobalVariables;
            public List<Board> Boards => project.Boards;
            public List<AWComponent> Components => project.Components;
            public IEnumerable<Variable> GetAllVariables() => project.GetAllVariables();
            public Element ElementWithId(string id) => project.ElementWithId(id);
            public Variable GetVariable(string name, string scope = null) => project.GetVariable(name, scope);
        }
    }
}
