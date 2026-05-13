using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StardewStoryInspector.Services;

internal static class LooseJsonParser
{
    private static readonly JsonDocumentOptions JsonNodeDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static JToken ParseTokenFromFile(string filePath)
    {
        var json = ReadNormalizedJson(filePath);
        return ParseTokenFromText(json);
    }

    public static JToken ParseTokenFromText(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal
        };

        return JToken.ReadFrom(
            jsonReader,
            new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
                LineInfoHandling = LineInfoHandling.Load
            }
        );
    }

    public static JsonNode? ParseNodeFromFile(string filePath)
    {
        var token = ParseTokenFromFile(filePath);
        return ParseNodeFromToken(token);
    }

    public static JsonNode? ParseNodeFromText(string json)
    {
        var token = ParseTokenFromText(json);
        return ParseNodeFromToken(token);
    }

    public static string ReadNormalizedJson(string filePath)
    {
        var rawJson = File.ReadAllText(filePath);
        return NormalizeJsonText(rawJson);
    }

    public static string NormalizeJsonText(string json)
    {
        var builder = new StringBuilder(json.Length);
        var inDoubleQuotedString = false;
        var inSingleQuotedString = false;
        var inLineComment = false;
        var inBlockComment = false;
        var isEscaped = false;

        for (var index = 0; index < json.Length; index++)
        {
            var ch = json[index];

            if (inLineComment)
            {
                builder.Append(ch);
                if (ch == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                builder.Append(ch);
                if (ch == '*' && index + 1 < json.Length && json[index + 1] == '/')
                {
                    builder.Append('/');
                    index++;
                    inBlockComment = false;
                }

                continue;
            }

            if (inDoubleQuotedString || inSingleQuotedString)
            {
                if (isEscaped)
                {
                    builder.Append(inSingleQuotedString && ch == '"' ? "\\\"" : ch.ToString());
                    isEscaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    if (inDoubleQuotedString && index + 1 < json.Length && json[index + 1] == '\'')
                    {
                        builder.Append('\'');
                        index++;
                        continue;
                    }

                    builder.Append(ch);
                    isEscaped = true;
                    continue;
                }

                if (inDoubleQuotedString && ch == '"')
                {
                    builder.Append(ch);
                    inDoubleQuotedString = false;
                    continue;
                }

                if (inSingleQuotedString && ch == '\'')
                {
                    builder.Append('"');
                    inSingleQuotedString = false;
                    continue;
                }

                if (char.IsControl(ch))
                {
                    builder.Append(ch switch
                    {
                        '\n' => "\\n",
                        '\r' => "\\r",
                        '\t' => "\\t",
                        '\b' => "\\b",
                        '\f' => "\\f",
                        _ => $"\\u{(int)ch:X4}"
                    });
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            if (ch == '/' && index + 1 < json.Length)
            {
                if (json[index + 1] == '/')
                {
                    builder.Append("//");
                    index++;
                    inLineComment = true;
                    continue;
                }

                if (json[index + 1] == '*')
                {
                    builder.Append("/*");
                    index++;
                    inBlockComment = true;
                    continue;
                }
            }

            if (ch == '"')
            {
                builder.Append(ch);
                inDoubleQuotedString = true;
                continue;
            }

            if (ch == '\'')
            {
                builder.Append('"');
                inSingleQuotedString = true;
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    public static string ComputeSha256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static JsonNode? ParseNodeFromToken(JToken token)
    {
        return JsonNode.Parse(
            token.ToString(Newtonsoft.Json.Formatting.None),
            documentOptions: JsonNodeDocumentOptions
        );
    }
}
