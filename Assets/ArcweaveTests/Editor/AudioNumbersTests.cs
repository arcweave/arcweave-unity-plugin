using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Arcweave.Project;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arcweave.Tests
{
    ///<summary>Arcweave exports may write the delay of element audio as a string (eg. "delay": "1200" in The Castle template).
    /// Checks that audio delays and volumes are imported from numbers and numeric strings, and that other values don't fail the import.</summary>
    public class AudioNumbersTests
    {
        [TestCase("1200", 1200f, TestName = "IntegerDelay")]
        [TestCase("0", 0f, TestName = "ZeroDelay")]
        [TestCase("-5", -5f, TestName = "NegativeIntegerDelay")]
        [TestCase("9007199254740993", (float)9007199254740993L, TestName = "LargeIntegerDelay")]
        [TestCase("1.5", 1.5f, TestName = "DecimalDelay")]
        [TestCase("\"1200\"", 1200f, TestName = "IntegerStringDelay")]
        [TestCase("\"2.5\"", 2.5f, TestName = "DecimalStringDelay")]
        [TestCase("null", 0f, TestName = "NullDelay")]
        [TestCase(null, 0f, TestName = "MissingDelay")]
        public void DelayIsImported(string delay, float expected)
        {
            var audio = MakeAudio(delay, volume: null);
            Assert.That(audio.delay, Is.EqualTo(expected));
            Assert.That(audio.volume, Is.EqualTo(1f));
        }

        [TestCase("0.71", 0.71f, TestName = "DecimalVolume")]
        [TestCase("2", 2f, TestName = "IntegerVolume")]
        [TestCase("0", 0f, TestName = "ZeroVolume")]
        [TestCase("\"0.5\"", 0.5f, TestName = "StringVolume")]
        [TestCase("null", 1f, TestName = "NullVolume")]
        [TestCase(null, 1f, TestName = "MissingVolume")]
        public void VolumeIsImported(string volume, float expected)
        {
            Assert.That(MakeAudio("0", volume).volume, Is.EqualTo(expected));
        }

        // Numeric strings are always read with the invariant culture, eg. "2.5" is not 25 where ',' is the decimal separator
        [Test]
        public void NumericStringsIgnoreTheCurrentCulture()
        {
            var culture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var audio = MakeAudio("\"2.5\"", "\"0.5\"");
                Assert.That(audio.delay, Is.EqualTo(2.5f));
                Assert.That(audio.volume, Is.EqualTo(0.5f));
            }
            finally {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestCase("\"soon\"", TestName = "TextDelay")]
        [TestCase("\"NaN\"", TestName = "NaNStringDelay")]
        [TestCase("\"Infinity\"", TestName = "InfinityStringDelay")]
        [TestCase("true", TestName = "BooleanDelay")]
        public void DelayThatIsNotANumberIsReportedAndIgnored(string delay)
        {
            LogAssert.Expect(LogType.Warning, new Regex("delay of audio song-1, which is not a number"));
            Assert.That(MakeAudio(delay, volume: null).delay, Is.EqualTo(0f));
        }

        [Test]
        public void VolumeThatIsNotANumberIsReportedAndIgnored()
        {
            LogAssert.Expect(LogType.Warning, new Regex("volume of audio asset song, which is not a number"));
            Assert.That(MakeAudio("0", "\"loud\"").volume, Is.EqualTo(1f));
        }

        private static AudioAsset MakeAudio(string delay, string volume)
        {
            var project = new ProjectMaker(ProjectJson(delay, volume), null).MakeProject();
            var audio = project.StartingElement.AudioAssets.Single();
            Assert.That(audio.id, Is.EqualTo("song-1"));
            Assert.That(audio.name, Is.EqualTo("song.mp3"));
            Assert.That(audio.mode, Is.EqualTo(AudioAsset.Mode.Once));
            return audio;
        }

        ///<summary>A project whose starting element plays one audio asset, in the Unity export's {"project": ...} envelope.
        /// A null delay or volume omits the property.</summary>
        private static string ProjectJson(string delay, string volume)
        {
            var delayProperty = delay == null ? "" : $@", ""delay"": {delay}";
            var volumeProperty = volume == null ? "" : $@", ""volume"": {volume}";
            return $@"{{ ""project"": {{
                ""name"": ""Audio Numbers Test"",
                ""startingElement"": ""start"",
                ""boards"": {{ ""main"": {{ ""name"": ""Main"", ""elements"": [""start""] }} }},
                ""elements"": {{
                    ""start"": {{ ""x"": 0, ""y"": 0, ""title"": ""Start"", ""content"": """", ""theme"": ""default"",
                        ""assets"": {{ ""audio"": [{{ ""id"": ""song-1"", ""mode"": ""once"", ""asset"": ""song""{delayProperty} }}] }} }}
                }},
                ""branches"": {{}},
                ""jumpers"": {{}},
                ""conditions"": {{}},
                ""connections"": {{}},
                ""components"": {{}},
                ""attributes"": {{}},
                ""variables"": {{}},
                ""notes"": {{}},
                ""assets"": {{ ""song"": {{ ""name"": ""song.mp3"", ""type"": ""audio""{volumeProperty} }} }}
            }} }}";
        }
    }
}
