namespace Prompt
{
    using Azure;
    using Azure.AI.OpenAI;

    /// <summary>
    /// This class is the entry point for the application.
    /// </summary>
    public class Main
    {
        public static async Task<string?> GetResponseTest(string prompt)
        {
            var uri = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_URI", EnvironmentVariableTarget.User);
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY", EnvironmentVariableTarget.User);
            var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_MODEL", EnvironmentVariableTarget.User);

            OpenAIClient client = new OpenAIClient(new Uri(uri), new AzureKeyCredential(key));

            // ### If streaming is not selected
            Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(
                model,
                new ChatCompletionsOptions()
                {
                    Messages =
                    {
                        new ChatMessage(ChatRole.System, prompt),
                    },
                    Temperature = (float)0.7,
                    MaxTokens = 800,
                    NucleusSamplingFactor = (float)0.95,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                });

            ChatCompletions completions = responseWithoutStream.Value;

            return completions?.Choices?.FirstOrDefault()?.Message.Content;
        }
    }
}