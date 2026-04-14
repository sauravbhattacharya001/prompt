namespace Prompt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Ranked search result from <see cref="PromptSemanticSearch"/>.
    /// </summary>
    public class SearchResult
    {
        /// <summary>Name of the matching prompt entry.</summary>
        public string Name { get; set; } = "";

        /// <summary>The matched <see cref="PromptEntry"/>.</summary>
        public PromptEntry Entry { get; set; } = null!;

        /// <summary>BM25 relevance score (higher = more relevant).</summary>
        public double Score { get; set; }

        /// <summary>
        /// Per-term breakdown showing which query terms matched and their
        /// individual BM25 contributions.
        /// </summary>
        public IReadOnlyDictionary<string, double> TermScores { get; set; }
            = new Dictionary<string, double>();

        /// <summary>Which searchable fields contained matches.</summary>
        public IReadOnlyList<string> MatchedFields { get; set; }
            = Array.Empty<string>();
    }

    /// <summary>
    /// Intelligent prompt search engine using BM25 (Best Matching 25) ranking.
    /// Indexes prompt entries by name, description, category, tags, and template
    /// body, then ranks results by term frequency, inverse document frequency,
    /// and document length normalization.
    ///
    /// <para>Supports:</para>
    /// <list type="bullet">
    ///   <item><description>BM25 ranking with configurable k1/b parameters</description></item>
    ///   <item><description>Field-weighted scoring (name/tags weighted higher than body)</description></item>
    ///   <item><description>Prefix matching for partial queries</description></item>
    ///   <item><description>Stop-word filtering for English</description></item>
    ///   <item><description>Stemming via Porter-style suffix stripping</description></item>
    ///   <item><description>Incremental index updates (add/remove without full rebuild)</description></item>
    ///   <item><description>Fuzzy matching via Levenshtein distance for typo tolerance</description></item>
    ///   <item><description>Query expansion with synonyms and related terms</description></item>
    /// </list>
    /// </summary>
    public class PromptSemanticSearch
    {
        // BM25 parameters
        private readonly double _k1;
        private readonly double _b;

        // Field boost weights
        private readonly double _nameBoost;
        private readonly double _descriptionBoost;
        private readonly double _categoryBoost;
        private readonly double _tagBoost;
        private readonly double _bodyBoost;

        // Fuzzy matching
        private readonly int _maxEditDistance;
        private readonly double _fuzzyScoreMultiplier;
        private readonly bool _enableQueryExpansion;

        // Synonym map for query expansion (stemmed form → related stemmed forms)
        private static readonly Dictionary<string, string[]> SynonymMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "error", new[] { "bug", "fault", "defect", "fail" } },
            { "bug", new[] { "error", "fault", "defect", "issue" } },
            { "fast", new[] { "quick", "speed", "perform", "optim" } },
            { "quick", new[] { "fast", "speed", "rapid" } },
            { "secure", new[] { "safe", "protect", "auth", "encrypt" } },
            { "safe", new[] { "secure", "protect", "valid" } },
            { "test", new[] { "check", "verif", "valid", "assert" } },
            { "check", new[] { "test", "verif", "valid" } },
            { "creat", new[] { "generat", "build", "make", "produc" } },
            { "generat", new[] { "creat", "build", "produc" } },
            { "delet", new[] { "remov", "clear", "purg" } },
            { "remov", new[] { "delet", "clear", "strip" } },
            { "updat", new[] { "modif", "chang", "edit", "alter" } },
            { "modif", new[] { "updat", "chang", "edit" } },
            { "search", new[] { "find", "look", "query", "seek" } },
            { "find", new[] { "search", "look", "query", "locat" } },
            { "format", new[] { "style", "template", "layout" } },
            { "templat", new[] { "format", "pattern", "layout" } },
            { "parse", new[] { "extract", "read", "analyz" } },
            { "analyz", new[] { "parse", "inspect", "examin" } },
            { "send", new[] { "emit", "dispatch", "transmit" } },
            { "respons", new[] { "reply", "answer", "output" } },
            { "config", new[] { "set", "option", "prefer" } },
            { "optim", new[] { "fast", "improv", "enhanc", "speed" } },
        };

        // Index structures
        private readonly Dictionary<string, PromptEntry> _entries = new();
        private readonly Dictionary<string, Dictionary<string, double>> _tfVectors = new();
        private readonly Dictionary<string, int> _documentFrequency = new();
        private readonly Dictionary<string, int> _documentLengths = new();
        private int _totalDocuments;
        private double _avgDocumentLength;

        // Running total for incremental average-length computation
        private int _totalDocumentLength;

        // Field tracking for matched-field reporting
        private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _fieldTerms = new();

        // Pre-compiled regex for tokenization (avoids recompilation each call)
        private static readonly Regex TokenizerRegex = new(@"[^a-z0-9]+",
            RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "in", "on", "at", "to",
            "for", "of", "with", "by", "from", "is", "it", "as", "be",
            "was", "are", "been", "being", "have", "has", "had", "do",
            "does", "did", "will", "would", "could", "should", "may",
            "might", "shall", "can", "this", "that", "these", "those",
            "i", "you", "he", "she", "we", "they", "me", "him", "her",
            "us", "them", "my", "your", "his", "its", "our", "their",
            "not", "no", "nor", "if", "then", "else", "when", "up",
            "out", "so", "than", "too", "very", "just", "about", "into"
        };

        /// <summary>
        /// Creates a new semantic search engine with default BM25 parameters.
        /// </summary>
        /// <param name="k1">
        /// Term frequency saturation parameter (default 1.5).
        /// Higher values increase the impact of term frequency.
        /// </param>
        /// <param name="b">
        /// Document length normalization (default 0.75).
        /// 0 = no normalization, 1 = full normalization.
        /// </param>
        /// <param name="nameBoost">Boost factor for name field matches (default 3.0).</param>
        /// <param name="descriptionBoost">Boost factor for description matches (default 1.5).</param>
        /// <param name="categoryBoost">Boost factor for category matches (default 2.0).</param>
        /// <param name="tagBoost">Boost factor for tag matches (default 2.5).</param>
        /// <param name="bodyBoost">Boost factor for template body matches (default 1.0).</param>
        /// <param name="maxEditDistance">Maximum Levenshtein distance for fuzzy matching (default 2). 0 disables fuzzy.</param>
        /// <param name="fuzzyScoreMultiplier">Score multiplier for fuzzy matches (default 0.4). Lower = less weight for fuzzy hits.</param>
        /// <param name="enableQueryExpansion">Whether to expand queries with synonyms (default true).</param>
        public PromptSemanticSearch(
            double k1 = 1.5,
            double b = 0.75,
            double nameBoost = 3.0,
            double descriptionBoost = 1.5,
            double categoryBoost = 2.0,
            double tagBoost = 2.5,
            double bodyBoost = 1.0,
            int maxEditDistance = 2,
            double fuzzyScoreMultiplier = 0.4,
            bool enableQueryExpansion = true)
        {
            if (k1 < 0) throw new ArgumentOutOfRangeException(nameof(k1), "k1 must be non-negative");
            if (b < 0 || b > 1) throw new ArgumentOutOfRangeException(nameof(b), "b must be between 0 and 1");

            _k1 = k1;
            _b = b;
            _nameBoost = nameBoost;
            _descriptionBoost = descriptionBoost;
            _categoryBoost = categoryBoost;
            _tagBoost = tagBoost;
            _bodyBoost = bodyBoost;
            _maxEditDistance = maxEditDistance;
            _fuzzyScoreMultiplier = fuzzyScoreMultiplier;
            _enableQueryExpansion = enableQueryExpansion;
        }

        /// <summary>Number of indexed documents.</summary>
        public int DocumentCount => _totalDocuments;

        /// <summary>Number of unique terms in the index.</summary>
        public int VocabularySize => _documentFrequency.Count;

        /// <summary>Average document length (in terms) across all indexed entries.</summary>
        public double AverageDocumentLength => _avgDocumentLength;

        // ── Indexing ─────────────────────────────────────────────────

        /// <summary>
        /// Index a single prompt entry. If an entry with the same name already
        /// exists, it is replaced.
        /// </summary>
        /// <param name="entry">The prompt entry to index.</param>
        /// <exception cref="ArgumentNullException">If entry is null.</exception>
        public void Index(PromptEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            // Remove old version if exists
            if (_entries.ContainsKey(entry.Name))
                Remove(entry.Name);

            _entries[entry.Name] = entry;

            // Tokenize all fields with boosts
            var allTerms = new List<string>();
            var fieldTermMap = new Dictionary<string, HashSet<string>>();

            AddFieldTerms(allTerms, fieldTermMap, "name", entry.Name, _nameBoost);
            AddFieldTerms(allTerms, fieldTermMap, "description", entry.Description ?? "", _descriptionBoost);
            AddFieldTerms(allTerms, fieldTermMap, "category", entry.Category ?? "", _categoryBoost);

            if (entry.Tags != null)
            {
                foreach (var tag in entry.Tags)
                    AddFieldTerms(allTerms, fieldTermMap, "tags", tag, _tagBoost);
            }

            AddFieldTerms(allTerms, fieldTermMap, "body", entry.Template?.Template ?? "", _bodyBoost);

            _fieldTerms[entry.Name] = fieldTermMap;

            // Build TF vector (boosted term frequencies)
            var tf = new Dictionary<string, double>();
            foreach (var term in allTerms)
            {
                tf.TryGetValue(term, out var count);
                tf[term] = count + 1;
            }
            _tfVectors[entry.Name] = tf;
            _documentLengths[entry.Name] = allTerms.Count;

            // Update document frequency
            foreach (var term in tf.Keys)
            {
                _documentFrequency.TryGetValue(term, out var df);
                _documentFrequency[term] = df + 1;
            }

            _totalDocuments++;
            _totalDocumentLength += allTerms.Count;
            _avgDocumentLength = _totalDocuments > 0
                ? _totalDocumentLength / (double)_totalDocuments : 0;
        }

        /// <summary>
        /// Index multiple prompt entries at once.
        /// </summary>
        public void IndexAll(IEnumerable<PromptEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries)
                Index(entry);
        }

        /// <summary>
        /// Remove an entry from the index by name.
        /// </summary>
        /// <returns>True if the entry was found and removed.</returns>
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name) || !_entries.ContainsKey(name))
                return false;

            // Decrement document frequency for all terms in this doc
            if (_tfVectors.TryGetValue(name, out var tf))
            {
                foreach (var term in tf.Keys)
                {
                    if (_documentFrequency.TryGetValue(term, out var df))
                    {
                        if (df <= 1)
                            _documentFrequency.Remove(term);
                        else
                            _documentFrequency[term] = df - 1;
                    }
                }
            }

            _entries.Remove(name);
            _tfVectors.Remove(name);
            if (_documentLengths.TryGetValue(name, out var removedLen))
            {
                _totalDocumentLength -= removedLen;
                _documentLengths.Remove(name);
            }
            _fieldTerms.Remove(name);
            _totalDocuments--;
            _avgDocumentLength = _totalDocuments > 0
                ? _totalDocumentLength / (double)_totalDocuments : 0;
            return true;
        }

        /// <summary>
        /// Clear the entire index.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _tfVectors.Clear();
            _documentFrequency.Clear();
            _documentLengths.Clear();
            _fieldTerms.Clear();
            _totalDocuments = 0;
            _totalDocumentLength = 0;
            _avgDocumentLength = 0;
        }

        // ── Search ───────────────────────────────────────────────────

        /// <summary>
        /// Search for prompts matching the query, ranked by BM25 relevance.
        /// </summary>
        /// <param name="query">Natural language search query.</param>
        /// <param name="maxResults">Maximum results to return (default 10).</param>
        /// <param name="minScore">Minimum BM25 score threshold (default 0).</param>
        /// <returns>Ranked list of search results.</returns>
        public IReadOnlyList<SearchResult> Search(string query, int maxResults = 10, double minScore = 0)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<SearchResult>();
            if (maxResults < 1)
                throw new ArgumentOutOfRangeException(nameof(maxResults));

            var queryTerms = Tokenize(query);
            if (queryTerms.Count == 0)
                return Array.Empty<SearchResult>();

            // Expand query with synonyms
            var expandedTerms = _enableQueryExpansion
                ? ExpandQueryWithSynonyms(queryTerms)
                : queryTerms.ToDictionary(t => t, _ => 1.0);

            // Pre-compute prefix and fuzzy expansions once against the global
            // vocabulary instead of recalculating per document. This reduces
            // fuzzy matching from O(queryTerms × docs × vocabPerDoc) to
            // O(queryTerms × globalVocab + queryTerms × docs).
            var prefixExpansions = new Dictionary<string, string?>();   // term → best prefix match in global vocab
            var fuzzyExpansions = new Dictionary<string, (string term, double penalty)?>();  // term → best fuzzy match

            var globalVocab = _documentFrequency.Keys;

            foreach (var termKvp in expandedTerms)
            {
                var term = termKvp.Key;

                // Pre-compute best prefix match (shortest indexed term that starts with query term)
                if (term.Length >= 3)
                {
                    string? bestPrefix = null;
                    int bestLen = int.MaxValue;
                    foreach (var indexedTerm in globalVocab)
                    {
                        if (indexedTerm.Length > term.Length &&
                            indexedTerm.Length < bestLen &&
                            indexedTerm.AsSpan().StartsWith(term.AsSpan(), StringComparison.Ordinal))
                        {
                            bestPrefix = indexedTerm;
                            bestLen = indexedTerm.Length;
                            // Can't get shorter than term.Length + 1
                            if (bestLen == term.Length + 1) break;
                        }
                    }
                    prefixExpansions[term] = bestPrefix;
                }

                // Pre-compute best fuzzy match
                if (_maxEditDistance > 0 && term.Length >= 4)
                {
                    int bestDist = _maxEditDistance + 1;
                    string? bestFuzzyTerm = null;
                    foreach (var indexedTerm in globalVocab)
                    {
                        if (Math.Abs(indexedTerm.Length - term.Length) > _maxEditDistance)
                            continue;
                        int dist = LevenshteinDistance(term, indexedTerm, _maxEditDistance);
                        if (dist > 0 && dist <= _maxEditDistance && dist < bestDist)
                        {
                            bestDist = dist;
                            bestFuzzyTerm = indexedTerm;
                        }
                    }
                    if (bestFuzzyTerm != null)
                        fuzzyExpansions[term] = (bestFuzzyTerm, _fuzzyScoreMultiplier / bestDist);
                    else
                        fuzzyExpansions[term] = null;
                }
            }

            var results = new List<SearchResult>();

            foreach (var kvp in _entries)
            {
                var name = kvp.Key;
                var entry = kvp.Value;
                var tf = _tfVectors[name];
                var docLen = _documentLengths[name];

                var termScores = new Dictionary<string, double>();
                double totalScore = 0;

                foreach (var termKvp in expandedTerms)
                {
                    var term = termKvp.Key;
                    var termWeight = termKvp.Value;

                    double score = ComputeTermBM25(term, tf, docLen) * termWeight;

                    // Prefix matching using pre-computed expansion
                    if (score == 0 && prefixExpansions.TryGetValue(term, out var prefixTerm) && prefixTerm != null)
                    {
                        score = ComputeTermBM25(prefixTerm, tf, docLen) * 0.6 * termWeight;
                    }

                    // Fuzzy matching using pre-computed expansion
                    if (score == 0 && fuzzyExpansions.TryGetValue(term, out var fuzzyMatch) && fuzzyMatch != null)
                    {
                        score = ComputeTermBM25(fuzzyMatch.Value.term, tf, docLen) * fuzzyMatch.Value.penalty * termWeight;
                    }

                    if (score > 0)
                    {
                        termScores[term] = Math.Round(score, 4);
                        totalScore += score;
                    }
                }

                if (totalScore > minScore)
                {
                    var matchedFields = GetMatchedFields(name, queryTerms);

                    results.Add(new SearchResult
                    {
                        Name = name,
                        Entry = entry,
                        Score = Math.Round(totalScore, 4),
                        TermScores = termScores,
                        MatchedFields = matchedFields
                    });
                }
            }

            // When maxResults is much smaller than the result set, a partial
            // sort via bounded insertion is O(n·k) which beats O(n log n) full
            // sort for the typical case where the caller wants top-10 from
            // hundreds of documents.
            if (results.Count <= maxResults)
            {
                results.Sort((a, b) => b.Score.CompareTo(a.Score));
                return results;
            }

            // Bounded selection: keep only the top-maxResults entries.
            // Uses a simple min-tracking approach that avoids sorting the
            // entire list — keeps a sorted-by-score list of size maxResults
            // and replaces the minimum when a better candidate arrives.
            var top = new List<SearchResult>(maxResults + 1);
            double threshold = double.MinValue;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (top.Count < maxResults)
                {
                    InsertSorted(top, r);
                    if (top.Count == maxResults)
                        threshold = top[top.Count - 1].Score;
                }
                else if (r.Score > threshold)
                {
                    // Remove the smallest (last) and insert new
                    top.RemoveAt(top.Count - 1);
                    InsertSorted(top, r);
                    threshold = top[top.Count - 1].Score;
                }
            }
            return top;
        }

        /// <summary>
        /// Find prompts similar to a given prompt by name (using its terms as query).
        /// </summary>
        /// <param name="name">Name of the source prompt entry.</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <returns>Ranked list of similar prompts (excludes the source).</returns>
        public IReadOnlyList<SearchResult> FindSimilar(string name, int maxResults = 5)
        {
            if (string.IsNullOrEmpty(name) || !_entries.ContainsKey(name))
                return Array.Empty<SearchResult>();

            var entry = _entries[name];

            // Build a query from the entry's key fields
            var queryParts = new List<string> { entry.Name.Replace("-", " ").Replace("_", " ") };
            if (!string.IsNullOrEmpty(entry.Description))
                queryParts.Add(entry.Description);
            if (!string.IsNullOrEmpty(entry.Category))
                queryParts.Add(entry.Category);
            if (entry.Tags != null)
                queryParts.AddRange(entry.Tags);

            var query = string.Join(" ", queryParts);
            var results = Search(query, maxResults + 1);

            // Exclude the source entry
            return results.Where(r => r.Name != name).Take(maxResults).ToList();
        }

        /// <summary>
        /// Get the top terms (highest IDF) in the index — useful for
        /// understanding what distinguishes prompts.
        /// </summary>
        /// <param name="count">Number of terms to return.</param>
        /// <returns>Terms sorted by IDF score (most distinctive first).</returns>
        public IReadOnlyList<(string Term, double Idf, int DocumentFrequency)> GetTopTerms(int count = 20)
        {
            if (_totalDocuments == 0)
                return Array.Empty<(string, double, int)>();

            return _documentFrequency
                .Select(kvp => (
                    Term: kvp.Key,
                    Idf: Math.Round(ComputeIdf(kvp.Value), 4),
                    DocumentFrequency: kvp.Value))
                .OrderByDescending(t => t.Idf)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get index statistics.
        /// </summary>
        public SearchIndexStats GetStats()
        {
            return new SearchIndexStats
            {
                TotalDocuments = _totalDocuments,
                VocabularySize = _documentFrequency.Count,
                AverageDocumentLength = Math.Round(_avgDocumentLength, 2),
                TotalTermOccurrences = CountTotalTermOccurrences(),
                LongestDocument = _documentLengths.Count > 0 ? _documentLengths.Values.Max() : 0,
                ShortestDocument = _documentLengths.Count > 0 ? _documentLengths.Values.Min() : 0,
                MostCommonTerms = _documentFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList()
            };
        }

        // ── BM25 internals ───────────────────────────────────────────

        /// <summary>
        /// Binary-search insert into a descending-sorted list of search results.
        /// Used by the bounded top-K selection in <see cref="Search"/>.
        /// </summary>
        private static void InsertSorted(List<SearchResult> list, SearchResult item)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (list[mid].Score >= item.Score)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            list.Insert(lo, item);
        }

        /// <summary>
        /// Counts total term occurrences across all indexed documents without
        /// LINQ allocation overhead.
        /// </summary>
        private int CountTotalTermOccurrences()
        {
            int total = 0;
            foreach (var tf in _tfVectors.Values)
            {
                foreach (var count in tf.Values)
                    total += (int)count;
            }
            return total;
        }

        private double ComputeTermBM25(string term, Dictionary<string, double> tf, int docLen)
        {
            if (!tf.TryGetValue(term, out var termFreq))
                return 0;

            if (!_documentFrequency.TryGetValue(term, out var df))
                return 0;

            double idf = ComputeIdf(df);
            double numerator = termFreq * (_k1 + 1);
            double denominator = termFreq + _k1 * (1 - _b + _b * (docLen / Math.Max(1, _avgDocumentLength)));

            return idf * (numerator / denominator);
        }

        private double ComputeIdf(int documentFrequency)
        {
            // Standard BM25 IDF with smoothing
            return Math.Log((_totalDocuments - documentFrequency + 0.5) / (documentFrequency + 0.5) + 1);
        }

        // ── Tokenization ─────────────────────────────────────────────

        internal static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Split on non-alphanumeric, lowercase, filter stop words, stem.
            // Uses direct loop instead of LINQ chain to avoid iterator/delegate
            // overhead — this method is called per-field per-document during
            // indexing and per-query during search, so it's on the hot path.
            var parts = TokenizerRegex.Split(text.ToLowerInvariant());
            var tokens = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i];
                if (t.Length < 2 || StopWords.Contains(t))
                    continue;
                var stemmed = Stem(t);
                if (stemmed.Length >= 2)
                    tokens.Add(stemmed);
            }
            return tokens;
        }

        /// <summary>
        /// Simple Porter-style suffix stripping for English.
        /// Handles common suffixes to normalize word forms.
        /// </summary>
        internal static string Stem(string word)
        {
            if (word.Length <= 3) return word;

            // Common suffixes, ordered by length (strip longest first)
            if (word.EndsWith("ational")) return word[..^5] + "e";
            if (word.EndsWith("ization")) return word[..^5] + "e";
            if (word.EndsWith("fulness")) return word[..^4];
            if (word.EndsWith("iveness")) return word[..^4];
            if (word.EndsWith("ousness")) return word[..^4];
            if (word.EndsWith("nesses")) return word[..^2];
            if (word.EndsWith("ically")) return word[..^4];
            if (word.EndsWith("ations")) return word[..^3] + "e";
            if (word.EndsWith("ating")) return word[..^3] + "e";
            if (word.EndsWith("ation")) return word[..^2] + "e";
            if (word.EndsWith("ously")) return word[..^2];
            if (word.EndsWith("izing")) return word[..^3] + "e";
            if (word.EndsWith("bling")) return word[..^3] + "e";
            if (word.EndsWith("iness")) return word[..^4] + "y";
            if (word.EndsWith("ment")) return word[..^4];
            if (word.EndsWith("ness")) return word[..^4];
            if (word.EndsWith("able")) return word[..^4];
            if (word.EndsWith("ible")) return word[..^4];
            if (word.EndsWith("ting")) return word[..^3] + "e";
            if (word.EndsWith("ally")) return word[..^2];
            if (word.EndsWith("ful")) return word[..^3];
            if (word.EndsWith("ies")) return word[..^3] + "y";
            if (word.EndsWith("ing") && word.Length > 5) return word[..^3];
            if (word.EndsWith("ion") && word.Length > 4) return word[..^3];
            if (word.EndsWith("ers")) return word[..^1];
            if (word.EndsWith("ed") && word.Length > 4) return word[..^2];
            if (word.EndsWith("ly") && word.Length > 4) return word[..^2];
            if (word.EndsWith("er") && word.Length > 4) return word[..^2];
            if (word.EndsWith("es") && word.Length > 4) return word[..^2];
            if (word.EndsWith("'s")) return word[..^2];
            if (word.EndsWith("s") && word.Length > 3 && !word.EndsWith("ss")) return word[..^1];

            return word;
        }

        // ── Fuzzy Matching ────────────────────────────────────────────

        /// <summary>
        /// Compute bounded Levenshtein edit distance between two strings.
        /// Uses O(min(m,n)) space with stackalloc for small strings to avoid
        /// heap allocations. Returns early when the minimum possible distance
        /// in any row exceeds <paramref name="maxDistance"/>, giving a
        /// significant speedup for the common case where strings are dissimilar.
        /// </summary>
        /// <param name="a">First string.</param>
        /// <param name="b">Second string.</param>
        /// <param name="maxDistance">
        /// Upper bound — if the true distance exceeds this value, the method
        /// returns <paramref name="maxDistance"/> + 1 without computing the
        /// exact result. Pass <see cref="int.MaxValue"/> for unbounded.
        /// </param>
        internal static int LevenshteinDistance(string a, string b, int maxDistance = int.MaxValue)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            // Quick length-difference check
            if (Math.Abs(a.Length - b.Length) > maxDistance)
                return maxDistance + 1;

            // Ensure a is the shorter string for space optimization
            if (a.Length > b.Length)
                (a, b) = (b, a);

            int m = a.Length, n = b.Length;

            // stackalloc for strings up to ~128 chars (typical search terms
            // are much shorter) — avoids GC pressure during fuzzy matching
            const int StackLimit = 128;
            int rowSize = m + 1;
            Span<int> prev = rowSize <= StackLimit ? stackalloc int[rowSize] : new int[rowSize];
            Span<int> curr = rowSize <= StackLimit ? stackalloc int[rowSize] : new int[rowSize];

            for (int i = 0; i <= m; i++) prev[i] = i;

            for (int j = 1; j <= n; j++)
            {
                curr[0] = j;
                int rowMin = j; // track minimum value in this row for early exit
                char bj = b[j - 1];

                for (int i = 1; i <= m; i++)
                {
                    int cost = a[i - 1] == bj ? 0 : 1;
                    int val = Math.Min(
                        Math.Min(curr[i - 1] + 1, prev[i] + 1),
                        prev[i - 1] + cost);
                    curr[i] = val;
                    if (val < rowMin) rowMin = val;
                }

                // If every cell in this row exceeds maxDistance, the final
                // result will too — no point continuing
                if (rowMin > maxDistance)
                    return maxDistance + 1;

                // Swap rows (copy-free via Span reassignment)
                var tmp = prev;
                prev = curr;
                curr = tmp;
            }

            return prev[m];
        }

        // ── Query Expansion ──────────────────────────────────────────

        /// <summary>
        /// Expand query terms with synonyms. Returns a dictionary of term → weight,
        /// where original terms have weight 1.0 and synonym expansions have weight 0.3.
        /// </summary>
        internal Dictionary<string, double> ExpandQueryWithSynonyms(List<string> queryTerms)
        {
            var expanded = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var term in queryTerms)
            {
                // Original term always gets full weight
                expanded[term] = 1.0;

                // Look up synonyms for the stemmed term
                if (SynonymMap.TryGetValue(term, out var synonyms))
                {
                    foreach (var syn in synonyms)
                    {
                        if (!expanded.ContainsKey(syn))
                            expanded[syn] = 0.3;
                    }
                }
            }

            return expanded;
        }

        /// <summary>
        /// Register custom synonyms for query expansion. Both directions are added.
        /// Terms are stemmed before storage.
        /// </summary>
        /// <param name="term">The term to add synonyms for.</param>
        /// <param name="synonyms">Related terms.</param>
        public void AddSynonyms(string term, params string[] synonyms)
        {
            if (string.IsNullOrWhiteSpace(term) || synonyms == null || synonyms.Length == 0)
                return;

            var stemmedTerm = Stem(term.ToLowerInvariant());
            var stemmedSynonyms = synonyms
                .Select(s => Stem(s.ToLowerInvariant()))
                .Where(s => s != stemmedTerm && s.Length >= 2)
                .ToArray();

            if (stemmedSynonyms.Length == 0) return;

            // Add forward direction
            if (SynonymMap.TryGetValue(stemmedTerm, out var existing))
            {
                var combined = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
                foreach (var s in stemmedSynonyms) combined.Add(s);
                SynonymMap[stemmedTerm] = combined.ToArray();
            }
            else
            {
                SynonymMap[stemmedTerm] = stemmedSynonyms;
            }

            // Add reverse direction
            foreach (var syn in stemmedSynonyms)
            {
                if (SynonymMap.TryGetValue(syn, out var revExisting))
                {
                    var combined = new HashSet<string>(revExisting, StringComparer.OrdinalIgnoreCase);
                    combined.Add(stemmedTerm);
                    SynonymMap[syn] = combined.ToArray();
                }
                else
                {
                    SynonymMap[syn] = new[] { stemmedTerm };
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void AddFieldTerms(
            List<string> allTerms,
            Dictionary<string, HashSet<string>> fieldMap,
            string fieldName,
            string text,
            double boost)
        {
            var tokens = Tokenize(text);
            if (!fieldMap.ContainsKey(fieldName))
                fieldMap[fieldName] = new HashSet<string>(StringComparer.Ordinal);

            foreach (var token in tokens)
            {
                fieldMap[fieldName].Add(token);
                // Apply boost by adding the term multiple times
                int boostCount = Math.Max(1, (int)Math.Round(boost));
                for (int i = 0; i < boostCount; i++)
                    allTerms.Add(token);
            }
        }

        private List<string> GetMatchedFields(string docName, List<string> queryTerms)
        {
            var fields = new List<string>();
            if (!_fieldTerms.TryGetValue(docName, out var fieldMap))
                return fields;

            foreach (var kvp in fieldMap)
            {
                bool matched = false;
                foreach (var term in queryTerms)
                {
                    if (kvp.Value.Contains(term))
                    {
                        matched = true;
                        break;
                    }
                    // Prefix check only when exact match failed
                    foreach (var t in kvp.Value)
                    {
                        if (t.Length > term.Length && t.AsSpan().StartsWith(term.AsSpan(), StringComparison.Ordinal))
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (matched) break;
                }
                if (matched)
                    fields.Add(kvp.Key);
            }

            return fields;
        }

    }

    /// <summary>
    /// Statistics about the search index.
    /// </summary>
    public class SearchIndexStats
    {
        /// <summary>Total number of indexed documents.</summary>
        public int TotalDocuments { get; set; }

        /// <summary>Number of unique terms in the index vocabulary.</summary>
        public int VocabularySize { get; set; }

        /// <summary>Average document length in terms.</summary>
        public double AverageDocumentLength { get; set; }

        /// <summary>Total term occurrences across all documents.</summary>
        public int TotalTermOccurrences { get; set; }

        /// <summary>Length of the longest indexed document.</summary>
        public int LongestDocument { get; set; }

        /// <summary>Length of the shortest indexed document.</summary>
        public int ShortestDocument { get; set; }

        /// <summary>Top 10 most frequent terms (term, document frequency).</summary>
        public IReadOnlyList<(string Term, int Frequency)> MostCommonTerms { get; set; }
            = Array.Empty<(string, int)>();
    }
}
