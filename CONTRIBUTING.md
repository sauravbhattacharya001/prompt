# Contributing to Prompt

Thanks for your interest in contributing! This document explains how to set up the project, make changes, and submit a pull request.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Git
- A GitHub account

## Getting Started

1. **Fork** the repository on GitHub
2. **Clone** your fork:
   ```bash
   git clone https://github.com/<your-username>/prompt.git
   cd prompt
   ```
3. **Restore** dependencies:
   ```bash
   dotnet restore
   ```
4. **Build** the solution:
   ```bash
   dotnet build -c Release
   ```
5. **Run tests** to make sure everything passes:
   ```bash
   dotnet test
   ```

## Project Structure

```
prompt/
├── src/
│   ├── Main.cs              # Entry point — GetResponseAsync()
│   ├── Conversation.cs      # Multi-turn conversation manager
│   ├── PromptTemplate.cs    # Reusable templates with {{variables}}
│   ├── PromptChain.cs       # Multi-step LLM pipelines
│   └── Prompt.csproj        # Project file (targets .NET 8.0)
├── tests/
│   ├── MainTests.cs         # Tests for Main class
│   ├── ConversationTests.cs # Tests for Conversation
│   ├── PromptTemplateTests.cs
│   ├── PromptChainTests.cs
│   └── ...
├── docs/                    # Documentation site
├── .github/workflows/       # CI/CD, CodeQL, publishing
├── Prompt.sln               # Solution file
└── Dockerfile               # Multi-stage build for NuGet packaging
```

## Development Workflow

### Creating a Branch

Create a descriptive branch from `main`:

```bash
git checkout -b feature/add-streaming-support
# or
git checkout -b fix/retry-timeout-handling
```

Use prefixes like `feature/`, `fix/`, `docs/`, or `refactor/`.

### Making Changes

- **Follow existing code style** — the project uses C# 12 with nullable reference types enabled.
- **Add XML doc comments** to all public APIs. The project generates documentation files.
- **Keep backward compatibility** — avoid breaking changes to existing public APIs. Use optional parameters with defaults for new functionality.
- **Use `internal` visibility** where possible. Only make types/members `public` when they are part of the library's API surface.

### Writing Tests

All changes should include tests. The project uses xUnit.

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test tests/Prompt.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/ \
  /p:Include="[Prompt]*"
```

**Test guidelines:**

- Tests that call Azure OpenAI should mock the `ChatClient` — don't require real API credentials in CI.
- Use `InternalsVisibleTo` (already configured) to test internal methods when needed.
- Name tests clearly: `MethodName_Scenario_ExpectedResult`.
- Cover edge cases: null inputs, empty strings, boundary values, concurrent access.

### Code Quality

The project enforces quality through:

- **CodeQL** — automated security scanning on every push/PR
- **Codecov** — coverage reports uploaded on push to main
- **CI** — build + test on every PR

Make sure your changes pass all checks before requesting review.

## Submitting a Pull Request

1. **Commit** with a clear message:
   ```bash
   git commit -m "Add streaming response support to Conversation class"
   ```
2. **Push** to your fork:
   ```bash
   git push origin feature/add-streaming-support
   ```
3. **Open a Pull Request** against `main` on the upstream repo
4. Fill in the PR description:
   - **What** does this change do?
   - **Why** is it needed?
   - **How** was it tested?
   - Link any related issues (e.g., "Closes #8")

### PR Checklist

- [ ] Code builds without warnings (`dotnet build -c Release`)
- [ ] All tests pass (`dotnet test`)
- [ ] New public APIs have XML doc comments
- [ ] New functionality has test coverage
- [ ] No breaking changes to existing public API (or discussed in the PR)
- [ ] Commit messages are clear and descriptive

## Reporting Issues

When filing an issue, include:

- **What you expected** vs **what happened**
- **Steps to reproduce** the problem
- **.NET version** (`dotnet --version`)
- **OS** (Windows/Linux/macOS)
- **Prompt library version** (NuGet package version)

For feature requests, explain the use case and why it matters.

## Architecture Notes

A few things to keep in mind when contributing:

- **`Main` uses a singleton `ChatClient`** — thread-safe via double-checked locking. If you modify client initialization, preserve this pattern.
- **`Conversation` is not thread-safe** — it's designed for single-threaded use per instance. Document this if adding concurrent features.
- **Environment variable resolution** is cross-platform — Windows checks Process → User → Machine scopes; Linux/macOS checks Process only. Test on both if changing this logic.
- **Retry handling** is delegated to Azure.Core's pipeline via `ClientRetryPolicy`. Don't add custom retry loops.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
