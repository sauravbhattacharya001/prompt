namespace Prompt.Tests
{
    using Xunit;
    using System;
    using System.Linq;

    public class PromptMetadataExtractorTests
    {
        private readonly PromptMetadataExtractor _ext = new();

        [Fact] public void Empty_ReturnsDefaults()
        {
            var r = _ext.Extract("");
            Assert.Equal(0, r.WordCount);
            Assert.Equal(PromptDomain.General, r.Domain);
            Assert.Equal(PromptFormality.Neutral, r.Tone);
        }

        [Fact] public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => _ext.Extract(null!));

        [Fact] public void SingleWord_Fast()
        {
            var r = _ext.Extract("Hello");
            Assert.Equal(1, r.WordCount);
            Assert.Equal("fast", r.RoutingSuggestion);
        }

        // ── Language ─────────────────────────────────────────────────

        [Fact] public void Lang_English()
        {
            Assert.Equal("en", _ext.Extract("The quick brown fox jumps over the lazy dog and this is a test").Language.Code);
        }

        [Fact] public void Lang_Spanish()
        {
            var r = _ext.Extract("El gato est\u00e1 en la casa y los ni\u00f1os juegan en el parque con las flores de primavera");
            Assert.Equal("es", r.Language.Code);
            Assert.True(r.Language.Confidence > 0.3);
        }

        [Fact] public void Lang_French()
        {
            Assert.Equal("fr", _ext.Extract("Le chat est dans la maison et les enfants jouent avec une balle pour le plaisir").Language.Code);
        }

        [Fact] public void Lang_German()
        {
            Assert.Equal("de", _ext.Extract("Der Hund ist ein gutes Tier und die Katze sitzt auf dem Stuhl nicht weit von mir").Language.Code);
        }

        // ── Capabilities ─────────────────────────────────────────────

        [Fact] public void Cap_Code()
        {
            Assert.Contains(PromptCapability.CodeGeneration, _ext.Extract("Write a Python function that implements a binary search algorithm").Capabilities);
        }

        [Fact] public void Cap_Math()
        {
            Assert.Contains(PromptCapability.MathReasoning, _ext.Extract("Calculate the derivative of x^2 + 3x and solve the equation for x").Capabilities);
        }

        [Fact] public void Cap_Translation()
        {
            Assert.Contains(PromptCapability.Translation, _ext.Extract("Translate this paragraph into French").Capabilities);
        }

        [Fact] public void Cap_Summarization()
        {
            Assert.Contains(PromptCapability.Summarization, _ext.Extract("Summarize the key points of this article").Capabilities);
        }

        [Fact] public void Cap_StructuredOutput()
        {
            Assert.Contains(PromptCapability.StructuredOutput, _ext.Extract("Return results in JSON format with name, age, email").Capabilities);
        }

        [Fact] public void Cap_Reasoning()
        {
            Assert.Contains(PromptCapability.Reasoning, _ext.Extract("Think through this step by step and reason about the pros and cons").Capabilities);
        }

        [Fact] public void Cap_AlwaysTextGen()
        {
            Assert.Contains(PromptCapability.TextGeneration, _ext.Extract("Tell me about cats").Capabilities);
        }

        [Fact] public void Cap_Multiple()
        {
            var r = _ext.Extract("Write a Python function to calculate compound interest, then summarize the key points of the algorithm step by step");
            Assert.True(r.Capabilities.Count >= 3);
        }

        // ── Domains ──────────────────────────────────────────────────

        [Fact] public void Domain_Tech()
        {
            Assert.Equal(PromptDomain.Technology, _ext.Extract("Debug this Python function. The API endpoint returns a 500 error when the database query runs on the server.").Domain);
        }

        [Fact] public void Domain_Medical()
        {
            Assert.Equal(PromptDomain.Medical, _ext.Extract("The patient presents with chronic symptoms. What diagnosis would you consider? The treatment involves medication and therapy.").Domain);
        }

        [Fact] public void Domain_Legal()
        {
            Assert.Equal(PromptDomain.Legal, _ext.Extract("Review this contract clause for liability issues. The plaintiff claims the defendant breached the statute in this jurisdiction.").Domain);
        }

        [Fact] public void Domain_Finance()
        {
            Assert.Equal(PromptDomain.Finance, _ext.Extract("Calculate the ROI on this investment portfolio. The revenue and profit projections show strong equity growth and dividend yield.").Domain);
        }

        [Fact] public void Domain_Creative()
        {
            Assert.Equal(PromptDomain.Creative, _ext.Extract("Write a story about a protagonist who discovers a hidden world. Include dialogue between the character and the antagonist.").Domain);
        }

        [Fact] public void Domain_General()
        {
            Assert.Equal(PromptDomain.General, _ext.Extract("What is the meaning of life?").Domain);
        }

        // ── Tone ─────────────────────────────────────────────────────

        [Fact] public void Tone_Formal()
        {
            Assert.Equal(PromptFormality.Formal, _ext.Extract("Pursuant to the aforementioned agreement, we hereby notify you that notwithstanding prior arrangements, the terms shall be amended.").Tone);
        }

        [Fact] public void Tone_Informal()
        {
            Assert.Equal(PromptFormality.Informal, _ext.Extract("hey yo can u help me out? lol this is confusing btw").Tone);
        }

        [Fact] public void Tone_Professional()
        {
            Assert.Equal(PromptFormality.Professional, _ext.Extract("Could you please review this document? I would appreciate your feedback. Thank you.").Tone);
        }

        [Fact] public void Tone_Neutral()
        {
            Assert.Equal(PromptFormality.Neutral, _ext.Extract("List the capitals of European countries.").Tone);
        }

        // ── Entities ─────────────────────────────────────────────────

        [Fact] public void Entity_Email()
        {
            var emails = _ext.Extract("Send to user@example.com about the project").Entities.Where(e => e.Type == "email").ToList();
            Assert.Single(emails);
            Assert.Equal("user@example.com", emails[0].Text);
        }

        [Fact] public void Entity_Url()
        {
            Assert.Single(_ext.Extract("Check https://docs.example.com/v2 for details").Entities.Where(e => e.Type == "url"));
        }

        [Fact] public void Entity_FilePath()
        {
            Assert.Contains(_ext.Extract("Edit src/main.py and check config.json").Entities, e => e.Type == "file_path");
        }

        [Fact] public void Entity_Date()
        {
            Assert.Contains(_ext.Extract("The deadline is 2026-03-15 for submission").Entities, e => e.Type == "date");
        }

        [Fact] public void Entity_CodeLangs()
        {
            var langs = _ext.Extract("Write this in Python and also provide a JavaScript version").Entities.Where(e => e.Type == "code_lang").Select(e => e.Text).ToList();
            Assert.Contains("python", langs);
            Assert.Contains("javascript", langs);
        }

        [Fact] public void Entity_Numbers()
        {
            Assert.Contains(_ext.Extract("The budget is $1,500.00 and we expect 25% growth").Entities, e => e.Type == "number");
        }

        [Fact] public void Entities_SortedByPosition()
        {
            var ents = _ext.Extract("Email user@test.com at https://test.com on 2026-01-01").Entities;
            for (int i = 1; i < ents.Count; i++)
                Assert.True(ents[i].StartIndex >= ents[i - 1].StartIndex);
        }

        // ── Questions & Instructions ─────────────────────────────────

        [Fact] public void Questions_Count()
        {
            Assert.Equal(3, _ext.Extract("What is AI? How does it work? Why is it important?").QuestionCount);
        }

        [Fact] public void Instructions_Count()
        {
            Assert.True(_ext.Extract("1. Write a summary\n2. List the key points\n3. Create a diagram\n4. Explain the concept").InstructionCount >= 3);
        }

        // ── Examples & System Directives ─────────────────────────────

        [Fact] public void HasExamples_Text()
        {
            Assert.True(_ext.Extract("For example, if the input is 'hello' the output should be 'HELLO'").HasExamples);
        }

        [Fact] public void HasExamples_CodeBlock()
        {
            Assert.True(_ext.Extract("Here is an example:\n```python\nprint('hello')\n```").HasExamples);
        }

        [Fact] public void HasSystemDirectives()
        {
            Assert.True(_ext.Extract("You are a helpful assistant. You must always be concise. Never use jargon.").HasSystemDirectives);
        }

        [Fact] public void NoSystemDirectives()
        {
            Assert.False(_ext.Extract("What color is the sky?").HasSystemDirectives);
        }

        // ── Routing ──────────────────────────────────────────────────

        [Fact] public void Route_Fast()
        {
            Assert.Equal("fast", _ext.Extract("What is 2+2?").RoutingSuggestion);
        }

        [Fact] public void Route_SpecialistOrPremium()
        {
            var r = _ext.Extract("Write a Python function to implement a Red-Black tree. Include step by step reasoning. Debug and refactor it. Summarize complexity. Output in JSON format.");
            Assert.True(r.RoutingSuggestion == "specialist" || r.RoutingSuggestion == "premium");
        }

        [Fact] public void Route_Standard()
        {
            Assert.Equal("standard", _ext.Extract("Write a detailed paragraph about the various benefits and advantages of regular exercise for maintaining good physical and mental health over a long period of time.").RoutingSuggestion);
        }

        // ── Tags ─────────────────────────────────────────────────────

        [Fact] public void Tag_WordBucket_Micro()
        {
            Assert.Equal("micro", _ext.Extract("Hello world").Tags["word_count_bucket"]);
        }

        [Fact] public void Tag_WordBucket_Short()
        {
            Assert.Equal("short", _ext.Extract("Tell me about machine learning basics and how it works in practice").Tags["word_count_bucket"]);
        }

        [Fact] public void Tag_CodeLanguages()
        {
            Assert.Contains("python", _ext.Extract("Write this in Python").Tags["code_languages"]);
        }

        [Fact] public void Tag_FewShot()
        {
            Assert.Equal("few_shot", _ext.Extract("For example, input: 'cat' output: 'animal'").Tags["pattern"]);
        }

        // ── Batch ────────────────────────────────────────────────────

        [Fact] public void Batch_ProcessesAll()
        {
            Assert.Equal(3, _ext.ExtractBatch(new[] { "Hello", "Write code", "Translate" }).Count);
        }

        [Fact] public void Batch_NullThrows() => Assert.Throws<ArgumentNullException>(() => _ext.ExtractBatch(null!));

        // ── Integration ──────────────────────────────────────────────

        [Fact] public void Integration_SystemCodeReview()
        {
            var r = _ext.Extract(
                "You are a senior Python developer. Act as a code reviewer. " +
                "Review the code for bugs, security issues, and performance problems. Debug any issues. Always explain step by step. " +
                "Never break backward compatibility.\n\n```python\ndef calc(x): return x*2\n```");
            Assert.True(r.HasSystemDirectives);
            Assert.True(r.HasExamples);
            Assert.Contains(PromptCapability.CodeGeneration, r.Capabilities);
            Assert.Equal(PromptDomain.Technology, r.Domain);
        }

        [Fact] public void Integration_DataAnalysis()
        {
            var r = _ext.Extract("Analyze this dataset. Calculate the mean, median, and standard deviation. Identify outliers. Show the trend over time in a markdown table.");
            Assert.Contains(PromptCapability.DataAnalysis, r.Capabilities);
            Assert.Contains(PromptCapability.StructuredOutput, r.Capabilities);
        }
    }
}
