namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Category of detected secret.</summary>
    public enum SecretCategory
    {
        /// <summary>An API key or access key credential.</summary>
        ApiKey,
        /// <summary>A password or passphrase.</summary>
        Password,
        /// <summary>An authentication or access token.</summary>
        Token,
        /// <summary>A cryptographic private key block.</summary>
        PrivateKey,
        /// <summary>A database connection string containing credentials.</summary>
        ConnectionString,
        /// <summary>An email address (PII).</summary>
        Email,
        /// <summary>A phone number (PII).</summary>
        PhoneNumber,
        /// <summary>A payment-card number.</summary>
        CreditCard,
        /// <summary>A US Social Security Number (PII).</summary>
        SSN,
        /// <summary>An IP address.</summary>
        IPAddress,
        /// <summary>A JSON Web Token.</summary>
        JWT,
        /// <summary>A webhook URL (e.g. Slack/Discord).</summary>
        Webhook
    }

    /// <summary>Severity level for a detected secret.</summary>
    public enum SecretSeverity
    {
        /// <summary>Low-risk finding (e.g. IP address).</summary>
        Low,
        /// <summary>Medium-risk finding (e.g. PII such as email/phone).</summary>
        Medium,
        /// <summary>High-risk finding (e.g. generic secrets/tokens).</summary>
        High,
        /// <summary>Critical finding (e.g. cloud/API keys, private keys).</summary>
        Critical
    }

    /// <summary>A secret detection rule with pattern and metadata.</summary>
    public sealed class SecretRule
    {
        /// <summary>Stable identifier for the rule.</summary>
        public string Id { get; }
        /// <summary>Human-readable rule name.</summary>
        public string Name { get; }
        /// <summary>Category of secret this rule detects.</summary>
        public SecretCategory Category { get; }
        /// <summary>Severity assigned to matches of this rule.</summary>
        public SecretSeverity Severity { get; }
        /// <summary>Compiled, case-insensitive detection pattern.</summary>
        public Regex Pattern { get; }
        /// <summary>Description of what the rule detects.</summary>
        public string Description { get; }

        /// <summary>Creates a secret detection rule from a regex pattern and metadata.</summary>
        public SecretRule(string id, string name, SecretCategory category,
            SecretSeverity severity, string pattern, string description)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Category = category;
            Severity = severity;
            // CultureInvariant keeps IgnoreCase folding ASCII case identically across
            // thread cultures. Under tr-TR the culture-specific dotless-I rule means an
            // uppercase 'I' in a payload (e.g. "AKIA..." prefixes, "BEARER") would not
            // fold to 'i', so a case-insensitive secret rule could miss under that locale.
            Pattern = new Regex(pattern,
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(500));
            Description = description ?? "";
        }
    }

    /// <summary>A single finding from a secret scan.</summary>
    public sealed class SecretFinding
    {
        /// <summary>The rule that produced this finding.</summary>
        public SecretRule Rule { get; }
        /// <summary>The exact text that matched the rule.</summary>
        public string MatchedText { get; }
        /// <summary>The redacted form of the matched text.</summary>
        public string RedactedText { get; }
        /// <summary>Zero-based character offset of the match in the source text.</summary>
        public int Position { get; }
        /// <summary>Length of the matched text in characters.</summary>
        public int Length { get; }
        /// <summary>One-based line number of the match.</summary>
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
        /// <summary>All findings from the scan, ordered by position.</summary>
        public IReadOnlyList<SecretFinding> Findings { get; }
        /// <summary>True when at least one secret was detected.</summary>
        public bool HasSecrets => Findings.Count > 0;
        /// <summary>Total number of findings.</summary>
        public int TotalFindings => Findings.Count;
        /// <summary>Highest severity across all findings (Low when none).</summary>
        public SecretSeverity HighestSeverity { get; }
        /// <summary>The original, unredacted input text.</summary>
        public string OriginalText { get; }
        /// <summary>The input text with detected secrets redacted.</summary>
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

        /// <summary>Creates a scanner preloaded with the built-in detection rules.</summary>
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

        /// <summary>Gets all currently active rules.</summary>
        public IReadOnlyList<SecretRule> Rules => _rules.AsReadOnly();

        /// <summary>Scan prompt text for secrets.</summary>
        public SecretScanResult Scan(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new SecretScanResult(text ?? "", text ?? "", Array.Empty<SecretFinding>());

            var raw = new List<SecretFinding>();

            // Precompute line offsets (sorted, strictly increasing).
            var lineStarts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lineStarts.Add(i + 1);

            foreach (var rule in _rules.Where(r => r.Severity >= _minSeverity))
            {
                foreach (Match m in rule.Pattern.Matches(text))
                {
                    var matched = m.Value;
                    if (_allowlist.Contains(matched)) continue;

                    // O(log L) line lookup via BinarySearch.
                    int bs = lineStarts.BinarySearch(m.Index);
                    int line = bs >= 0 ? bs + 1 : ~bs;
                    var mask = Redact(matched, rule.Category);

                    raw.Add(new SecretFinding(rule, matched, mask,
                        m.Index, m.Length, line));
                }
            }

            // Deduplicate by interval overlap BEFORE redacting, so the redacted
            // text and the returned Findings list describe the same set.
            // Greedy: walk by descending priority (severity, then length, then
            // earliest position) and keep a finding only if its [pos, pos+len)
            // interval does not overlap any already-kept interval.
            var deduped = DeduplicateByOverlap(raw);

            // Apply replacements from the deduped list, ordered by descending
            // position so earlier offsets stay valid as we mutate the string.
            var sb = new System.Text.StringBuilder(text);
            foreach (var f in deduped.OrderByDescending(f => f.Position))
            {
                sb.Remove(f.Position, f.Length);
                sb.Insert(f.Position, f.RedactedText);
            }

            return new SecretScanResult(text, sb.ToString(),
                deduped.OrderBy(f => f.Position).ToList());
        }

        private static List<SecretFinding> DeduplicateByOverlap(List<SecretFinding> findings)
        {
            if (findings.Count < 2) return new List<SecretFinding>(findings);

            var prioritized = findings
                .OrderByDescending(f => (int)f.Rule.Severity)
                .ThenByDescending(f => f.Length)
                .ThenBy(f => f.Position)
                .ToList();

            var kept = new List<SecretFinding>();
            foreach (var f in prioritized)
            {
                int fEnd = f.Position + f.Length;
                bool overlaps = false;
                foreach (var k in kept)
                {
                    int kEnd = k.Position + k.Length;
                    if (f.Position < kEnd && k.Position < fEnd)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps) kept.Add(f);
            }
            return kept;
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
                SecretCategory.Email => RedactEmail(value),
                SecretCategory.CreditCard => MaskDigits(value, keepLastDigits: 4),
                SecretCategory.SSN => MaskDigits(value, keepLastDigits: 0),
                SecretCategory.PhoneNumber => MaskDigits(value, keepLastDigits: 4),
                _ => RedactGeneric(value)
            };
        }

        /// <summary>
        /// Masks the digits of a grouped numeric secret (card / phone / SSN) while
        /// preserving the original length and any grouping separators (spaces,
        /// hyphens, parentheses, '+'), revealing only the trailing
        /// <paramref name="keepLastDigits"/> digits. This is the length- and
        /// separator-preserving shape every grouped-number category needs: a fixed
        /// prefix like <c>"****-****-****-"</c> or <c>"***-***-"</c> fabricates a
        /// canonical grouping for every match, misrepresenting a 15-digit Amex, a
        /// space-separated SSN, or a <c>+1 (NNN)</c> phone, and can inflate the
        /// redaction past the original length (breaking length-preserving
        /// substitution). Only digits are masked, so separators survive verbatim.
        /// </summary>
        /// <param name="value">The matched secret text.</param>
        /// <param name="keepLastDigits">
        /// How many trailing digits to leave visible (0 masks every digit). For an
        /// SSN this is 0: the last four digits are the most sensitive part of an
        /// SSN (they are used as an identity verifier), so none are revealed.
        /// </param>
        private static string MaskDigits(string value, int keepLastDigits)
        {
            var chars = value.ToCharArray();
            // Walk from the end; leave the last `keepLastDigits` DIGITS intact and
            // mask every digit before them. Non-digit separators are never touched,
            // so the original grouping/length is preserved exactly.
            int seenDigits = 0;
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(chars[i])) continue;
                if (seenDigits < keepLastDigits) { seenDigits++; continue; }
                chars[i] = '*';
            }
            return new string(chars);
        }

        /// <summary>
        /// Masks the middle of a value while revealing a few leading and trailing
        /// characters for recognizability. The number of revealed characters scales
        /// with length so the head and tail can NEVER overlap and at least one
        /// character is always masked.
        /// </summary>
        /// <remarks>
        /// The previous implementation was <c>value[..3] + stars + value[^3..]</c>,
        /// which for a 5- or 6-character match returned overlapping head/tail slices
        /// that together spelled out the entire secret (e.g. <c>"abcde"</c> =&gt;
        /// <c>"abc***cde"</c>) — a redaction that leaked 100% of the original
        /// characters. Reveal counts are now capped so <c>2 * reveal &lt; length</c>.
        /// Callers guarantee <c>value.Length &gt;= 5</c> here (shorter values are
        /// fully starred by <see cref="Redact(string, SecretCategory)"/>).
        /// </remarks>
        private static string RedactGeneric(string value)
        {
            int reveal = value.Length >= 12 ? 3 : value.Length >= 8 ? 2 : 1;
            int masked = value.Length - 2 * reveal;
            return value[..reveal] + new string('*', masked) + value[^reveal..];
        }

        /// <summary>
        /// Redacts an email so only the first character of the local part and
        /// the trailing TLD remain visible, e.g. <c>john.doe@example.com</c> =&gt;
        /// <c>j***@***.com</c>.
        /// </summary>
        /// <remarks>
        /// The previous implementation used <c>value[..2]</c> on the whole match,
        /// which grabbed the first two characters of the entire address rather
        /// than of the local part. For a single-character local part (e.g.
        /// <c>x@y.io</c>) that included the literal <c>@</c>, producing a
        /// malformed redaction (<c>x@***@***.io</c>) that leaked the address
        /// structure. This splits on the first <c>@</c> so the masking is
        /// correct regardless of local-part length.
        /// </remarks>
        private static string RedactEmail(string value)
        {
            int at = value.IndexOf('@');
            // No '@' (shouldn't happen for the email rule, but stay defensive):
            // fall back to the generic masking shape.
            if (at <= 0)
                return value[..1] + new string('*', value.Length - 1);

            string local = value[..at];
            string domain = value[(at + 1)..];

            // Preserve only the final TLD label (the part from the last dot),
            // matching the prior intent of revealing the top-level domain.
            int lastDot = domain.LastIndexOf('.');
            string tld = lastDot >= 0 ? domain[lastDot..] : string.Empty;

            return local[..1] + "***@***" + tld;
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
                    // Matches legacy sk-XXXX keys as well as modern
                    // sk-proj-/sk-svcacct-/sk-admin-/sk-None- keys, which can
                    // contain hyphens and underscores in the body.
                    @"sk-(?:proj-|svcacct-|admin-|None-)?[A-Za-z0-9][A-Za-z0-9_\-]{19,}",
                    "OpenAI API key (legacy and project/service-account/admin formats)"),

                new SecretRule("github-token", "GitHub Token", SecretCategory.Token,
                    SecretSeverity.Critical,
                    // Two shapes: classic/OAuth tokens (ghp_/gho_/ghu_/ghs_/ghr_
                    // followed by a Base62 body) AND fine-grained PATs, which use
                    // the distinct `github_pat_` prefix — the old `gh[pousr]_`
                    // class never matched those, so the description's
                    // "fine-grained token" claim was a silent detection gap.
                    @"gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{22,}",
                    "GitHub personal access token (classic) or fine-grained token"),

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
                    // Covers every standard PEM/PGP private-key header: the bare
                    // PKCS#8 header (no type), the typed RSA/EC/DSA/OPENSSH headers,
                    // the PKCS#8 ENCRYPTED header, and the ASCII-armored PGP block.
                    // The previous (RSA|EC|DSA|OPENSSH)? group silently skipped the
                    // ENCRYPTED and PGP headers, a detection gap on a Critical rule.
                    @"-----BEGIN\s+(?:(?:RSA|EC|DSA|OPENSSH|ENCRYPTED)\s+)?PRIVATE KEY-----|-----BEGIN\s+PGP\s+PRIVATE KEY BLOCK-----",
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
                    // The area code is EITHER wrapped in a balanced "(NNN)" pair OR
                    // bare "NNN" — never a lone "(NNN" or "NNN)". The old
                    // \(?\d{3}\)? made both parens independently optional, so
                    // unbalanced fragments like "(555 123-4567" or "555) 123-4567"
                    // (a stray paren from surrounding prose butting up against a
                    // 3-3-4 digit run) were flagged as phone numbers. Splitting the
                    // two shapes into an alternation requires the parens to match.
                    @"(?<!\d)(?:\+?1[\s\-]?)?(?:\(\d{3}\)|\d{3})[\s\-]?\d{3}[\s\-]?\d{4}(?!\d)",
                    "US phone number (PII)"),

                new SecretRule("credit-card", "Credit Card Number", SecretCategory.CreditCard,
                    SecretSeverity.Critical,
                    // Two brand groups: 16-digit cards (Visa/MC/Discover, grouped
                    // 4-4-4-4) and 15-digit American Express (prefix 34/37, grouped
                    // 4-6-5). Amex is 15 digits, so it needs its own branch — the
                    // single 4-4-4-4 shape can never match a real Amex number.
                    @"(?<!\d)(?:(?:4\d{3}|5[1-5]\d{2}|6(?:011|5\d{2}))[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}|3[47]\d{2}[\s\-]?\d{6}[\s\-]?\d{5})(?!\d)",
                    "Credit card number (Visa, MC, Amex, Discover)"),

                new SecretRule("ssn", "Social Security Number", SecretCategory.SSN,
                    SecretSeverity.Critical,
                    // The two separators must be the SAME single character (space
                    // or hyphen) via a backreference, and must not be arbitrary
                    // whitespace: a bare \s matches \t/\n, so "123<newline>45<newline>6789"
                    // (three unrelated numbers across lines/cells) was falsely
                    // flagged, and mixed separators like "123-45 6789" are not a
                    // real SSN shape.
                    @"(?<!\d)\d{3}([ \-])\d{2}\1\d{4}(?!\d)",
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
