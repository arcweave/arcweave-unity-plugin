using System.Reflection;
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

        static string Sanitize(string name)
        {
            var sanitize = typeof(ArcweaveProjectAsset).Assembly.GetType("Arcweave.ArcweaveMediaDownloader")
                .GetMethod("SanitizeName", BindingFlags.Static | BindingFlags.NonPublic);
            return (string)sanitize.Invoke(null, new object[] { name });
        }
    }
}
