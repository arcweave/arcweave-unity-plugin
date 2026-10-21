using System.Collections;
using System.Linq;
using Arcweave.Interpreter;
using Arcweave.Project;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>Arcweave rich text is converted to the tags the project's text component supports.
    /// Legacy Text, the default, keeps bold and italic. TextMeshPro also keeps underline and strikethrough.</summary>
    public class RichTextTests
    {
        private const string Code = "<color=#27b7f5>";

        [TestCase("<p>Hello</p>", "Hello", TestName = "Paragraph")]
        [TestCase("<p>One</p><p>Two<br>Three</p>", "One\n\nTwo\nThree", TestName = "ParagraphsAndLineBreaks")]
        [TestCase("<p><strong>Bold</strong> and <em>italic</em></p>", "<b>Bold</b> and <i>italic</i>", TestName = "StrongAndEm")]
        [TestCase("<p><b>Bold</b> and <i>italic</i></p>", "<b>Bold</b> and <i>italic</i>", TestName = "BAndI")]
        [TestCase("<p><u>Under</u> and <s>strike</s></p>", "Under and strike", TestName = "UnderlineAndStrikethroughAreRemoved")]
        [TestCase("<p><strong><em><u><s>All</s></u></em></strong></p>", "<b><i>All</i></b>", TestName = "NestedMarks")]
        [TestCase("<pre><code>x = 1</code></pre><p>After</p>", Code + "x = 1</color>\nAfter", TestName = "Code")]
        [TestCase("<p>Hi <span class=\"mention\" data-id=\"1\">Hero</span></p>", "Hi Hero", TestName = "Mention")]
        [TestCase("<p>a &lt; b &amp;&amp; c &gt; d &quot;q&quot; &apos;s&apos;&nbsp;!</p>", "a < b && c > d \"q\" 's' !", TestName = "Entities")]
        [TestCase("<p>&amp;lt;u&amp;gt;</p>", "&lt;u&gt;", TestName = "EscapedEntity")]
        [TestCase("<p>Typed &lt;u&gt;tag&lt;/u&gt;</p>", "Typed <u>tag</u>", TestName = "TypedTagIsDecoded")]
        [TestCase("<p>Text {bold}not bold{/bold}</p>", "Text {bold}not bold{/bold}", TestName = "BracesAreText")]
        [TestCase("", "", TestName = "Empty")]
        [TestCase(null, null, TestName = "Null")]
        public void LegacyText(string html, string expected)
        {
            Assert.That(Utils.CleanString(html), Is.EqualTo(expected));
            Assert.That(Utils.CleanString(html, RichTextTarget.LegacyText), Is.EqualTo(expected));
        }

        [TestCase("<p>Hello</p>", "Hello", TestName = "Paragraph")]
        [TestCase("<p><strong>Bold</strong> and <em>italic</em></p>", "<b>Bold</b> and <i>italic</i>", TestName = "StrongAndEm")]
        [TestCase("<p><b>Bold</b> and <i>italic</i></p>", "<b>Bold</b> and <i>italic</i>", TestName = "BAndI")]
        [TestCase("<p><u>Under</u> and <s>strike</s></p>", "<u>Under</u> and <s>strike</s>", TestName = "UnderlineAndStrikethrough")]
        [TestCase("<p><strong><em><u><s>All</s></u></em></strong></p>", "<b><i><u><s>All</s></u></i></b>", TestName = "NestedMarks")]
        [TestCase("<pre><code>x = 1</code></pre><p>After</p>", Code + "x = 1</color>\nAfter", TestName = "Code")]
        [TestCase("<p>Hi <span class=\"mention\" data-id=\"1\">Hero</span></p>", "Hi Hero", TestName = "Mention")]
        [TestCase("<p>a &lt; b &gt; c</p>", "a <noparse><</noparse> b > c", TestName = "LessThanIsText")]
        [TestCase("<p>Typed &lt;u&gt;tag&lt;/u&gt;</p>", "Typed <noparse><</noparse>u>tag<noparse><</noparse>/u>", TestName = "TypedTagIsText")]
        [TestCase("<p>&amp;lt;u&amp;gt;</p>", "&lt;u&gt;", TestName = "EscapedEntity")]
        [TestCase(null, null, TestName = "Null")]
        public void TextMeshPro(string html, string expected)
        {
            Assert.That(Utils.CleanString(html, RichTextTarget.TextMeshPro), Is.EqualTo(expected));
        }

        [Test]
        public void ProjectsDefaultToLegacyText()
        {
            Assert.That(ScriptableObject.CreateInstance<ArcweaveProjectAsset>().RichTextTarget, Is.EqualTo(RichTextTarget.LegacyText));
            Assert.That(MakeProject().RichTextTarget, Is.EqualTo(RichTextTarget.LegacyText));
        }

        [Test]
        public void ContentLabelsAndAttributesUseTheProjectTarget()
        {
            var project = MakeProject();
            var start = project.StartingElement;

            start.RunContentScript();
            var connection = start.Outputs.Single();
            connection.RunLabelScript();
            Assert.That(start.RuntimeContent, Is.EqualTo("<b>Bold</b> under strike"));
            Assert.That(connection.RuntimeLabel, Is.EqualTo("Go under"));
            Assert.That(start.Attributes.Single().data, Is.EqualTo("Note under"));
            Assert.That(start.GetOptions().Paths.Single().text, Is.EqualTo("Go under"));

            project.RichTextTarget = RichTextTarget.TextMeshPro;
            start.RunContentScript();
            connection.RunLabelScript();
            Assert.That(start.RuntimeContent, Is.EqualTo("<b>Bold</b> <u>under</u> <s>strike</s>"));
            Assert.That(connection.RuntimeLabel, Is.EqualTo("Go <u>under</u>"));
            Assert.That(start.Attributes.Single().data, Is.EqualTo("Note <u>under</u>"));
        }

        [Test]
        public void TitlesOfPathsUseTheProjectTarget()
        {
            var project = MakeProject(label: "");
            project.RichTextTarget = RichTextTarget.TextMeshPro;
            var path = project.StartingElement.GetOptions().Paths.Single();
            Assert.That(path.text, Is.EqualTo("<s>End</s>"));
        }

        [UnityTest]
        public IEnumerator AssetAppliesItsTargetToTheProject()
        {
            var asset = ScriptableObject.CreateInstance<ArcweaveProjectAsset>();
            try {
                asset.RichTextTarget = RichTextTarget.TextMeshPro;
                asset.importSource = ArcweaveProjectAsset.ImportSource.FromJson;
                asset.projectJsonFile = new TextAsset(ProjectJson("Go <u>under</u>"));
                var callbacks = 0;
                string error = null;
                asset.ImportProject(() => callbacks++, e => { callbacks++; error = e; });
                yield return WaitUntil(() => callbacks > 0);

                Assert.That(error, Is.Null);
                Assert.That(asset.Project.RichTextTarget, Is.EqualTo(RichTextTarget.TextMeshPro));

                // Changing the target applies to the imported project without a new import
                asset.RichTextTarget = RichTextTarget.LegacyText;
                Assert.That(asset.Project.RichTextTarget, Is.EqualTo(RichTextTarget.LegacyText));
            }
            finally {
                Object.DestroyImmediate(asset.projectJsonFile);
                Object.DestroyImmediate(asset);
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            var timeout = System.DateTime.Now.AddSeconds(30);
            while ( !condition() ) {
                Assert.That(System.DateTime.Now, Is.LessThan(timeout), "Timed out waiting for the import");
                yield return null;
            }
        }

        private static Project.Project MakeProject(string label = "Go <u>under</u>")
        {
            var project = new ProjectMaker(ProjectJson(label), null).MakeProject();
            project.Initialize();
            return project;
        }

        ///<summary>A project whose starting element has rich text content and a rich text attribute, and connects to an ending element.</summary>
        private static string ProjectJson(string label)
        {
            return $@"{{ ""project"": {{
                ""name"": ""Rich Text Test"",
                ""startingElement"": ""start"",
                ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start"", ""end""], ""connections"": [""go""] }} }},
                ""elements"": {{
                    ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""theme"": ""default"", ""outputs"": [""go""], ""attributes"": [""note""],
                        ""content"": ""<p><strong>Bold</strong> <u>under</u> <s>strike</s></p>"" }},
                    ""end"": {{ ""x"": 200, ""y"": 0, ""title"": ""<p><s>End</s></p>"", ""content"": """", ""theme"": ""default"" }}
                }},
                ""branches"": {{}},
                ""jumpers"": {{}},
                ""conditions"": {{}},
                ""connections"": {{ ""go"": {{ ""label"": ""{(string.IsNullOrEmpty(label) ? "" : "<p>" + label + "</p>")}"", ""sourceid"": ""start"", ""targetid"": ""end"", ""sourceType"": ""elements"", ""targetType"": ""elements"" }} }},
                ""components"": {{}},
                ""attributes"": {{ ""note"": {{ ""name"": ""Note"", ""cType"": ""elements"", ""cId"": ""start"", ""value"": {{ ""type"": ""string"", ""data"": ""<p>Note <u>under</u></p>"" }} }} }},
                ""variables"": {{}},
                ""notes"": {{}},
                ""assets"": {{}}
            }} }}";
        }
    }
}
