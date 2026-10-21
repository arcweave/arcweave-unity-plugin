using System.Text.RegularExpressions;

namespace Arcweave.Interpreter
{
    ///<summary>The Unity text component that Arcweave rich text is converted for.</summary>
    public enum RichTextTarget
    {
        ///<summary>Legacy uGUI Text and IMGUI. Only bold and italic are kept. Underline and strikethrough show as plain text,
        /// since these components display the &lt;u&gt; and &lt;s&gt; tags literally.</summary>
        LegacyText = 0,
        ///<summary>TextMeshPro and UI Toolkit. Also keeps underline and strikethrough, and shows a '&lt;' typed by the writer
        /// as text instead of the start of a tag.</summary>
        TextMeshPro = 1,
    }

    public class Utils
    {
        public static readonly string CODE_HEX_COLOR = "#27b7f5";

        private static readonly Regex TagRegex = new Regex(@"<[^>]*>");

        ///<summary>Converts Arcweave html to Unity rich text for legacy uGUI Text and IMGUI.</summary>
        public static string CleanString(string s) => CleanString(s, RichTextTarget.LegacyText);

        ///<summary>Converts Arcweave html to Unity rich text for the given text component.</summary>
        public static string CleanString(string s, RichTextTarget target)
        {
            if (!string.IsNullOrEmpty(s))
            {
                // Tags are mapped in a single pass, so text can't be mistaken for a tag it produces
                s = TagRegex.Replace(s, m => ConvertTag(m.Value, target));
                // In TextMeshPro an escaped '<' would start a tag, eg. a writer typing "<u>", so it is kept as text
                s = s.Replace("&lt;", target == RichTextTarget.TextMeshPro ? "<noparse><</noparse>" : "<").Replace("&gt;", ">");
                s = s.Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&nbsp;", " ").Replace("&apos;", "'");
                s = s.TrimEnd();
                return s;
            }
            return s;
        }

        private static string ConvertTag(string tag, RichTextTarget target)
        {
            switch (tag)
            {
                case "<strong>": case "<b>": return "<b>";
                case "</strong>": case "</b>": return "</b>";
                case "<em>": case "<i>": return "<i>";
                case "</em>": case "</i>": return "</i>";
                case "</p>": return "\n\n";
                case "<br>": return "\n";
                case "<code>": return string.Format("<color={0}>", CODE_HEX_COLOR);
                case "</code>": return "</color>\n";
            }
            if (target == RichTextTarget.TextMeshPro)
            {
                switch (tag)
                {
                    case "<u>": return "<u>";
                    case "</u>": return "</u>";
                    case "<s>": return "<s>";
                    case "</s>": return "</s>";
                }
            }
            return string.Empty;
        }
    }
}
