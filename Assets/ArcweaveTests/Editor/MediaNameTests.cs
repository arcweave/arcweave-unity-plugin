using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Arcweave.Tests
{
    ///<summary>Checks how the names of Arcweave assets are made safe to use as the names of downloaded media files.</summary>
    public class MediaNameTests
    {
        [TestCase("monsignor.jpeg", "monsignor.jpeg")]
        [TestCase("Act 1: Cover.png", "Act 1_ Cover.png")]
        [TestCase("a<b>c\"d/e\\f|g?h*i.png", "a_b_c_d_e_f_g_h_i.png")]
        [TestCase("tab\there\u0001.png", "tab_here_.png")]
        [TestCase("Cover.png. . ", "Cover.png")]
        [TestCase("", "_")]
        [TestCase(" ", "_")]
        [TestCase(".", "_")]
        [TestCase("..", "_")]
        [TestCase("../..", "_.._")]
        [TestCase("..\\secret.png", "_.._secret.png")]
        [TestCase("CON", "CON_")]
        [TestCase("con.png", "con_.png")]
        [TestCase("Nul.tar.gz", "Nul_.tar.gz")]
        [TestCase("com1.mp3", "com1_.mp3")]
        [TestCase("LPT9", "LPT9_")]
        [TestCase("aux .png", "aux _.png")]
        [TestCase("COM10.png", "COM10.png")]
        [TestCase("CONSOLE.png", "CONSOLE.png")]
        [TestCase(".hidden.png", "_.hidden.png")]
        [TestCase(".png", "_.png")]
        public void NameIsSanitized(string name, string expected)
        {
            Assert.That(Sanitize(name), Is.EqualTo(expected));
        }

        [Test]
        public void LongNameIsShortenedToAHundredCharactersBeforeItsExtension()
        {
            Assert.That(Sanitize(new string('a', 100) + ".png"), Is.EqualTo(new string('a', 100) + ".png"));
            Assert.That(Sanitize(new string('a', 150) + ".png"), Is.EqualTo(new string('a', 100) + ".png"));
            Assert.That(Sanitize(new string('a', 150) + ".png"), Is.EqualTo(Sanitize(new string('a', 150) + ".png")), "Shortening is deterministic.");
        }

        [Test]
        public void ShortenedNameNeverEndsWithADotOrSpaceOrHalfACharacter()
        {
            Assert.That(Sanitize(new string('a', 98) + ". b.png"), Is.EqualTo(new string('a', 98) + ".png"));
            // An emoji is two UTF-16 chars, the first of which would be the 100th
            Assert.That(Sanitize(new string('a', 99) + "\U0001F600.png"), Is.EqualTo(new string('a', 99) + ".png"));
        }

        [Test]
        public void NameIsComposed()
        {
            // "é" written as "e" and a combining accent
            Assert.That(Sanitize("Café.png"), Is.EqualTo("Café.png"));
        }

        [Test]
        public void LongNameWithMultibyteCharactersIsShortenedToTwoHundredBytes()
        {
            // Each of these is 3 bytes in UTF-8, and file systems such as ext4 and APFS limit a name to 255 bytes
            var han = "漢";
            var sanitized = Sanitize(string.Concat(Enumerable.Repeat(han, 150)) + ".png");
            Assert.That(sanitized, Is.EqualTo(string.Concat(Enumerable.Repeat(han, 66)) + ".png"));
        }

        [TestCase(".", 99, "_.", 98)]
        [TestCase("CON.", 96, "CON_.", 95)]
        public void NameMadeSafeAfterShorteningStillFitsTheLimit(string start, int count, string expectedStart, int expectedCount)
        {
            var sanitized = Sanitize(start + new string('a', count) + ".png");
            Assert.That(sanitized, Is.EqualTo(expectedStart + new string('a', expectedCount) + ".png"));
            Assert.That(sanitized.Length - ".png".Length, Is.EqualTo(100));
        }

        [Test]
        public void ReservedNameFollowedBySpacesIsStillMadeSafeAfterShortening()
        {
            Assert.That(Sanitize("CON" + new string(' ', 150) + ".png"), Is.EqualTo("CON_.png"));
        }

        [Test]
        public void SanitizedNamesStayWithinTheLimits([Values(".", "CON", "a", "漢", "\U0001F600")] string start)
        {
            for ( var count = 90; count < 260; count++ ) {
                var stem = System.IO.Path.GetFileNameWithoutExtension(Sanitize(start + string.Concat(Enumerable.Repeat(start == "." ? "a" : start, count)) + ".png"));
                Assert.That(stem.Length, Is.LessThanOrEqualTo(100));
                Assert.That(Encoding.UTF8.GetByteCount(stem), Is.LessThanOrEqualTo(200));
                Assert.That(stem, Does.Not.StartWith(".").And.Not.EndWith(".").And.Not.EndWith(" "));
            }
        }

        static string Sanitize(string name)
        {
            var sanitize = typeof(ArcweaveProjectAsset).Assembly.GetType("Arcweave.ArcweaveMediaDownloader")
                .GetMethod("SanitizeName", BindingFlags.Static | BindingFlags.NonPublic);
            return (string)sanitize.Invoke(null, new object[] { name });
        }
    }
}
