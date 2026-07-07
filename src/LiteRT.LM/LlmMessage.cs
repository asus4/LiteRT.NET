using System;
using System.Text;

namespace LiteRT.LM
{
    /// <summary>
    /// Minimal JSON helpers for the LiteRT-LM Conversation message format.
    /// Hand-rolled because the bindings must compile in Unity (netstandard2.1)
    /// where System.Text.Json is not available.
    /// </summary>
    public static class LlmMessage
    {
        /// <summary>Builds a user message: <c>{"role":"user","content":[{"type":"text","text":...}]}</c>.</summary>
        public static string UserText(string text) => RoleText("user", text);

        /// <summary>Builds a message with the given role and a single text content part.</summary>
        public static string RoleText(string role, string text)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (text == null) throw new ArgumentNullException(nameof(text));
            var sb = new StringBuilder(text.Length + 64);
            sb.Append("{\"role\":");
            AppendJsonString(sb, role);
            sb.Append(",\"content\":[{\"type\":\"text\",\"text\":");
            AppendJsonString(sb, text);
            sb.Append("}]}");
            return sb.ToString();
        }

        /// <summary>Builds a system-message content object: <c>{"type":"text","text":...}</c>
        /// (the shape expected by conversation config's system message).</summary>
        public static string SystemText(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var sb = new StringBuilder(text.Length + 32);
            sb.Append("{\"type\":\"text\",\"text\":");
            AppendJsonString(sb, text);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Extracts and concatenates the <c>"text"</c> values of a message or stream-chunk
        /// JSON (<c>{"content":[{"type":"text","text":"..."}, ...]}</c>). Returns false when
        /// the JSON has no text parts (e.g. tool calls only). Consumers needing the full
        /// structure (tools, channels) should use the raw JSON APIs instead.
        /// </summary>
        public static bool TryExtractText(string json, out string text)
        {
            if (string.IsNullOrEmpty(json))
            {
                text = string.Empty;
                return false;
            }

            StringBuilder? result = null;
            int i = 0;
            while (i < json.Length)
            {
                int key = json.IndexOf("\"text\"", i, StringComparison.Ordinal);
                if (key < 0) break;

                int colon = SkipWhitespace(json, key + 6);
                if (colon >= json.Length || json[colon] != ':')
                {
                    i = key + 6;
                    continue;
                }

                int valueStart = SkipWhitespace(json, colon + 1);
                if (valueStart >= json.Length || json[valueStart] != '"')
                {
                    i = valueStart;
                    continue;
                }

                result ??= new StringBuilder(json.Length);
                i = ReadJsonString(json, valueStart, result);
            }

            text = result?.ToString() ?? string.Empty;
            return result != null;
        }

        private static int SkipWhitespace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        /// <summary>Reads a JSON string starting at the opening quote; appends the unescaped
        /// value and returns the index just past the closing quote.</summary>
        private static int ReadJsonString(string json, int openQuote, StringBuilder sb)
        {
            int i = openQuote + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"') return i + 1;
                if (c == '\\' && i + 1 < json.Length)
                {
                    char e = json[i + 1];
                    switch (e)
                    {
                        case '"': sb.Append('"'); i += 2; break;
                        case '\\': sb.Append('\\'); i += 2; break;
                        case '/': sb.Append('/'); i += 2; break;
                        case 'b': sb.Append('\b'); i += 2; break;
                        case 'f': sb.Append('\f'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        case 'u':
                            if (i + 5 < json.Length
                                && ushort.TryParse(json.Substring(i + 2, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out ushort code))
                            {
                                sb.Append((char)code);
                                i += 6;
                            }
                            else
                            {
                                i += 2;
                            }
                            break;
                        default: sb.Append(e); i += 2; break;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return i;
        }

        internal static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4",
                                System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
