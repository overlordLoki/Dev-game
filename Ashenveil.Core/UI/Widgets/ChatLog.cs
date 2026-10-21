using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ashenveil.Core.UI.Widgets
{
    /// <summary>
    /// A list of messages drawn newest at the bottom, working upwards until the box is full;
    /// older ones simply fall off the top, so there's no scroll position to keep. Each entry
    /// can start with a coloured name ("Sir Lancelot:") and is word-wrapped to the box width.
    /// Shared by the chat box (what was said) and the dev console (commands and their output).
    /// </summary>
    public class ChatLog : Widget
    {
        private const int EntryGap = 6;

        // One message, plus its word-wrapped lines. Wrapping needs the font, which only
        // arrives in Draw, so it's done there - once, then kept.
        private sealed class Entry
        {
            public string Name, Text;
            public Color NameColor, TextColor;
            public List<string> Wrapped;
        }

        private readonly List<Entry> _entries = new();
        private readonly int _maxEntries;

        /// <param name="maxEntries">Oldest entries are dropped past this many. Unlimited by default.</param>
        public ChatLog(Rectangle bounds, int maxEntries = int.MaxValue) : base(bounds) => _maxEntries = maxEntries;

        /// <summary>How many entries are kept.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// One extra entry drawn under the newest, without being kept - it can change every frame
        /// (the chat box's "Sir Lancelot: ..." while the LLM writes). Null for none.
        /// </summary>
        public (string Name, Color NameColor, string Text, Color TextColor)? Pending { get; set; }

        /// <summary>Adds an entry: <paramref name="text"/>, optionally after a coloured "name:".</summary>
        public void Add(string text, Color color, string name = null, Color nameColor = default)
        {
            _entries.Add(new Entry
            {
                Name = name == null ? null : Printable(name),
                NameColor = nameColor,
                Text = Printable(text),
                TextColor = color,
            });
            if (_entries.Count > _maxEntries) _entries.RemoveAt(0);
        }

        public void Clear() => _entries.Clear();

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Visible) return;
            float y = Bounds.Bottom;

            if (Pending is { } p)
            {
                // Re-wrapped every frame: it's one short line and it changes as it goes.
                var lines = Wrap(font, p.Name == null ? null : Printable(p.Name), Printable(p.Text));
                y -= lines.Count * font.LineSpacing;
                DrawLines(spriteBatch, font, lines, y, p.Name, p.NameColor, p.TextColor);
                y -= EntryGap;
            }

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                e.Wrapped ??= Wrap(font, e.Name, e.Text);
                y -= e.Wrapped.Count * font.LineSpacing;
                if (y < Bounds.Top) break;

                DrawLines(spriteBatch, font, e.Wrapped, y, e.Name, e.NameColor, e.TextColor);
                y -= EntryGap;
            }
        }

        private List<string> Wrap(SpriteFont font, string name, string text) =>
            UIDraw.WrapText(font, name == null ? text : $"{name}: {text}", Bounds.Width);

        // The name is coloured and the rest of the line isn't, so the first line is drawn in
        // two pieces. An entry with no name is all one colour.
        private void DrawLines(SpriteBatch spriteBatch, SpriteFont font, List<string> lines, float y,
                               string name, Color nameColor, Color textColor)
        {
            string prefix = name == null ? "" : name + ":";
            for (int l = 0; l < lines.Count; l++)
            {
                var pos = new Vector2(Bounds.X, (int)(y + l * font.LineSpacing));
                if (l == 0 && prefix.Length > 0 && lines[0].StartsWith(prefix))
                {
                    spriteBatch.DrawString(font, prefix, pos, nameColor);
                    pos.X = (int)(pos.X + font.MeasureString(prefix).X);
                    spriteBatch.DrawString(font, lines[0].Substring(prefix.Length), pos, textColor);
                }
                else spriteBatch.DrawString(font, lines[l], pos, textColor);
            }
        }

        /// <summary>
        /// The LLM writes whatever it likes; the font only has so many glyphs. Swaps the usual
        /// suspects (curly quotes, long dashes, the ellipsis) for plain ones that read better
        /// than the font's DefaultCharacter '?'. Everything else passes through.
        /// </summary>
        public static string Printable(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = raw switch
                {
                    '‘' or '’' => '\'',
                    '“' or '”' => '"',
                    '–' or '—' => '-',
                    _ => raw,
                };
                if (c == '…') sb.Append("...");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
