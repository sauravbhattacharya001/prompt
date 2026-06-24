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

    // ── Email redaction correctness ──────────────────────────────
    // Regression: the redactor used value[..2] on the whole match, which
    // grabbed the first two chars of the entire address. For a single-char
    // local part it captured the literal '@' and produced a malformed
    // redaction (e.g. "x@y.io" -> "x@***@***.io") that leaked structure.

    [Fact]
    public void RedactEmail_NeverLeaksAtSymbolFromLocalPart()
    {
        var scanner = new PromptSecretScanner();
        // Single-character local part is the trigger for the old bug.
        var redacted = scanner.Redact("mail x@y.io done");
        // Exactly one '@' should survive (the separator in the mask), never two.
        Assert.Equal(1, redacted.Count(c => c == '@'));
        Assert.DoesNotContain("@***@", redacted);
        Assert.Equal("mail x***@***.io done", redacted);
    }

    [Fact]
    public void RedactEmail_RevealsFirstLocalCharAndTld()
    {
        var scanner = new PromptSecretScanner();
        Assert.Equal("Contact j***@***.com here",
            scanner.Redact("Contact john.doe@example.com here"));
    }

    [Fact]
    public void RedactEmail_SingleCharLocalPart_DoesNotExposeDomain()
    {
        var scanner = new PromptSecretScanner();
        // "a@b.com" is 7 chars so it passes the length gate and hits the
        // Email branch; the host "b" must not appear in the redaction.
        var redacted = scanner.Redact("send a@b.com now");
        Assert.Equal("send a***@***.com now", redacted);
        Assert.DoesNotContain("a@b", redacted);
    }

    [Fact]
    public void RedactEmail_MultiLabelDomain_KeepsOnlyFinalTld()
    {
        var scanner = new PromptSecretScanner();
        var redacted = scanner.Redact("Reach a.b.c.d@sub.domain.co.uk now");
        // Only the final ".uk" label is revealed; the rest of the domain is masked.
        Assert.Equal("Reach a***@***.uk now", redacted);
        Assert.DoesNotContain("domain", redacted);
        Assert.DoesNotContain(".co.", redacted);
    }

    [Fact]
    public void RedactEmail_RedactedTextIsReconstructibleFromFindings()
    {
        var scanner = new PromptSecretScanner();
        var input = "a@b.com and john.doe@example.com and x@y.io";
        var result = scanner.Scan(input);

        var expected = input;
        foreach (var f in result.Findings.OrderByDescending(f => f.Position))
            expected = expected.Remove(f.Position, f.Length).Insert(f.Position, f.RedactedText);

        Assert.Equal(expected, result.RedactedText);
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

    // --- Regression: overlapping rule matches must not corrupt RedactedText
    // and the Findings list must agree with the redacted output (issue #188).

    [Fact]
    public void OverlappingRules_RedactedTextMatchesFindings_NoDoubleRedaction()
    {
        // "password:" triggers generic-secret on the JWT body; the JWT itself
        // triggers the jwt rule. Pre-fix this double-redacted the same span.
        var scanner = new PromptSecretScanner();
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.abcdefghijklmnop";
        var input = $"password: {jwt}";
        var result = scanner.Scan(input);

        // Exactly one finding survives dedup for that single logical secret.
        Assert.Single(result.Findings);

        // The redacted text must not still contain the secret body.
        Assert.DoesNotContain(jwt, result.RedactedText);

        // And the redacted text length must equal: prefix + the surviving
        // finding's RedactedText length. No leftover characters, no double mask.
        var only = result.Findings[0];
        var expected = input.Substring(0, only.Position) + only.RedactedText
            + input.Substring(only.Position + only.Length);
        Assert.Equal(expected, result.RedactedText);
    }

    [Fact]
    public void OverlappingRules_KeepsHigherSeverity()
    {
        // connection-string (Critical) fully contains a generic-secret (High)
        // match on the Password= portion. Critical must win.
        var scanner = new PromptSecretScanner();
        var cs = "Server=myserver;User Id=admin;Password=s3cr3tValue";
        var result = scanner.Scan(cs);

        Assert.Single(result.Findings);
        Assert.Equal("connection-string", result.Findings[0].Rule.Id);
    }

    [Fact]
    public void RedactedText_Reconstructible_FromFindings_Multiline()
    {
        var scanner = new PromptSecretScanner();
        var input = "line1 key sk-abc123def456ghi789jkl012mno x\n"
                  + "line2 email a@b.com end\n"
                  + "line3 ip 10.0.0.1 done";
        var result = scanner.Scan(input);

        // Build expected redacted text by applying findings in reverse order.
        var expected = input;
        foreach (var f in result.Findings.OrderByDescending(f => f.Position))
            expected = expected.Remove(f.Position, f.Length).Insert(f.Position, f.RedactedText);

        Assert.Equal(expected, result.RedactedText);
    }

    [Fact]
    public void OpenAIKey_ModernProjectFormat_IsDetected()
    {
        var scanner = new PromptSecretScanner();
        // sk-proj-... shape used by current OpenAI project keys.
        var key = "sk-proj-AbCdEf123456_-789xyzABCDEFghij";
        var result = scanner.Scan($"OPENAI_API_KEY={key}");

        Assert.Contains(result.Findings, f => f.Rule.Id == "openai-key");
        Assert.DoesNotContain(key, result.RedactedText);
    }

    [Fact]
    public void OpenAIKey_ServiceAccountFormat_IsDetected()
    {
        var scanner = new PromptSecretScanner();
        var key = "sk-svcacct-AbCdEf123456_-789xyzABCDEF";
        var result = scanner.Scan($"key={key}");
        Assert.Contains(result.Findings, f => f.Rule.Id == "openai-key");
    }

    [Fact]
    public void LineNumberLookup_IsCorrect_ForLastLine()
    {
        // Regression for the BinarySearch-based line attribution: secrets on
        // the final line (no trailing newline) must report the right line.
        var scanner = new PromptSecretScanner();
        var input = "line 1\nline 2\nline 3 has sk-abc123def456ghi789jkl012mno here";
        var result = scanner.Scan(input);
        var f = result.Findings.First(x => x.Rule.Id == "openai-key");
        Assert.Equal(3, f.Line);
    }

    [Fact]
    public void LineNumberLookup_FindingAtLineStart_IsCorrect()
    {
        // m.Index is exactly equal to a lineStarts entry -> BinarySearch hits.
        var scanner = new PromptSecretScanner();
        var input = "prefix\nsk-abc123def456ghi789jkl012mno tail";
        var result = scanner.Scan(input);
        var f = result.Findings.First(x => x.Rule.Id == "openai-key");
        Assert.Equal(2, f.Line);
    }

    // -- Credit card detection (Visa/MC/Discover 16-digit + Amex 15-digit) --

    [Theory]
    [InlineData("4111111111111111")]      // Visa, 16 digits
    [InlineData("4111 1111 1111 1111")]   // Visa, spaced
    [InlineData("5500-0055-5555-5559")]   // MasterCard, dashed
    [InlineData("6011000990139424")]      // Discover, 16 digits
    public void DetectsCreditCard_SixteenDigitBrands(string card)
    {
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan($"pay with {card} today");
        Assert.Contains(result.Findings, f => f.Rule.Id == "credit-card");
    }

    [Theory]
    [InlineData("378282246310005")]    // Amex, 15 digits (canonical test number)
    [InlineData("371449635398431")]    // Amex, 15 digits
    [InlineData("3714 496353 98431")]  // Amex, grouped 4-6-5
    public void DetectsAmexCreditCard_FifteenDigits(string card)
    {
        // Regression: the rule advertises Amex support, but a single 4-4-4-4
        // (16-digit) shape can never match a 15-digit Amex number. Amex needs
        // its own 4-6-5 branch; without it these leaked cards passed unredacted.
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan($"amex {card} ok");

        Assert.Contains(result.Findings, f => f.Rule.Id == "credit-card");
        // The card must also be redacted out of the returned text (last 4 kept).
        Assert.DoesNotContain(card, result.RedactedText);
        Assert.Contains("0005", scanner.Scan("amex 378282246310005 ok").RedactedText);
    }

    [Fact]
    public void CreditCardRule_DoesNotMatch_SsnShapedDigits()
    {
        // A 9-digit SSN-shaped sequence must not be picked up by the card rule
        // (the Amex branch requires a 34/37 prefix and a full 15 digits).
        var scanner = new PromptSecretScanner();
        var result = scanner.Scan("ref 123 45 6789 only");
        Assert.DoesNotContain(result.Findings, f => f.Rule.Id == "credit-card");
    }
}
