using System;
using System.Linq;
using Arcweave.Interpreter;
using Arcweave.Interpreter.INodes;
using Arcweave.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ArcweaveProject = Arcweave.Project.Project;

namespace Arcweave.Tests.Editor
{
    public class ComponentBoardVariableTests
    {
        private const string FixturePath = "Assets/Arcweave/Tests/Editor/Fixtures/component-board-variables.json";

        [Test]
        public void ImportsAttributeBackedBoardAndComponentVariables()
        {
            var project = MakeProject();
            var board = project.Boards.Single();
            var component = project.Components.Single();

            Assert.That(project.Variables.Select(variable => variable.Id), Is.EqualTo(new[] { "global-health" }));
            Assert.That(board.CustomId, Is.EqualTo("castle"));
            Assert.That(component.CustomId, Is.EqualTo("hero"));
            Assert.That(board.Variables.Count, Is.EqualTo(4));
            Assert.That(component.Variables.Count, Is.EqualTo(2));
            Assert.That(project.GetAllVariables().Count(), Is.EqualTo(7));

            AssertVariable(project.GetVariable("health"), "global-health", 100, null);
            AssertVariable(project.GetVariable("health", "castle"), "board-health", 10, board);
            AssertVariable(project.GetVariable("health", "hero"), "component-health", 20, component);
            AssertVariable(project.GetVariable("is_open", "castle"), "board-open", true, board);
            AssertVariable(project.GetVariable("rate", "castle"), "board-rate", 1.5d, board);
            AssertVariable(project.GetVariable("title", "castle"), "board-title", string.Empty, board);
            AssertVariable(project.GetVariable("label", "hero"), "component-label", string.Empty, component);

            Assert.That(project.GetVariable("summary", "castle"), Is.Null);
            Assert.That(board.Variables.Any(variable => variable.Id == "board-ordinary"), Is.False);
        }

        [Test]
        public void InterpreterKeepsGlobalBoardAndComponentNamesDistinct()
        {
            var project = MakeProject();
            var output = new AwInterpreter(project).RunScript(
                "<pre><code>health = 101</code></pre>" +
                "<pre><code>castle.health = 11</code></pre>" +
                "<pre><code>hero.health = 21</code></pre>"
            );

            Assert.That(output.Changes["global-health"], Is.EqualTo(101));
            Assert.That(output.Changes["board-health"], Is.EqualTo(11));
            Assert.That(output.Changes["component-health"], Is.EqualTo(21));

            var resetAll = new AwInterpreter(project).RunScript(
                "<pre><code>resetAll(hero.health)</code></pre>"
            );
            Assert.That(resetAll.Changes.ContainsKey("global-health"), Is.True);
            Assert.That(resetAll.Changes.ContainsKey("board-health"), Is.True);
            Assert.That(resetAll.Changes.ContainsKey("component-health"), Is.False);
        }

        [Test]
        public void InitializeAndSaveLoadCoverEveryScope()
        {
            var project = MakeProject();
            project.Initialize();

            var richTextAttribute = project.Boards.Single().Attributes.Single(attribute => attribute.Id == "board-rich");
            Assert.That(richTextAttribute.data, Is.EqualTo("10"));

            project.SetVariable("health", 101);
            project.SetVariable("health", "castle", 11);
            project.SetVariable("health", "hero", 21);
            var save = project.SaveVariables();

            project.ResetVariablesToDefaultValues();
            project.LoadVariables(save);

            Assert.That(project.GetVariable("health").Value, Is.EqualTo(101));
            Assert.That(project.GetVariable("health", "castle").Value, Is.EqualTo(11));
            Assert.That(project.GetVariable("health", "hero").Value, Is.EqualTo(21));
        }

        [Test]
        public void VariableSerializationKeepsCurrentAndDefaultValuesDistinct()
        {
            AssertSerializationRoundTrip("string", "default", "current");
            AssertSerializationRoundTrip("integer", 10, 20);
            AssertSerializationRoundTrip("float", 1.5d, 2.5d);
            AssertSerializationRoundTrip("boolean", false, true);
        }

        [Test]
        public void VariableRejectsNullValueAndEnumsRemainSerializationCompatible()
        {
            Assert.Throws<ArgumentNullException>(() => new Variable("id", "name", null));

            Assert.That((int)IAttribute.DataType.AssetList, Is.EqualTo(4));
            Assert.That((int)IAttribute.DataType.Boolean, Is.EqualTo(5));
            Assert.That((int)IAttribute.DataType.Integer, Is.EqualTo(6));
            Assert.That((int)IAttribute.DataType.Float, Is.EqualTo(7));
            Assert.That((int)IAttribute.ContainerType.Board, Is.EqualTo(3));
        }

        private static ArcweaveProject MakeProject()
        {
            var fixture = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.That(fixture, Is.Not.Null);
            return new ProjectMaker(fixture.text, null).MakeProject();
        }

        private static void AssertVariable(Variable variable, string id, object value, IHasVariables parent)
        {
            Assert.That(variable, Is.Not.Null);
            Assert.That(variable.Id, Is.EqualTo(id));
            Assert.That(variable.Value, Is.EqualTo(value));
            Assert.That(variable.DefaultValue, Is.EqualTo(value));
            Assert.That(variable.Parent, Is.SameAs(parent));
        }

        private static void AssertSerializationRoundTrip(string name, object defaultValue, object currentValue)
        {
            var variable = new Variable(name, name, defaultValue) { Value = currentValue };
            variable.OnBeforeSerialize();
            variable.Value = null;
            variable.DefaultValue = null;
            variable.OnAfterDeserialize();

            Assert.That(variable.Value, Is.EqualTo(currentValue));
            Assert.That(variable.DefaultValue, Is.EqualTo(defaultValue));

            variable.ResetToDefaultValue();
            Assert.That(variable.Value, Is.EqualTo(defaultValue));
        }
    }
}
