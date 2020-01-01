using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace DotNetVault.Interfaces
{
    /// <summary>
    /// Utilities implementing this interface are used to parse the
    /// contents of text files that contain type names
    /// </summary>
    public interface ITypeListFileParser
    {
        /// <summary>
        /// Parses the contents of text files that contains type names
        /// </summary>
        /// <param name="parseMe">the content of the text file</param>
        /// <returns>An enumeration of type names</returns>
        /// <exception cref="ArgumentNullException"><paramref name="parseMe"/> was null.</exception>
        IEnumerable<string> ParseContents([NotNull] string parseMe);
    }
}
