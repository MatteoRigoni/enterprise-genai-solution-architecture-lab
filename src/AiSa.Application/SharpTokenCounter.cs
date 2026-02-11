using SharpToken;

namespace AiSa.Application;

/// <summary>
/// Advanced token counter using SharpToken (tiktoken port for .NET).
/// Uses cl100k_base encoding, compatible with text-embedding-ada-002 and GPT-4.
/// </summary>
public class SharpTokenCounter : ITokenCounter
{
    private readonly GptEncoding _encoding;

    public SharpTokenCounter()
    {
        // Use cl100k_base encoding (compatible with text-embedding-ada-002, GPT-4, etc.)
        _encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    public string ModelName => "SharpToken-cl100k_base";

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Encode text to tokens and count them
        var tokens = _encoding.Encode(text);
        return tokens.Count;
    }
}
