using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search.Spell;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.AspNetCore.Hosting;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Community.SearchSpellCheck.Interfaces;
using Umbraco.Community.SearchSpellCheck.Models;

namespace Umbraco.Community.SearchSpellCheck.Services
{
    public class SuggestionService : ISuggestionService, IDisposable
    {
        /// <summary>
        ///     How much of the score comes from how closely the candidate resembles the typed word, and how much from
        ///     how often it appears in the site.
        /// </summary>
        /// <remarks>
        ///     Resemblance dominates deliberately. Frequency only separates candidates that are otherwise equally
        ///     close, which is the job it can actually do well.
        /// </remarks>
        private const float DistanceWeight = 0.85f;
        private const float FrequencyWeight = 0.15f;

        /// <summary>
        ///     Document frequency at which the frequency part of the score reaches its maximum.
        /// </summary>
        /// <remarks>
        ///     The previous formula was <c>(frequency / 100f + jaro + leven + ngram) / 4f</c>. Document frequency is
        ///     unbounded, so a word appearing on 300 pages contributed 3.0 against three metrics that each cap at 1.0.
        ///     Common words therefore beat close matches, and the resulting score could exceed 1, which made the
        ///     <c>suggestionAccuracy</c> threshold meaningless. Saturating on a log curve keeps the total within 0..1.
        /// </remarks>
        private const double FrequencySaturation = 1000d;

        private readonly ILuceneReaderAccessor _readerAccessor;
        private readonly IWebHostEnvironment _webHostEnvironment;

        /// <summary>
        ///     One cached spell checker per field set, rebuilt only when the index has moved on.
        /// </summary>
        /// <remarks>
        ///     Building the checker reads every term in the index and writes an n-gram index for them. The previous
        ///     implementation did that on every single call, along with opening a directory, a reader, a RAMDirectory
        ///     and a SpellChecker that were never disposed. On a busy search page that leaked file handles and made
        ///     each search progressively slower.
        /// </remarks>
        private readonly Dictionary<string, CachedSpellChecker> _spellCheckers = new();
        private readonly object _spellCheckerLock = new();
        private bool _disposed;

        public SuggestionService(
            ILuceneReaderAccessor readerAccessor,
            IWebHostEnvironment webHostEnvironment)
        {
            _readerAccessor = readerAccessor;
            _webHostEnvironment = webHostEnvironment;
        }

        /// <inheritdoc />
        public string GetSuggestion(
            string searchTerm,
            int numberOfSuggestions = 10,
            float suggestionAccuracy = 0.75f,
            string? culture = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return string.Empty;
            }

            var words = searchTerm.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var corrected = new List<string>(words.Length);
            var anyWordChanged = false;

            foreach (var word in words)
            {
                Suggestion? best = GetSuggestions(word, numberOfSuggestions, culture).FirstOrDefault();

                // Keep the visitor's word unless we have something better to offer. Previously a word with no
                // suggestion at all was skipped entirely, so the phrase came back missing words.
                if (best?.Word is null || best.Priority <= suggestionAccuracy)
                {
                    corrected.Add(word);
                    continue;
                }

                corrected.Add(best.Word);
                anyWordChanged |= !string.Equals(best.Word, word, StringComparison.Ordinal);
            }

            // Returning the search term unchanged made the documented
            // "if (!string.IsNullOrEmpty(Model.SpellCheck))" check always true, so sites offered
            // "Did you mean <exactly what you typed>?".
            return anyWordChanged ? string.Join(" ", corrected) : string.Empty;
        }

        /// <inheritdoc />
        public IReadOnlyList<Suggestion> GetSuggestions(string word, int numberOfSuggestions = 10, string? culture = null)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return Array.Empty<Suggestion>();
            }

            var wordField = FieldName(Constants.Internals.FieldName, culture);
            var nodeNameField = FieldName(UmbracoExamineFieldNames.NodeNameFieldName, culture);

            return _readerAccessor.Read(
                reader => Suggest(reader, word, numberOfSuggestions, wordField, nodeNameField),
                Array.Empty<Suggestion>());
        }

        private IReadOnlyList<Suggestion> Suggest(
            IndexReader reader,
            string word,
            int numberOfSuggestions,
            string wordField,
            string nodeNameField)
        {
            SpellChecker checker = GetOrBuildSpellChecker(reader, wordField, nodeNameField);

            var jaro = new JaroWinklerDistance();
            var leven = new LuceneLevenshteinDistance();
            var ngram = new NGramDistance();

            string[] candidates;
            lock (_spellCheckerLock)
            {
                // SuggestSimilar reads the checker's own n-gram index, so it must not race a rebuild.
                candidates = checker.SuggestSimilar(word, numberOfSuggestions);
            }

            var suggestions = new List<Suggestion>(candidates.Length);

            foreach (var candidate in candidates)
            {
                // Count the candidate across both fields it could have come from. Previously only the word field
                // was counted, so anything sourced from node names always scored a frequency of zero.
                var frequency = reader.DocFreq(new Term(wordField, candidate))
                    + reader.DocFreq(new Term(nodeNameField, candidate));

                var suggestion = new Suggestion
                {
                    Word = candidate,
                    Frequency = frequency,
                    Jaro = jaro.GetDistance(candidate, word),
                    Leven = leven.GetDistance(candidate, word),
                    NGram = ngram.GetDistance(candidate, word)
                };

                suggestion.Priority = Score(suggestion);
                suggestions.Add(suggestion);
            }

            // Materialised on purpose. The result outlives the reader, and the previous lazy IOrderedEnumerable was
            // enumerated by callers after the reader had gone.
            return suggestions
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Word, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        ///     Scores a candidate from 0 to 1, so that <c>suggestionAccuracy</c> means what it says.
        /// </summary>
        internal static float Score(Suggestion suggestion)
        {
            var distance = ((suggestion.Jaro ?? 0f) + (suggestion.Leven ?? 0f) + (suggestion.NGram ?? 0f)) / 3f;

            var frequency = suggestion.Frequency.GetValueOrDefault();
            var frequencyScore = frequency <= 0
                ? 0f
                : (float)(Math.Log10(1 + frequency) / Math.Log10(1 + FrequencySaturation));

            return Math.Clamp((DistanceWeight * distance) + (FrequencyWeight * Math.Clamp(frequencyScore, 0f, 1f)), 0f, 1f);
        }

        private SpellChecker GetOrBuildSpellChecker(IndexReader reader, string wordField, string nodeNameField)
        {
            // DirectoryReader.Version changes on every commit, so it tells us when a rebuild or a publish has
            // invalidated the dictionary we cached.
            var version = reader is DirectoryReader directoryReader ? directoryReader.Version : -1L;
            var key = $"{wordField}|{nodeNameField}";

            lock (_spellCheckerLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_spellCheckers.TryGetValue(key, out CachedSpellChecker? cached) && cached.Version == version)
                {
                    return cached.Checker;
                }

                var checker = new SpellChecker(new RAMDirectory(), new JaroWinklerDistance());

                try
                {
                    var analyser = new StandardAnalyzer(LuceneVersion.LUCENE_48);
                    checker.IndexDictionary(new LuceneDictionary(reader, wordField), new IndexWriterConfig(LuceneVersion.LUCENE_48, analyser), true);
                    checker.IndexDictionary(new LuceneDictionary(reader, nodeNameField), new IndexWriterConfig(LuceneVersion.LUCENE_48, analyser), true);
                }
                catch
                {
                    checker.Dispose();
                    throw;
                }

                cached?.Dispose();
                _spellCheckers[key] = new CachedSpellChecker(checker, version);
                return checker;
            }
        }

        private static string FieldName(string field, string? culture)
            => string.IsNullOrWhiteSpace(culture) ? field : $"{field}_{culture.ToLowerInvariant()}";

        /// <inheritdoc />
        [Obsolete("Use GetSuggestions, which returns a materialised list. Scheduled for removal in 2.0.")]
        public IOrderedEnumerable<Suggestion> SuggestionData(string word, int numberOfSuggestions = 10, string? culture = null)
            => GetSuggestions(word, numberOfSuggestions, culture).OrderByDescending(x => x.Priority);

        /// <inheritdoc />
        [Obsolete("Scoring is an implementation detail and the weighting is no longer a plain average. Read Suggestion.Priority instead. Scheduled for removal in 2.0.")]
        public float? Priority(Suggestion metric) => Score(metric);

        /// <inheritdoc />
        [Obsolete("The index is now resolved through IExamineManager. Scheduled for removal in 2.0.")]
        public SimpleFSDirectory GetFileSystemLuceneDirectory(string indexName)
        {
            var dirInfo = new DirectoryInfo(
                Path.Combine(
                    _webHostEnvironment.MapPathContentRoot(Cms.Core.Constants.SystemDirectories.TempData),
                    "ExamineIndexes",
                    indexName));

            return new SimpleFSDirectory(dirInfo);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            lock (_spellCheckerLock)
            {
                foreach (CachedSpellChecker cached in _spellCheckers.Values)
                {
                    cached.Dispose();
                }

                _spellCheckers.Clear();
                _disposed = true;
            }
        }

        private sealed class CachedSpellChecker : IDisposable
        {
            public CachedSpellChecker(SpellChecker checker, long version)
            {
                Checker = checker;
                Version = version;
            }

            public SpellChecker Checker { get; }

            public long Version { get; }

            public void Dispose() => Checker.Dispose();
        }
    }
}
