using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Umbraco.Community.SearchSpellCheck.Interfaces;
using Directory = Lucene.Net.Store.Directory;

namespace Umbraco.Community.SearchSpellCheck.Tests.Services
{
    /// <summary>
    ///     An in-memory stand in for the real Examine backed accessor, so the suggestion behaviour can be exercised
    ///     against a genuine Lucene index rather than a mock.
    /// </summary>
    internal sealed class TestLuceneReaderAccessor : ILuceneReaderAccessor, IDisposable
    {
        private readonly Directory? _directory;

        private TestLuceneReaderAccessor(Directory? directory) => _directory = directory;

        /// <summary>How many times a reader has been requested. Used to prove the spell checker is cached.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Stands in for the index not existing yet, or IndexName pointing at nothing.</summary>
        public static TestLuceneReaderAccessor Unavailable() => new(null);

        /// <summary>Builds an index holding <paramref name="words" /> in the given field.</summary>
        public static TestLuceneReaderAccessor WithWords(string field, params string[] words)
            => WithFields(new Dictionary<string, string[]> { [field] = words });

        /// <summary>Builds an index where each entry is a field name and the words to put in it.</summary>
        public static TestLuceneReaderAccessor WithFields(IDictionary<string, string[]> fields)
        {
            var directory = new RAMDirectory();
            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);

            using (var writer = new IndexWriter(directory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer)))
            {
                foreach (KeyValuePair<string, string[]> field in fields)
                {
                    foreach (var word in field.Value)
                    {
                        var document = new Document { new TextField(field.Key, word, Field.Store.YES) };
                        writer.AddDocument(document);
                    }
                }

                writer.Commit();
            }

            return new TestLuceneReaderAccessor(directory);
        }

        public T Read<T>(Func<IndexReader, T> read, T valueIfUnavailable)
        {
            if (_directory is null)
            {
                return valueIfUnavailable;
            }

            ReadCount++;
            using DirectoryReader reader = DirectoryReader.Open(_directory);
            return read(reader);
        }

        public void Dispose() => _directory?.Dispose();
    }
}
