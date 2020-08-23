using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetVault.Attributes
{
    /// <summary>
    /// There is currently a flaw in analyzer where when ref structs symbols are loaded
    /// into analyzer from metadata rather than from source, they are not recognized as ref structs.
    ///
    /// This makes testing of several analyzer rules difficult because we cannot tell which
    /// types are ref structs.
    ///
    /// We will therefore annotate all the ref structs in this library with this attribute
    /// so that analyzer when it loads this project from meta data will have a way to recognize
    /// the type as a ref struct.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class RefStructAttribute : Attribute
    {
    }
}
