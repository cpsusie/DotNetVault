using System;
using System.Collections.Immutable;
using DotNetVault.Attributes;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotNetVault.Interfaces
{
    internal interface IDelegateNoNonVsCaptureAnalyzer
    {
        /// <summary>
        /// Scan the analysis context to 
        ///     1-  determine whether the operation involves the creation of a delegate
        ///         annotated with the <see cref="NoNonVsCaptureAttribute"/> attribute and, if so
        ///     2-  gather the information needed to perform compliance analysis
        /// </summary>
        /// <param name="con">the operation analysis context.  for efficiency purposes, it is best to only supply a context
        ///  whose <see cref="OperationAnalysisContext.Operation"/> property returns an object of type <see cref="IDelegateCreationOperation"/>:
        ///  if not, no further analysis is needed or required.  </param>
        /// <returns>A value tuple whose first value is true if further analysis is required or false otherwise.  If true, the other
        /// values in the tuple are guaranteed not to be null.  If false, the other values are undefined and may well be null.</returns>
        /// <exception cref="OperationCanceledException"></exception>
        (bool IdentifiedAttribute, IDelegateCreationOperation CreationOp,
            INamedTypeSymbol CreationOpType, IOperation Target) 
            ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData(OperationAnalysisContext con);

        /// <summary>
        /// Scan the specified delegate creation operation <paramref name="delCrOp"/> to ensure it complies with rules associated with
        /// delegates annotated with the <seealso cref="NoNonVsCaptureAttribute"/> attribute.  All the parameters passed should be retrieval
        /// results for a call to the <see cref="ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData"/> whose IdentifiedAttribute value is true.
        /// </summary>
        /// <param name="targetOp">The target operation as retrieved by the <see cref="ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData"/> method.</param>
        /// <param name="delCrOp">The delegate creation operation as retrieved by the <see cref="ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData"/> method.</param>
        /// <param name="assignee">The named type symbol as retrieved by the <see cref="ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData"/> method</param>
        /// <param name="con">the operation analysis context</param>
        /// <returns>A value tuple.  The first value will be: true if operation is known to be compliant, false if operation is known to be non-compliant and
        /// null if its compliance vel-non is unknown.  If false, NonVsCaptures will contain all offending type symbols that are not vault safe and NonVsCapture text
        /// (the arrays are to be considered parallel) will contain brief text explaining the reason for that symbol's non compliance.</returns>
        /// <exception cref="ArgumentNullException">one of the parameters was null.</exception>
        /// <exception cref="OperationCanceledException">analysis was cancelled</exception>
        (bool? IsCompliant, ImmutableArray<ITypeSymbol> NonVsCaptures, ImmutableArray<string> NonVsCaptureText)
            AnalyzeOperationForCompliance([NotNull] IOperation targetOp, [NotNull] IDelegateCreationOperation delCrOp,
                [NotNull] INamedTypeSymbol assignee, OperationAnalysisContext con);
    }
}
