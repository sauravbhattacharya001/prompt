namespace Prompt
{
    using Azure;
    using Azure.AI.OpenAI;

    /// <summary>
    /// Entry point for sending chat completions requests to Azure OpenAI.
    /// </summary>
    /// <remarks>
    /// Requires the following user-level environment variables:
    /// <list type="bullet">
    ///   <item><c>AZURE_OPENAI_API_URI</c> – Azure OpenAI endpoint URI</item>
    ///   <item><c>AZURE_OPENAI_API_KEY</c> – Azure OpenAI API key</item>
    ///   <item><c>AZURE_OPENAI_API_MODEL</c> – Deployed model name (e.g. gpt-4)</item>
    /// </list>
    /// </remarks>
    public class Main
    {
        /// <summary>
        /// Sends a prompt to Azure OpenAI and returns the response text.
        /// </summary>
        /// <param name="prompt">The user prompt to send as a user message.</param>
        /// <param name="systemPrompt">Optional system prompt to set the assistant's behavior.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the request.</param>
        /// <returns>The model's response text, or <c>null</c> if no response was generated.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="prompt"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required environment variable is not set.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
        /// </exception>
        public static async Task<string?> GetResponseTest(
            string prompt,
            string? systemPrompt = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));

            var uri = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_URI", EnvironmentVariableTarget.User)
                ?? throw new InvalidOperationException(
                    "Environment variable AZURE_OPENAI_API_URI is not set. " +
                    "Set it as a user-level variable pointing to your Azure OpenAI endpoint.");

            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY", EnvironmentVariableTarget.User)
                ?? throw new InvalidOperationException(
                    "Environment variable AZURE_OPENAI_API_KEY is not set. " +
                    "Set it as a user-level variable with your Azure OpenAI API key.");

            var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_MODEL", EnvironmentVariableTarget.User)
                ?? throw new InvalidOperationException(
                    "Environment variable AZURE_OPENAI_API_MODEL is not set. " +
                    "Set it as a user-level variable with your deployed model name (e.g. gpt-4).");

            OpenAIClient client = new OpenAIClient(new Uri(uri), new AzureKeyCredential(key));

            var options = new ChatCompletionsOptions()
            {
                Temperature = (float)0.7,
                MaxTokens = 800,
                NucleusSamplingFactor = (float)0.95,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
            };

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                options.Messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
            options.Messages.Add(new ChatMessage(ChatRole.User, prompt));

            Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(
                model, options, cancellationToken);

            ChatCompletions completions = responseWithoutStream.Value;

            return completions?.Choices?.FirstOrDefault()?.Message.Content;
        }
    }
}
