using System;
using System.Collections.Generic;
using System.Text;
using DotNetVault.Vaults;
using Xunit;

namespace VaultUnitTests
{
    public class SubstringMatcherUnitTests
    {
        private IEnumerable<(string BasisString, string SubString)> StringPairs
        {
            get
            {
                yield return ("xxxoooxxoCxxoo", "C");
                yield return ("xxxoooxxoxxooC", "C");
                yield return ("Cxxxoooxxoxxoo", "C");
                yield return ("xxxoooxxoxxoo", "C");
                yield return ("xxxoooxxoCLORTONxxoo", "CLORTON");
                yield return ("xxxoooxxoxxooCLORTON", "CLORTON");
                yield return ("CLORTONxxxoooxxoxxoo", "CLORTON");
                yield return ("xxxoooxxoxxoo", "CLORTON");

            }
        }

        [Fact]
        public void TestFinder()
        {
            foreach (var finder in StringPairs)
            {
                ValidateFinder(finder.BasisString, finder.SubString);
            }
        }

        private static void ValidateFinder(string str, string substr)
        {
            StringBuilder sb = new StringBuilder(str);
            int firstIdxCtrl = str.IndexOf(substr, StringComparison.Ordinal);
            SubStringMatcher matcher = SubStringMatcher.CreateMatcher(substr);
            int firstIndex = -1;
            for (int i = 0; i < sb.Length; ++i)
            {
                matcher.FeedChar(sb[i]);
                if (matcher.IsMatch)
                {
                    firstIndex = i - (matcher.Length - 1);
                }
            }
            Assert.True(firstIndex == firstIdxCtrl);
        }

    }
}
