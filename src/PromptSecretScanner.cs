namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Category of detected secret.</summary>
    public enum SecretCategory
    {
        ApiKey, Password, Token, PrivateKey, ConnectionString,
        Email, PhoneNumber, CreditCard, SSN, IPAddress, JWT, Webhook
    }

    /// <summary>Severity level for a detected secret.</summary>
    public enum SecretSeverity { Low, Medium, High, Critical }

    /// <summary>A secret detection rule with pattern and metadata.</summary>
    public sealed class SecretRule
    {
        public string Id { get; }
        public string Name { get; }
        public SecretCategory Category { get; }
        public SecretSeverity Severity { get; }
        public Regex Pattern { get; }
        public string Description { get; }

        public SecretRule(string id, string name, SecretCategory category,
            SecretSeverity severity, string pattern, string description)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Category = category;
            Severity = severity;
            Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            Description = description ?? "";
        }
    }

    /// <summary>A single finding from a secret scan.</summary>
    public sealed class SecretFinding
    {
        public SecretRule Rule { get; }
        public string MatchedText { get; }
        public string RedactedText { get; }
        public int Position { get; }
        public int Length { get; }
        public int Line { get; }

        internal SecretFinding(SecretRule rule, string matched, string redacted,
            int position, int length, int line)
        {
            Rule = rule;
            MatchedText = matched;
            RedactedText = redacted;
            Position = position;
            Length = length;
            Line = line;
        }
    }

    /// <summary>Result of scanning a prompt for secrets.</summary>
    public sealed class SecretScanResult
    {
        public IReadOnlyList<SecretFinding> Findings { get; }
        public bool HasSecrets => Findings.Count > 0;
        public int TotalFindings => Findings.Count;
        public SecretSeverity HighestSeverity { get; }
        public string OriginalText { get; }
        public string RedactedText { get; }

        internal SecretScanResult(string original, string redacted,
            IReadOnlyList<SecretFinding> findings)
        {
            OriginalText = original;
            RedactedText = redacted;
            Findings = findings;
            HighestSeverity = findings.Count > 0
                ? findings.Max(f => f.Rule.Severity)
                : SecretSeverity.Low;
        }

        /// <summary>Get findings filtered by category.</summary>
        public IEnumerable<SecretFinding> ByCategory(SecretCategory cat) =>
            Findings.Where(f => f.Rule.Category == cat);

        /// <summary>Get findings at or above a severity.</summary>
        public IEnumerable<SecretFinding> AtSeverity(SecretSeverity min) =>
            Findings.Where(f => f.Rule.Severity >= min);

        /// <summary>Produce a summary report.</summary>
        public string ToReport()
        {
            if (!HasSecrets) return "No secrets detected.";
            var lines = new List<string>
            {
                $"Secret Scan Report: {TotalFindings} finding(s), highest severity: {HighestSeverity}",
                new string('=', 70)
            };
            foreach (var g in Findings.GroupBy(f => f.Rule.Category))
            {
                lines.Add($"\n[{g.Key}] ({g.Count()} finding(s))");
                foreach (var f in g)
                    lines.Add($"  Line {f.Line}: {f.Rule.Name} ({f.Rule.Severity}) -> {f.RedactedText}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Scans prompt text for accidentally embedded secrets, API keys, passwords,
    /// tokens, PII, and other sensitive data. Supports redaction, custom rules,
    /// allowlists, and severity-based filtering.
    /// </summary>
    public sealed class PromptSecretScanner
    {
        private readonly List<SecretRule> _rules = new();
        private readonly HashSet<string> _allowlist = new(StringComparer.OrdinalIgnoreCase);
        private SecretSeverity _minSeverity = SecretSeverity.Low;

        public PromptSecretScanner()
        {
            LoadBuiltInRules();
        }

        /// <summary>Set minimum severity threshold for reporting.</summary>
        public PromptSecretScanner MinSeverity(SecretSeverity severity)
        {
            _minSeverity = severity;
            return this;
        }

        /// <summary>Add values to the allowlist (won't be flagged).</summary>
        public PromptSecretScanner Allow(params string[] values)
        {
            foreach (var v in values) _allowlist.Add(v.Trim());
            return this;
        }

        /// <summary>Add a custom detection rule.</summary>
        public PromptSecretScanner AddRule(SecretRule rule)
        {
            _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
            return this;
        }

        /// <summary>Remove a built-in rule by ID.</summary>
        public PromptSecretScanner RemoveRule(string id)
        {
            _rules.RemoveAll(r => r.Id == id);
            return this;
        }

        /// <summary>Get all currently active rules.</summary>
        public IReadOnlyList<SecretRule> Rules => _rules.AsReadOnly();

        /// <summary>Scan prompt text for secrets.</summary>
        public SecretScanResult Scan(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new SecretScanResult(text ?? "", text ?? "", Array.Empty<SecretFinding>());

            var findings = new List<SecretFinding>();
            var redacted = text;

            // Precompute line offsets
            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lineStarts.Add(i + 1);

            foreach (var rule in _rules.Where(r => r.Severity >= _minSeverity))
            {
                foreach (Match m in rule.Pattern.Matches(text))
                {
                    var matched = m.Value;
                    if (_allowlist.Contains(matched)) continue;

                    int line = lineStarts.Count(ls => ls <= m.Index);
                    var mask = Redact(matched, rule.Category);

                    findings.Add(new SecretFinding(rule, matched, mask,
                        m.Index, m.Length, line));
                }
            }

            // Sort by position descending for safe replacement
            foreach (var f in findings.OrderByDescending(f => f.Position))
                redacted = redacted.Remove(f.Position, f.Length).Insert(f.Position, f.RedactedText);

            // Deduplicate overlapping findings (keep highest severity)
            var deduped = findings
                .GroupBy(f => f.Position)
                .Select(g => g.OrderByDescending(f => f.Rule.Severity).First())
                .OrderBy(f => f.Position)
                .ToList();

            return new SecretScanResult(text, redacted, deduped);
        }

        /// <summary>Quick check: does the text contain any secrets?</summary>
        public bool ContainsSecrets(string text) => Scan(text).HasSecrets;

        /// <summary>Scan and return redacted text directly.</summary>
        public string Redact(string text) => Scan(text).RedactedText;

        /// <summary>Scan multiple prompts and return aggregate results.</summary>
        public IReadOnlyList<SecretScanResult> ScanAll(params string[] prompts) =>
            prompts.Select(Scan).ToList().AsReadOnly();

        private static string Redact(string value, SecretCategory cat)
        {
            if (value.Length <= 4) return new string('*', value.Length);
            return cat switch
            {
                SecretCategory.Email => value[..2] + "***@***" + (value.Contains('@') ? value[value.LastIndexOf('.')..] : ""),
                SecretCategory.CreditCard => "****-****-****-" + value[^4..],
                SecretCategory.SSN => "***-**-" + value[^4..],
                SecretCategory.PhoneNumber => "***-***-" + value[^4..],
                _ => value[..3] + new string('*', Math.Max(value.Length - 6, 3)) + value[^3..]
            };
        }

        private void LoadBuiltInRules()
        {
            _rules.AddRange(new[]
            {
                new SecretRule("aws-key", "AWS Access Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"(?<![A-Za-z0-9])AKIA[0-9A-Z]{16}(?![A-Za-z0-9])",
                    "AWS access key ID"),

                new SecretRule("aws-secret", "AWS Secret Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"(?<![A-Za-z0-9/+=])[A-Za-z0-9/+=]{40}(?=\s|$|"")",
                    "Potential AWS secret access key"),

                new SecretRule("openai-key", "OpenAI API Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"sk-[A-Za-z0-9]{20,}",
                    "OpenAI API key"),

                new SecretRule("github-token", "GitHub Token", SecretCategory.Token,
                    SecretSeverity.Critical,
                    @"gh[pousr]_[A-Za-z0-9_]{36,}",
                    "GitHub personal access token or fine-grained token"),

                new SecretRule("generic-api-key", "Generic API Key", SecretCategory.ApiKey,
                    SecretSeverity.High,
                    @"(?i)(?:api[_-]?key|apikey)\s*[:=]\s*[""']?([A-Za-z0-9\-_]{16,})[""']?",
                    "Generic API key assignment"),

                new SecretRule("generic-secret", "Generic Secret", SecretCategory.Password,
                    SecretSeverity.High,
                    @"(?i)(?:secret|password|passwd|pwd)\s*[:=]\s*[""']?([^\s""']{8,})[""']?",
                    "Generic secret or password assignment"),

                new SecretRule("bearer-token", "Bearer Token", SecretCategory.Token,
                    SecretSeverity.High,
                    @"(?i)bearer\s+[A-Za-z0-9\-_\.]{20,}",
                    "HTTP Bearer authentication token"),

                new SecretRule("jwt", "JSON Web Token", SecretCategory.JWT,
                    SecretSeverity.High,
                    @"eyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+",
                    "JSON Web Token"),

                new SecretRule("private-key", "Private Key Block", SecretCategory.PrivateKey,
                    SecretSeverity.Critical,
                    @"-----BEGIN\s+(RSA|EC|DSA|OPENSSH)?\s*PRIVATE KEY-----",
                    "PEM private key header"),

                new SecretRule("connection-string", "Connection String", SecretCategory.ConnectionString,
                    SecretSeverity.Critical,
                    @"(?i)(?:Server|Data Source|Host)\s*=\s*[^;]+;\s*(?:User\s*Id|uid)\s*=\s*[^;]+;\s*(?:Password|pwd)\s*=\s*[^;]+",
                    "Database connection string with credentials"),

                new SecretRule("slack-webhook", "Slack Webhook", SecretCategory.Webhook,
                    SecretSeverity.High,
                    @"https://hooks\.slack\.com/services/T[A-Z0-9]+/B[A-Z0-9]+/[A-Za-z0-9]+",
                    "Slack incoming webhook URL"),

                new SecretRule("discord-webhook", "Discord Webhook", SecretCategory.Webhook,
                    SecretSeverity.High,
                    @"https://discord(?:app)?\.com/api/webhooks/\d+/[A-Za-z0-9_\-]+",
                    "Discord webhook URL"),

                new SecretRule("email", "Email Address", SecretCategory.Email,
                    SecretSeverity.Medium,
                    @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
                    "Email address (PII)"),

                new SecretRule("phone-us", "US Phone Number", SecretCategory.PhoneNumber,
                    SecretSeverity.Medium,
                    @"(?<!\d)(?:\+?1[\s\-]?)?\(?\d{3}\)?[\s\-]?\d{3}[\s\-]?\d{4}(?!\d)",
                    "US phone number (PII)"),

                new SecretRule("credit-card", "Credit Card Number", SecretCategory.CreditCard,
                    SecretSeverity.Critical,
                    @"(?<!\d)(?:4\d{3}|5[1-5]\d{2}|3[47]\d{2}|6(?:011|5\d{2}))[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}(?!\d)",
                    "Credit card number (Visa, MC, Amex, Discover)"),

                new SecretRule("ssn", "Social Security Number", SecretCategory.SSN,
                    SecretSeverity.Critical,
                    @"(?<!\d)\d{3}[\s\-]\d{2}[\s\-]\d{4}(?!\d)",
                    "US Social Security Number"),

                new SecretRule("ipv4", "IPv4 Address", SecretCategory.IPAddress,
                    SecretSeverity.Low,
                    @"(?<!\d)(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)(?!\d)",
                    "IPv4 address"),

                new SecretRule("azure-key", "Azure Storage Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"(?i)AccountKey\s*=\s*[A-Za-z0-9+/=]{44,}",
                    "Azure Storage account key"),

                new SecretRule("stripe-key", "Stripe API Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"(?:sk|pk)_(?:test|live)_[A-Za-z0-9]{24,}",
                    "Stripe API key"),

                new SecretRule("sendgrid-key", "SendGrid API Key", SecretCategory.ApiKey,
                    SecretSeverity.Critical,
                    @"SG\.[A-Za-z0-9\-_]{22,}\.[A-Za-z0-9\-_]{22,}",
                    "SendGrid API key"),
            });
        }
    }
}
