using Lucene.Net.Index;

namespace Umbraco.Community.SearchSpellCheck.Interfaces
{
    /// <summary>
    ///     Gives scoped access to the live Lucene reader behind the spell check index.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The callback shape is deliberate. Every reader Examine hands out has to be released again, and returning
    ///         one to the caller makes it their job to remember. Passing a callback keeps acquire and release in one
    ///         place, so a caller cannot leak a reader by forgetting, or by returning a lazy LINQ chain that outlives
    ///         it.
    ///     </para>
    ///     <para>
    ///         This is an advanced extension point, exposed so that <see cref="ISuggestionService" /> can be pointed at
    ///         a different reader in tests or in a custom setup. Most consumers only need
    ///         <see cref="ISuggestionService" />.
    ///     </para>
    /// </remarks>
    public interface ILuceneReaderAccessor
    {
        /// <summary>
        ///     Runs <paramref name="read" /> against the index reader, or returns
        ///     <paramref name="valueIfUnavailable" /> if the index does not exist yet.
        /// </summary>
        T Read<T>(Func<IndexReader, T> read, T valueIfUnavailable);
    }
}
