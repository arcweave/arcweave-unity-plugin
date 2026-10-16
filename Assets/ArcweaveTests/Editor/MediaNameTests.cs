using System.Reflection;
using NUnit.Framework;

namespace Arcweave.Tests
{
    ///<summary>Checks how the names of Arcweave assets and folders are made safe to use as the names of downloaded media files and folders.</summary>
    public class MediaNameTests
    {
        [TestCase("monsignor.jpeg", "monsignor.jpeg")]
        [TestCase("Act 1: Cover.png", "Act 1_ Cover.png")]
        [TestCase("a<b>c\"d/e\\f|g?h*i.png", "a_b_c_d_e_f_g_h_i.png")]
        [TestCase("tab\there\u0001.png", "tab_here_.png")]
        [TestCase("Images. . ", "Images")]
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
        [TestCase("Backup~", "Backup~_")]
        [TestCase("cvs", "cvs_")]
        [TestCase("Draft.tmp", "Draft.tmp_")]
        public void NameIsSanitized(string name, string expected)
        {
            var sanitize = typeof(ArcweaveProjectAsset).Assembly.GetType("Arcweave.ArcweaveMediaDownloader")
                .GetMethod("SanitizeName", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(sanitize.Invoke(null, new object[] { name }), Is.EqualTo(expected));
        }
    }
}
