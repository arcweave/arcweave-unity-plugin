#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text;

namespace Arcweave
{
    ///<summary>The rules that turn the names of Arcweave assets into names of downloaded media files that are safe on every platform.</summary>
    internal static class ArcweaveMediaNames
    {
        private static readonly HashSet<string> RESERVED_NAMES = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            // Windows also reserves these with superscript digits
            "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³",
        };
        private const string INVALID_NAME_CHARS = "<>:\"/\\|?*";
        // Leaves room for the number of a unique path and for Unity's own paths, eg. in the Library folder.
        // File systems such as ext4 and APFS limit a name to 255 bytes, which must also fit the extension, the number and ".meta".
        internal const int MAX_NAME_LENGTH = 100;
        internal const int MAX_NAME_BYTES = 200;

        ///<summary>Makes a file name safe on every OS and visible to Unity, so it can never leave the media folder,
        /// and shortens it to MAX_NAME_LENGTH characters and MAX_NAME_BYTES UTF-8 bytes before its extension.</summary>
        internal static string SanitizeName(string name) {
            // Composed, so names that only differ in how their characters are written are the same name, eg. in claimed paths
            var builder = new StringBuilder(Normalize(name ?? ""));
            for ( var i = 0; i < builder.Length; i++ ) {
                if ( char.IsControl(builder[i]) || INVALID_NAME_CHARS.IndexOf(builder[i]) >= 0 ) { builder[i] = '_'; }
            }
            // Windows drops trailing dots and spaces, which also turns "." and ".." into ""
            var result = builder.ToString().TrimEnd('.', ' ');

            var extension = System.IO.Path.GetExtension(result);
            var stem = result.Substring(0, result.Length - extension.Length);
            // Making the name safe adds at most one character, so it is shortened by one more if that goes over the limits
            var safeStem = MakeSafe(Shorten(stem, MAX_NAME_LENGTH, MAX_NAME_BYTES));
            if ( safeStem.Length > MAX_NAME_LENGTH || Encoding.UTF8.GetByteCount(safeStem) > MAX_NAME_BYTES ) {
                safeStem = MakeSafe(Shorten(stem, MAX_NAME_LENGTH - 1, MAX_NAME_BYTES - 1));
            }
            return safeStem + extension;
        }

        ///<summary>True if the names are the same file name on some file system: ignoring case and Unicode normalization.</summary>
        internal static bool AreSameName(string name, string other) {
            return string.Equals(Normalize(name), Normalize(other), StringComparison.OrdinalIgnoreCase);
        }

        ///<summary>Returns the composed form of the name, or the name itself if it can't be normalized, eg. with a lone surrogate.</summary>
        internal static string Normalize(string name) {
            try {
                return name.Normalize(NormalizationForm.FormC);
            }
            catch ( ArgumentException ) {
                return name;
            }
        }

        ///<summary>Returns the name if it fits the limits, or else its start that does, without splitting a character made of
        /// two UTF-16 chars or ending with a dot or space.</summary>
        static string Shorten(string name, int maxLength, int maxBytes) {
            var length = Math.Min(name.Length, maxLength);
            while ( length > 0 && ( ( length < name.Length && char.IsHighSurrogate(name[length - 1]) ) || Encoding.UTF8.GetByteCount(name.Substring(0, length)) > maxBytes ) ) {
                length--;
            }
            return length == name.Length ? name : name.Substring(0, length).TrimEnd('.', ' ');
        }

        static string MakeSafe(string stem) {
            // Unity ignores hidden files, whose names start with a dot, eg. ".png"
            if ( stem.Length == 0 || stem[0] == '.' ) { stem = "_" + stem; }

            // Windows reserves device names, even with an extension
            var dot = stem.IndexOf('.');
            var device = dot < 0 ? stem : stem.Substring(0, dot);
            if ( RESERVED_NAMES.Contains(device.TrimEnd(' ')) ) {
                stem = device + "_" + stem.Substring(device.Length);
            }
            return stem;
        }
    }
}

#endif
