using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using YARG.PlayMode;
using YARG.Song;

namespace YARG.Util
{
    public static class SongToText
    {
        public enum Style
        {
            None,
            Header,
            SubHeader
        }

        public readonly struct Line
        {
            public readonly Style Style;
            public readonly string Text;

            public Line(Style style, string text)
            {
                Style = style;
                Text = text;
            }
        }

        private enum TokenType
        {
            Keyword,
            Punctuation,
            String
        }

        private class Token
        {
            public TokenType TokenType;
            public string Value;
        }

        // These are temporary until advanced settings
        public const string FORMAT_LONG = "<header> song (speed_percent)\n" +
            "<sub_header> artist\n" +
            "album, year\n" +
            "if charter then \"Charter:\" charter";

        public const string FORMAT_SHORT = "<header> song (speed_percent)\n" +
            "<sub_header> artist";

        private static readonly Regex StyleRegex = new(@"^<[^>\s]*>", RegexOptions.Compiled);

        private static readonly Dictionary<string, Func<SongEntry, string>> Keywords = new()
        {
            {
                "song", x => x.Name
            },
            {
                "artist", x => x.Artist
            },
            {
                "year", x => x.Year
            },
            {
                "album", x => x.Album
            },
            {
                "charter", x => string.IsNullOrEmpty(x.Charter) ? "Unknown" : x.Charter
            },
            {
                "speed_percent", _ =>
                {
                    if (Play.speed == 1f)
                    {
                        return string.Empty;
                    }

                    return Play.speed.ToString("P0", new NumberFormatInfo
                    {
                        PercentPositivePattern = 1, PercentNegativePattern = 1
                    });
                }
            }
        };

        private static readonly Dictionary<string, Func<SongEntry, bool>> Conditions = new()
        {
            {
                "song", x => !string.IsNullOrEmpty(x.Name)
            },
            {
                "artist", x => !string.IsNullOrEmpty(x.Artist)
            },
            {
                "year", x => !string.IsNullOrEmpty(x.Year)
            },
            {
                "album", x => !string.IsNullOrEmpty(x.Album)
            },
            {
                "charter", x => !string.IsNullOrEmpty(x.Charter)
            },
            {
                "changed_speed", _ => Play.speed == 1f
            }
        };

        public static Line[] ToStyled(string format, SongEntry song)
        {
            var formatLines = Regex.Split(format, @"\r?\n|\r");
            var outputLines = new Line[formatLines.Length];

            for (int i = 0; i < formatLines.Length; i++)
            {
                var line = formatLines[i];

                // Get the style match
                var style = Style.None;
                var styleMatch = StyleRegex.Match(line).Value;
                if (!string.IsNullOrEmpty(styleMatch))
                {
                    style = styleMatch switch
                    {
                        "<header>"     => Style.Header,
                        "<sub_header>" => Style.SubHeader,
                        _              => Style.None
                    };
                }

                // Also remove it, and trim it
                line = StyleRegex.Replace(line, "").Trim();

                // Create a node list
                var tokenList = Tokenize(line);

                // Process keywords into strings + if statements
                bool dropLine = false;
                bool ifMode = false;
                foreach (var token in new List<Token>(tokenList))
                {
                    if (!ifMode)
                    {
                        if (token.TokenType != TokenType.Keyword)
                        {
                            continue;
                        }

                        // Normal keywords
                        if (token.Value == "if")
                        {
                            ifMode = true;
                            tokenList.Remove(token);
                        }
                        else if (Keywords.TryGetValue(token.Value, out var keyword))
                        {
                            token.Value = keyword(song);
                            token.TokenType = TokenType.String;

                            // If the result is nothing, remove
                            if (string.IsNullOrEmpty(token.Value))
                            {
                                tokenList.Remove(token);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Keyword `{token.Value}` was not found.");
                            tokenList.Remove(token);
                        }
                    }
                    else
                    {
                        if (token.TokenType != TokenType.Keyword)
                        {
                            // Drop all non-keywords
                            tokenList.Remove(token);
                            continue;
                        }

                        // If keywords
                        if (token.Value == "then")
                        {
                            ifMode = false;
                        }
                        else if (Conditions.TryGetValue(token.Value, out var condition))
                        {
                            // If the condition is not met, drop the line
                            if (!condition(song))
                            {
                                dropLine = true;
                                break;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Condition `{token.Value}` was not found.");
                        }

                        // These never stay
                        tokenList.Remove(token);
                    }
                }

                // If the "if" statement is false, drop the line (don't add it)
                if (dropLine)
                {
                    continue;
                }

                // Trim tokens at end
                var copyList = new List<Token>(tokenList);
                for (int j = copyList.Count - 1; j > 0; j--)
                {
                    var token = copyList[j];

                    // Remove whitespace
                    if (token.TokenType == TokenType.Punctuation && token.Value.All(char.IsWhiteSpace))
                    {
                        tokenList.Remove(token);
                    }
                    else
                    {
                        break;
                    }
                }

                // Process other tokens
                copyList = new List<Token>(tokenList);
                for (var j = 0; j < copyList.Count; j++)
                {
                    // Get the current token
                    var token = copyList[j];

                    // Get the previous token
                    Token previous = null;
                    if (j > 0)
                    {
                        previous = copyList[j - 1];
                    }

                    if (token.TokenType == TokenType.Punctuation)
                    {
                        if (token.Value == ")" && previous?.Value == "(")
                        {
                            // Remove ()
                            tokenList.Remove(token);
                            tokenList.Remove(previous);
                        }
                        else if (j >= copyList.Count - 1 && token.Value == ",")
                        {
                            // Remove trailing ,
                            tokenList.Remove(token);
                        }
                    }
                }

                // Convert the tokens back into a string
                string outputLine = "";
                foreach (var token in tokenList)
                {
                    outputLine += token.Value;
                }

                outputLines[i] = new Line(style, outputLine.Trim());
            }

            return outputLines;
        }

        private static List<Token> Tokenize(string line)
        {
            var tokenList = new List<Token>();
            Token currentToken = null;

            void EndToken()
            {
                if (currentToken is null)
                {
                    return;
                }

                tokenList.Add(currentToken);
                currentToken = null;
            }

            for (var i = 0; i < line.Length; i++)
            {
                char c = line[i];
                switch (currentToken)
                {
                    case null:
                        if (c == '"')
                        {
                            // Start a string
                            currentToken = new Token
                            {
                                TokenType = TokenType.String, Value = ""
                            };
                        }
                        else if (char.IsLetter(c) || c == '_')
                        {
                            // Start a keyword
                            currentToken = new Token
                            {
                                TokenType = TokenType.Keyword, Value = char.ToString(c)
                            };
                        }
                        else
                        {
                            // Append a punctuation (always one character)
                            tokenList.Add(new Token
                            {
                                TokenType = TokenType.Punctuation, Value = char.ToString(c)
                            });
                        }

                        break;
                    case { TokenType: TokenType.String }:
                        if (c != '"')
                        {
                            // Append to string
                            currentToken.Value += c;
                        }
                        else
                        {
                            // End string on "
                            EndToken();
                        }

                        break;
                    case { TokenType: TokenType.Keyword }:
                        if (char.IsLetter(c) || c == '_')
                        {
                            // Append to keyword
                            currentToken.Value += c;
                        }
                        else
                        {
                            // End keyword on non-keyword character
                            EndToken();

                            // Backtrack (now that we are out of the token)
                            i--;
                        }

                        break;
                }
            }

            EndToken();

            return tokenList;
        }
    }
}