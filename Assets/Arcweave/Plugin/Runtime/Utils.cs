using System;
using System.Linq;

namespace Arcweave
{
    public class Utils
    {
        private static readonly Random _getrandom = new Random();

        // Returns the absolute of a number
        public static double Abs(double n) {
            return Math.Abs(n);
        }

        // Returns the square of a number
        public static double Sqr(double n) {
            return n * n;
        }

        // Returns the square root of a number
        public static double Sqrt(double n) {
            return Math.Sqrt(n);
        }

        // Returns a random decimal between (and including) 0 and (excluding) 1, i.e. in [0, 1)
        public static double Random() {
            lock ( _getrandom ) {
                return _getrandom.NextDouble();
            }
        }

        // Returns a roll of an (n) number of (m)-sided dice
        public static int Roll(int maxRoll, int numRolls = 1) {
            int sum = 0;
            for ( int i = 0; i < maxRoll; i++ ) {
                int oneRoll = _getrandom.Next(1, maxRoll + 1);
                sum += oneRoll;
            }
            return sum;
        }

        // Retuns a number rounded to the nearest integer
        public static int Round(double n) {
            return (int)Math.Round(n);
        }

        public static double Min(params double[] list) {
            return list.Min();
        }

        public static double Max(params double[] list) {
            return list.Max();
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public static string CleanString(string s) {
            if ( !string.IsNullOrEmpty(s) ) {
                s = s.Replace("<strong>", "{bold}").Replace("</strong>", "{/bold}");
                s = s.Replace("<em>", "{italic}").Replace("</em>", "{/italic}");
                s = s.Replace("&lt;", string.Empty).Replace("&gt;", string.Empty);
                s = s.Replace("</p>", "\n\n");
                s = System.Text.RegularExpressions.Regex.Replace(s, @"<[^>]*>", string.Empty);
                s = s.Replace("{bold}", "<b>").Replace("{/bold}", "</b>");
                s = s.Replace("{italic}", "<i>").Replace("{/italic}", "</i>");
                s = s.TrimEnd();
                return s;
            }
            return s;
        }
    }
}