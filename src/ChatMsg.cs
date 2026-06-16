namespace Prompt
{
    /// <summary>
    /// A vendor-neutral chat message used at the provider boundary
    /// (<see cref="ILlmProvider"/>). Decouples the library's execution
    /// layer from any specific SDK's message type so the same message
    /// list can be sent to Azure OpenAI, OpenAI, Anthropic, Gemini, and
    /// the OpenAI-compatible vendors (Mistral, Groq, DeepSeek, Grok,
    /// OpenRouter, Together, Fireworks, Ollama).
    /// </summary>
    /// <param name="Role">
    /// The message role: <c>"system"</c>, <c>"user"</c>, or <c>"assistant"</c>.
    /// Providers map these onto their own conventions (for example Gemini
    /// maps <c>"assistant"</c> to <c>"model"</c> and lifts <c>"system"</c>
    /// into a top-level instruction field).
    /// </param>
    /// <param name="Content">The message text.</param>
    public readonly record struct ChatMsg(string Role, string Content)
    {
        /// <summary>The system role constant.</summary>
        public const string System = "system";

        /// <summary>The user role constant.</summary>
        public const string User = "user";

        /// <summary>The assistant role constant.</summary>
        public const string Assistant = "assistant";

        /// <summary>Creates a <see cref="ChatMsg"/> with the <c>system</c> role.</summary>
        public static ChatMsg FromSystem(string content) => new(System, content);

        /// <summary>Creates a <see cref="ChatMsg"/> with the <c>user</c> role.</summary>
        public static ChatMsg FromUser(string content) => new(User, content);

        /// <summary>Creates a <see cref="ChatMsg"/> with the <c>assistant</c> role.</summary>
        public static ChatMsg FromAssistant(string content) => new(Assistant, content);
    }
}
