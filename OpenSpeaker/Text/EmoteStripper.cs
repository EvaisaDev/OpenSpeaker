using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace OpenSpeaker.Text;

public class EmoteStripper
{
    private HashSet<string> _twitchEmotes = new();
    private HashSet<string> _bttvEmotes = new();
    private HashSet<string> _ffzEmotes = new();
    private HashSet<string> _sevenTvEmotes = new();
    private static readonly Regex CheermotePattern = new(@"\b[A-Za-z]+\d+\b", RegexOptions.Compiled);
    private static readonly Regex TwemojiPattern = new(@"[©® -㌀\uD83C퀀-\uDFFF\uD83D퀀-\uDFFF\uD83E퀀-\uDFFF]+", RegexOptions.Compiled);

    public void SetTwitchEmotes(IEnumerable<string> emotes) => _twitchEmotes = new HashSet<string>(emotes);
    public void SetBttvEmotes(IEnumerable<string> emotes) => _bttvEmotes = new HashSet<string>(emotes);
    public void SetFfzEmotes(IEnumerable<string> emotes) => _ffzEmotes = new HashSet<string>(emotes);
    public void SetSevenTvEmotes(IEnumerable<string> emotes) => _sevenTvEmotes = new HashSet<string>(emotes);

    public string Strip(string message, bool stripTwitch, bool stripBttv, bool stripFfz, bool stripSevenTv, bool stripCheermotes, bool stripTwemoji, bool allowFirst, ICollection<string>? allowedEmotes = null)
    {
        var words = message.Split(' ');
        var result = new List<string>();
        bool foundFirst = false;

        foreach (var word in words)
        {
            bool isEmote = false;

            if (allowedEmotes != null && allowedEmotes.Contains(word)) { result.Add(word); continue; }

            if (stripTwitch && _twitchEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripBttv && _bttvEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripFfz && _ffzEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripSevenTv && _sevenTvEmotes.Contains(word)) isEmote = true;
            if (!isEmote && stripCheermotes && CheermotePattern.IsMatch(word)) isEmote = true;

            if (isEmote && allowFirst && !foundFirst)
            {
                foundFirst = true;
                result.Add(word);
                continue;
            }

            if (!isEmote)
                result.Add(word);
        }

        var stripped = string.Join(" ", result);

        if (stripTwemoji)
            stripped = TwemojiPattern.Replace(stripped, string.Empty);

        return stripped.Trim();
    }
}
