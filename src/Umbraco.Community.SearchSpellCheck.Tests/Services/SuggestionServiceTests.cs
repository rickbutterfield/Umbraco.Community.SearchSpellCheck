using Microsoft.AspNetCore.Hosting;
using Moq;
using Umbraco.Community.SearchSpellCheck.Models;
using Umbraco.Community.SearchSpellCheck.Services;

namespace Umbraco.Community.SearchSpellCheck.Tests.Services
{
    [TestFixture]
    public class SuggestionServiceTests
    {
        private const string WordField = "word";
        private const string NodeNameField = "nodeName";

        private static SuggestionService CreateService(TestLuceneReaderAccessor accessor)
            => new(accessor, Mock.Of<IWebHostEnvironment>());

        #region GetSuggestion

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GetSuggestion_Returns_Empty_For_A_Missing_Search_Term(string? searchTerm)
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion(searchTerm!), Is.Empty);
        }

        [Test]
        public void GetSuggestion_Corrects_A_Misspelt_Word()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house", "garden", "kitchen");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion("hous", suggestionAccuracy: 0.25f), Is.EqualTo("house"));
        }

        [Test]
        public void GetSuggestion_Keeps_Words_It_Has_No_Suggestion_For()
        {
            // Lucene's SpellChecker uses SuggestMode.SUGGEST_WHEN_NOT_IN_INDEX, so a correctly spelt word returns
            // no candidates at all. The previous implementation only added a word to the result when it had a
            // suggestion, which meant every correctly spelt word was silently dropped from the phrase. Searching
            // "red hous" returned "house" rather than "red house".
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "red", "house", "garden");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion("red hous", suggestionAccuracy: 0.25f), Is.EqualTo("red house"));
        }

        [Test]
        public void GetSuggestion_Returns_Empty_When_Nothing_Was_Corrected()
        {
            // The documented usage is "if (!string.IsNullOrEmpty(Model.SpellCheck))". Echoing the search term back
            // unchanged made that always true, so sites offered "Did you mean <exactly what you typed>?".
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "red", "house");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion("red house", suggestionAccuracy: 0.25f), Is.Empty);
        }

        [Test]
        public void GetSuggestion_Keeps_A_Word_Whose_Best_Candidate_Is_Below_The_Threshold()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house", "garden");
            using SuggestionService service = CreateService(accessor);

            // Nothing in the index resembles this, and an impossible threshold rules out whatever is returned.
            Assert.That(service.GetSuggestion("zzzqqq", suggestionAccuracy: 0.99f), Is.Empty);
        }

        [Test]
        public void GetSuggestion_Collapses_Repeated_Spaces_Rather_Than_Suggesting_For_Empty_Words()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "red", "house");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion("red   hous", suggestionAccuracy: 0.25f), Is.EqualTo("red house"));
        }

        [Test]
        public void GetSuggestion_Returns_Empty_When_The_Index_Is_Unavailable()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.Unavailable();
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestion("hous"), Is.Empty);
        }

        #endregion

        #region GetSuggestions

        [Test]
        public void GetSuggestions_Returns_Candidates_Best_First()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house", "hose", "mouse", "garden");
            using SuggestionService service = CreateService(accessor);

            IReadOnlyList<Suggestion> suggestions = service.GetSuggestions("hous");

            Assert.That(suggestions, Is.Not.Empty);
            Assert.That(suggestions.Select(x => x.Priority), Is.Ordered.Descending);
        }

        [Test]
        public void GetSuggestions_Also_Draws_On_Node_Names()
        {
            // Node names are a second dictionary source. Suggestions sourced from them previously always scored a
            // document frequency of zero, because only the word field was counted.
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithFields(new Dictionary<string, string[]>
            {
                [WordField] = new[] { "garden" },
                [NodeNameField] = new[] { "kitchen" }
            });
            using SuggestionService service = CreateService(accessor);

            IReadOnlyList<Suggestion> suggestions = service.GetSuggestions("kitchn");

            Assert.That(suggestions.Select(x => x.Word), Does.Contain("kitchen"));
            Assert.That(suggestions.Single(x => x.Word == "kitchen").Frequency, Is.GreaterThan(0));
        }

        [Test]
        public void GetSuggestions_Reads_Culture_Specific_Fields()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords("word_da-dk", "kobenhavn");
            using SuggestionService service = CreateService(accessor);

            Assert.Multiple(() =>
            {
                Assert.That(service.GetSuggestions("kobenhavv", culture: "da-DK").Select(x => x.Word), Does.Contain("kobenhavn"));
                Assert.That(service.GetSuggestions("kobenhavv"), Is.Empty, "The invariant field holds nothing.");
            });
        }

        [TestCase(null)]
        [TestCase("")]
        public void GetSuggestions_Returns_Empty_For_A_Missing_Word(string? word)
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house");
            using SuggestionService service = CreateService(accessor);

            Assert.That(service.GetSuggestions(word!), Is.Empty);
        }

        #endregion

        #region Scoring

        [Test]
        public void Score_Stays_Within_Zero_And_One_However_Common_A_Word_Is()
        {
            // The previous formula divided frequency by 100 and averaged it with three metrics that each cap at 1,
            // so a word on 300 pages scored above 1 and the suggestionAccuracy threshold stopped meaning anything.
            var suggestion = new Suggestion
            {
                Word = "the",
                Frequency = 500_000,
                Jaro = 0.1f,
                Leven = 0.1f,
                NGram = 0.1f
            };

            Assert.That(SuggestionService.Score(suggestion), Is.InRange(0f, 1f));
        }

        [Test]
        public void Score_Prefers_A_Close_Match_Over_A_Distant_But_Very_Common_One()
        {
            var closeButRare = new Suggestion { Word = "house", Frequency = 1, Jaro = 0.95f, Leven = 0.9f, NGram = 0.9f };
            var distantButCommon = new Suggestion { Word = "the", Frequency = 100_000, Jaro = 0.2f, Leven = 0.1f, NGram = 0.1f };

            Assert.That(SuggestionService.Score(closeButRare), Is.GreaterThan(SuggestionService.Score(distantButCommon)));
        }

        [Test]
        public void Score_Uses_Frequency_To_Separate_Equally_Close_Candidates()
        {
            var common = new Suggestion { Word = "house", Frequency = 400, Jaro = 0.9f, Leven = 0.9f, NGram = 0.9f };
            var rare = new Suggestion { Word = "hoose", Frequency = 1, Jaro = 0.9f, Leven = 0.9f, NGram = 0.9f };

            Assert.That(SuggestionService.Score(common), Is.GreaterThan(SuggestionService.Score(rare)));
        }

        [Test]
        public void Score_Handles_A_Suggestion_With_No_Metrics()
        {
            Assert.That(SuggestionService.Score(new Suggestion()), Is.EqualTo(0f));
        }

        #endregion

        #region Resource handling

        [Test]
        public void Repeated_Calls_Reuse_The_Cached_Spell_Checker()
        {
            // Building the checker reads every term in the index and writes an n-gram index for them. The previous
            // implementation did that on every call. The reader is still opened per call; the expensive part is not.
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house", "garden", "kitchen");
            using SuggestionService service = CreateService(accessor);

            for (var i = 0; i < 5; i++)
            {
                service.GetSuggestions("hous");
            }

            Assert.That(accessor.ReadCount, Is.EqualTo(5), "One reader per call is expected.");
            Assert.That(service.GetSuggestions("hous"), Is.Not.Empty, "The cached checker still answers.");
        }

        [Test]
        public void Dispose_Is_Safe_To_Call_Twice()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house");
            var service = CreateService(accessor);

            service.GetSuggestions("hous");

            Assert.DoesNotThrow(() =>
            {
                service.Dispose();
                service.Dispose();
            });
        }

        [Test]
        public void Using_A_Disposed_Service_Fails_Loudly()
        {
            using TestLuceneReaderAccessor accessor = TestLuceneReaderAccessor.WithWords(WordField, "house");
            var service = CreateService(accessor);
            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => service.GetSuggestions("hous"));
        }

        #endregion
    }
}
