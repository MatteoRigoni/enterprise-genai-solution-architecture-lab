namespace AiSa.Application;

/// <summary>
/// Basic token counter using character-based estimation.
/// Approximates tokens as ~4 characters per token (common for English text).
/// </summary>
public class BasicTokenCounter : ITokenCounter
{
    // Approximate tokens: ~4 characters per token (common approximation for English text)
    private const int CharsPerToken = 4;

    public string ModelName => "Basic-4chars";

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Simple estimation: divide character count by 4
        return (int)Math.Ceiling(text.Length / (double)CharsPerToken);
    }
}
