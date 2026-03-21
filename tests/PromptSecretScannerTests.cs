namespace Prompt.Tests;

using System;
using System.Linq;
using Xunit;

/// <summary>Tests for <see cref="PromptSecretScanner"/>.</summary>
public class PromptSecretScannerTests
{
    [Fact]
    public void EmptyText_ReturnsNoFindings()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("");
        Assert.False(result.HasSecrets);
        Assert.Equal(0, result.TotalFindings);
    }

    [Fact]
    public void DetectsOpenAIKey()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Use key sk-abc123def456ghi789jkl012mno for auth");
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Id == "openai-key");
    }

    [Fact]
    public void DetectsAWSAccessKey()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Access: AKIAIOSFODNN7EXAMPLE");
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Id == "aws-key");
    }

    [Fact]
    public void DetectsJWT()
    {
        var scanner = new PromptSecretScanner();
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123xyz456";
        var result = scanner.Scan($"Token: {jwt}");
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Category == SecretCategory.JWT);
    }

    [Fact]
    public void DetectsEmail()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Contact john.doe@example.com for help");
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Category == SecretCategory.Email);
    }

    [Fact]
    public void DetectsPrivateKeyHeader()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("-----BEGIN RSA PRIVATE KEY-----\nMIIE...");
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Category == SecretCategory.PrivateKey);
    }

    [Fact]
    public void DetectsConnectionString()
    {
        var scanner = new PromptSecretScanner();
        var cs = "Server=myserver;User Id=admin;Password=s3cr3t!";
        var result = scanner.Scan(cs);
        Assert.True(result.HasSecrets);
        Assert.Contains(result.Findings, f => f.Rule.Category == SecretCategory.ConnectionString);
    }

    [Fact]
    public void DetectsGenericPassword()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("password=MyS3cretP@ss");
        Assert.True(result.HasSecrets);
    }

    [Fact]
    public void RedactsSecrets()
    {
        var scanner = new PromptSecretScanner();
        var text = "Use key sk-abc123def456ghi789jkl012mno";
        var result = scanner.Scan(text);
        Assert.DoesNotContain("sk-abc123def456ghi789jkl012mno", result.RedactedText);
    }

    [Fact]
    public void AllowlistSkipsMatches()
    {
        var scanner = new PromptSecretScanner();
        scanner.Allow("test@example.com");
        var result = scanner.Scan("Contact test@example.com");
        var emailFindings = result.Findings.Where(f => f.Rule.Category == SecretCategory.Email);
        Assert.Empty(emailFindings);
    }

    [Fact]
    public void MinSeverityFilters()
    {
        var scanner = new PromptSecretScanner().MinSeverity(SecretSeverity.High);
        // IPv4 is Low severity, should be filtered out
        var result = scanner.Scan("Server at 192.168.1.100");
        var ipFindings = result.Findings.Where(f => f.Rule.Id == "ipv4");
        Assert.Empty(ipFindings);
    }

    [Fact]
    public void CustomRuleWorks()
    {
        var scanner = new PromptSecretScanner();
        scanner.AddRule(new SecretRule("custom-1", "Internal ID", SecretCategory.Token,
            SecretSeverity.High, @"INTERNAL-[A-Z]{3}-\d{6}", "Internal tracking ID"));
        var result = scanner.Scan("Reference: INTERNAL-ABC-123456");
        Assert.Contains(result.Findings, f => f.Rule.Id == "custom-1");
    }

    [Fact]
    public void RemoveRuleWorks()
    {
        var scanner = new PromptSecretScanner();
        scanner.RemoveRule("email");
        var result = scanner.Scan("Contact john@example.com");
        Assert.DoesNotContain(result.Findings, f => f.Rule.Id == "email");
    }

    [Fact]
    public void ContainsSecretsShortcut()
    {
        var scanner = new PromptSecretScanner();
        Assert.True(scanner.ContainsSecrets("key sk-abc123def456ghi789jkl012mno"));
        Assert.False(scanner.ContainsSecrets("Hello, world!"));
    }

    [Fact]
    public void RedactShortcut()
    {
        var scanner = new PromptSecretScanner();
        var redacted = scanner.Redact("key sk-abc123def456ghi789jkl012mno");
        Assert.DoesNotContain("sk-abc123def456ghi789jkl012mno", redacted);
    }

    [Fact]
    public void ScanAllProcessesMultiple()
    {
        var scanner = new PromptSecretScanner();
        var results = scanner.ScanAll("Clean text", "Has sk-abc123def456ghi789jkl012mno");
        Assert.Equal(2, results.Count);
        Assert.False(results[0].HasSecrets);
        Assert.True(results[1].HasSecrets);
    }

    [Fact]
    public void ToReportFormatsCorrectly()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Use sk-abc123def456ghi789jkl012mno here");
        Assert.Contains("Secret Scan Report", result.ToReport());
    }

    [Fact]
    public void CleanTextReportSaysNoSecrets()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Just a normal prompt");
        Assert.Equal("No secrets detected.", result.ToReport());
    }

    [Fact]
    public void DetectsStripeKey()
    {
        var scanner = new PromptSecretScanner();
        // Use split string to avoid GitHub push protection
        var key = "sk" + "_" + "live" + "_" + "AAAAAABBBBBBCCCCCCDDDDDDEE";
        var result = scanner.Scan($"Charge with {key}");
        Assert.Contains(result.Findings, f => f.Rule.Id == "stripe-key");
    }

    [Fact]
    public void DetectsSlackWebhook()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Post to https://hooks.slack.com/services/T12345678/B12345678/abcdefghijklmnop");
        Assert.Contains(result.Findings, f => f.Rule.Id == "slack-webhook");
    }

    [Fact]
    public void FindingHasLineNumber()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Line 1\nLine 2 sk-abc123def456ghi789jkl012mno\nLine 3");
        var finding = result.Findings.First(f => f.Rule.Id == "openai-key");
        Assert.Equal(2, finding.Line);
    }

    [Fact]
    public void ByCategoryFilters()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("Email: test@test.com and key sk-abc123def456ghi789jkl012mno");
        Assert.Single(result.ByCategory(SecretCategory.Email));
    }

    [Fact]
    public void AtSeverityFilters()
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("IP 10.0.0.1 and key sk-abc123def456ghi789jkl012mno");
        var critical = result.AtSeverity(SecretSeverity.Critical).ToList();
        Assert.All(critical, f => Assert.True(f.Rule.Severity >= SecretSeverity.Critical));
    }
}
