namespace AiSa.Application;

/// <summary>
/// Token counter interface for counting tokens in text.
/// Used to verify chunk size limits before sending to embedding models.
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// Count the number of tokens in the given text.
    /// </summary>
    /// <param name="text">Text to count tokens for.</param>
    /// <returns>Number of tokens.</returns>
    int CountTokens(string text);

    /// <summary>
    /// Name of the tokenizer model/encoding used (for logging and identification).
    /// </summary>
    string ModelName { get; }
}
