using System;
using System.Collections.Generic;
using System.Linq;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using TypeNameParserFactory = System.Func<DotNetVault.Interfaces.ITypeListFileParser>;
namespace DotNetVault.UtilitySources
{
    /// <summary>
    /// A source for retrieving utilities of type ITypeListFileParser
    /// </summary>
    public static class TypeNameParserUtilityFactorySource
    {
        /// <summary>
        /// The factory instance.  If you want to use a different factory, you must use the <see cref="SupplyAlternateFactory"/>
        /// method BEFORE accessing this property.  Accessing without first supplying an alternate will use default factory.
        /// </summary>
        public static TypeNameParserFactory ParserFactory => TheTypeNameParser;

        /// <summary>
        /// Call to supply a different factory.
        /// </summary>
        /// <param name="alternate">The alternate factory</param>
        /// <remarks>The alternate factory must not return null.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="alternate"/> was null.</exception>
        /// <exception cref="InvalidOperationException">The factory has already been set -- either explicitly through this method
        /// or implicitly defaulted by prior access to the <see cref="ParserFactory"/> property.</exception>
        public static void SupplyAlternateFactory([NotNull] TypeNameParserFactory alternate)
        {
            if (alternate == null) throw new ArgumentNullException(nameof(alternate));
            bool setIt = TheTypeNameParser.SetToNonDefaultValue(alternate);
            if (!setIt)
            {
                throw new InvalidOperationException("The factory has already been set or defaulted.");
            }
        }

        private sealed class TypeNameParser : ITypeListFileParser
        {
            internal static ITypeListFileParser CreateParser() => new TypeNameParser();

            [ItemNotNull]
            [NotNull]
            public IEnumerable<string> ParseContents(string parseMe)
            {
                if (parseMe == null) throw new ArgumentNullException(nameof(parseMe));
                if (string.IsNullOrWhiteSpace(parseMe)) return Enumerable.Empty<string>();

                parseMe = parseMe.Trim();
                return Split(parseMe);

                static IEnumerable<string> Split(string p)
                {
                    var semiColonsSplit= p.Split(TheItemSeparators, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in semiColonsSplit)
                    {
                        var splitMore = item.Split(WhiteSpaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var token in splitMore.Where(str => !string.IsNullOrWhiteSpace(str)))
                        {
                            yield return token.Trim();
                        }
                    }
                }
            }

            private TypeNameParser() { }

            private static readonly string[] WhiteSpaceSeparators = {Environment.NewLine, "\t", " "};
            private static readonly char[] TheItemSeparators = {';'};
        }

        static TypeNameParserUtilityFactorySource() => TheTypeNameParser =
            new LocklessWriteOnce<TypeNameParserFactory>(() => TypeNameParser.CreateParser);

        private static readonly LocklessWriteOnce<TypeNameParserFactory> TheTypeNameParser;
    }
}
