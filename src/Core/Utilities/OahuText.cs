#nullable enable
using Bit.App.Contracts;

namespace Bit.App.Utilities
{
    /// <summary>Text helpers shared by the OAHU contract halves.</summary>
    public static class OahuText
    {
        /// <summary>
        /// The runs GeneratedValueFormatter colors (letters, digits, everything else), so a native screen colors a
        /// password or username the way the MAUI pages' HTML labels did, without HTML.
        /// </summary>
        public static IReadOnlyList<TextRun> Runs(string? value)
        {
            var runs = new List<TextRun>();
            if (string.IsNullOrEmpty(value))
            {
                return runs;
            }
            var start = 0;
            TextRunKind? current = null;
            for (var i = 0; i < value.Length; i++)
            {
                var kind = char.IsLetter(value[i]) ? TextRunKind.Letters
                    : char.IsDigit(value[i]) ? TextRunKind.Digits : TextRunKind.Symbols;
                if (current != null && kind != current)
                {
                    runs.Add(new TextRun(value[start..i], current.Value));
                    start = i;
                }
                current = kind;
            }
            if (current != null)
            {
                runs.Add(new TextRun(value[start..], current.Value));
            }
            return runs;
        }
    }
}
