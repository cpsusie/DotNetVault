using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.ExtensionMethods;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using DefaultDelNoVsCaptureAnalyzer = DotNetVault.UtilitySources.DelegateNoNonVsCaptureAnalyzerSource.DelegateNoNonVsCaptureAnalyzer;
namespace DotNetVault.UtilitySources
{
    using DefaultDelNoVsCaptureAnalyzerFactory = Func<DefaultDelNoVsCaptureAnalyzer>;
    using DelegateNoVsCaptureAnalyzerFactory = Func<IDelegateNoNonVsCaptureAnalyzer>;

    internal static class DelegateNoNonVsCaptureAnalyzerSource
    {
        internal static DefaultDelNoVsCaptureAnalyzerFactory DefaultFactoryInstance => TheDefaultFactory;

        public static DelegateNoVsCaptureAnalyzerFactory FactoryInstance => TheFactoryInstance;

        public static bool SupplyAlternateFactoryInstance([NotNull] DelegateNoVsCaptureAnalyzerFactory alternate) =>
            TheFactoryInstance.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        #region nested type
        internal struct DelegateNoNonVsCaptureAnalyzer : IDelegateNoNonVsCaptureAnalyzer
        {
            internal static IDelegateNoNonVsCaptureAnalyzer CreateInstance() => CreateDefaultInstance();

            internal static DelegateNoNonVsCaptureAnalyzer CreateDefaultInstance() => new DelegateNoNonVsCaptureAnalyzer();

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
            public (bool IdentifiedAttribute, IDelegateCreationOperation CreationOp, INamedTypeSymbol CreationOpType, IOperation Target)
                ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData(OperationAnalysisContext con)
            {
                bool foundAttribute;
                INamedTypeSymbol delCreateOpType = null;
                IDelegateCreationOperation delCreateOp = null;
                IOperation target = null;
                try
                {
                    delCreateOp = (IDelegateCreationOperation)con.Operation;
                    INamedTypeSymbol attributeSymbol = con.Compilation.FindNoNonVsCaptureAttribute();
                    con.CancellationToken.ThrowIfCancellationRequested();
                    if (delCreateOp.Type is INamedTypeSymbol nts && attributeSymbol != null)
                    {
                        delCreateOpType = nts;
                        bool tempFoundAttrib = delCreateOpType.DoesNamedTypeHaveAttribute(attributeSymbol);
                        target = delCreateOp.Target;
                        foundAttribute = tempFoundAttrib &&
                                         target != null;
                    }
                    else
                    {
                        foundAttribute = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    TraceLog.Log(e);
                    foundAttribute = false;
                }
                Debug.Assert(!foundAttribute || (delCreateOp != null && delCreateOpType != null && target != null));
                return (foundAttribute, delCreateOp, delCreateOpType, target);
            }

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
            public (bool? IsCompliant, ImmutableArray<ITypeSymbol> NonVsCaptures, ImmutableArray<string> NonVsCaptureText)
                AnalyzeOperationForCompliance(IOperation targetOp, IDelegateCreationOperation delCrOp, INamedTypeSymbol assignee,
                    OperationAnalysisContext con)
            {
                if (targetOp == null) throw new ArgumentNullException(nameof(targetOp));
                if (delCrOp == null) throw new ArgumentNullException(nameof(delCrOp));
                if (assignee == null) throw new ArgumentNullException(nameof(assignee));

                var nonVsCapturesFinal = ImmutableArray<ITypeSymbol>.Empty;
                var nonVsCaptureTextFinal = ImmutableArray<string>.Empty;
                var nonVsCaptures = nonVsCapturesFinal.ToBuilder();
                var nonVsCaptureText = nonVsCaptureTextFinal.ToBuilder();

                Debug.Assert(targetOp != null && delCrOp != null && assignee != null && con.Compilation != null);
                bool? isCompliant = true;

                var nodeExtractionResult = ExtractSyntaxNode(targetOp, con.CancellationToken);
                if (nodeExtractionResult.FoundIt)
                {
                    DebugLog.Log($"target op type: {targetOp.GetType().Name}; target op kind: {targetOp.Kind}");
                    SyntaxNode node = nodeExtractionResult.Node;
                    DebugLog.Log($"Syntax node found of kind: [{node.Kind()}], with text: [{node.GetText()}]");
                    
                    var semanticModel = con.Compilation.GetSemanticModel(node.SyntaxTree);
                    var dataFlowAnalysisRes = DataFlowAnalyzeNode(node, semanticModel, con.CancellationToken);
                    var nodeParameterSymbols = GetNodeParameterSymbols(node, semanticModel, con.CancellationToken);

                    ParameterSyntax protectedParameter = nodeParameterSymbols.First().Syntax;
                    string protectedParameterName = protectedParameter.Identifier.ValueText;

                    var scanForIllegalUseAsParameter =
                        ScanForIllegalParameterUsage(node, con, nodeParameterSymbols.First().Type, protectedParameterName);
                    if (scanForIllegalUseAsParameter.IllegalParameterUsageDetected)
                    {
                        string message = scanForIllegalUseAsParameter.explanation;
                        nonVsCapturesFinal = nonVsCapturesFinal.Add(nodeParameterSymbols.First().Type);
                        nonVsCaptureTextFinal = nonVsCaptureTextFinal.Add(message);
                        return (false, nonVsCapturesFinal,nonVsCaptureTextFinal);
                    }

                    ImmutableHashSet<string> nodeNameSet =
                        ImmutableHashSet<string>.Empty.Union(nodeParameterSymbols.Select(nps => nps.IdentifierName));
                    ImmutableHashSet<ISymbol> analyteSymbols = ImmutableHashSet<ISymbol>.Empty;
                    var readOrWrittenSymbols = analyteSymbols.ToBuilder();
                    if (dataFlowAnalysisRes.Success)
                    {
                        var dataFlowAnalysis = dataFlowAnalysisRes.Analysis;
                        readOrWrittenSymbols.UnionWith(dataFlowAnalysis.Captured);
                        readOrWrittenSymbols.UnionWith(dataFlowAnalysis.DataFlowsIn);
                        readOrWrittenSymbols.UnionWith(dataFlowAnalysis.WrittenInside);
                        readOrWrittenSymbols.UnionWith(
                            dataFlowAnalysisRes.IdentifiedStaticMembers.Select(tuple => tuple.StaticSymbol));
                        //ok we are not interested in parameter symbols
                        readOrWrittenSymbols.RemoveAll(symbol => nodeNameSet.Contains(symbol.Name));

                        
                        
                        var vaultSafetyAnalyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                        analyteSymbols = readOrWrittenSymbols.ToImmutable();
                        foreach (var symbol in analyteSymbols)
                        {
                            con.CancellationToken.ThrowIfCancellationRequested();
                            bool isThis = false;
                            ITypeSymbol typeSymbol;
                            AnalyteSymbolType symbolType;
                            switch (symbol)
                            {
                                case IParameterSymbol ps:
                                    typeSymbol = ps.Type;
                                    isThis = ps.IsThis;
                                    symbolType = AnalyteSymbolType.ParameterSymbol;
                                    break;
                                case ITypeSymbol ts:
                                    typeSymbol = ts;
                                    symbolType = AnalyteSymbolType.TypeSymbol;
                                    break;
                                case IFieldSymbol fs:
                                    typeSymbol = fs.Type;
                                    symbolType = AnalyteSymbolType.Field;
                                    break;
                                case IPropertySymbol prs:
                                    typeSymbol = prs.Type;
                                    symbolType = AnalyteSymbolType.Property;
                                    break;
                                case ILocalSymbol ls:
                                    symbolType = AnalyteSymbolType.Local;
                                    typeSymbol = ls.Type;
                                    break;
                                case null:
                                    const string failM = "Received a null analyte symbol!";
                                    DebugLog.Log(failM);
                                    Debug.Assert(false);
                                    throw new LogicErrorException(failM);
                                default:
                                    string logMessage =
                                         "Unknown symbol type encountered in analyteSymbols.  Type of symbol: " +
                                        $"{symbol.GetType().Name}, Kind of symbol: {symbol.Kind}, Name of symbol: {symbol.Name}";
                                    DebugLog.Log(logMessage);
                                    TraceLog.Log(logMessage);
                                    return (null, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<string>.Empty);
                            }

                            if (typeSymbol is IErrorTypeSymbol ets)
                            {
                                ITypeSymbol temp = ets.ResolveErrorTypeSymbol(con.Compilation);
                                if (temp != null) typeSymbol = temp;
                            }

                            bool? analysisRes;
                            string failureText;
                            switch (typeSymbol)
                            {
                                default:
                                case IErrorTypeSymbol _:
                                    analysisRes = null;
                                    failureText = string.Empty;
                                    break;
                                case INamedTypeSymbol nts:
                                    analysisRes = vaultSafetyAnalyzer.IsTypeVaultSafe(nts, con.Compilation);
                                    if (analysisRes == true)
                                    {
                                        failureText = string.Empty;
                                    }
                                    else
                                    {
                                        if (isThis)
                                        {
                                            failureText =
                                                 "The operation captures or directly or indirectly references " +
                                                $"the \"this\" parameter, but \"this\"'s type {nts.Name} is not vault-safe.";
                                        }
                                        else
                                        {
                                            switch (symbolType)
                                            {
                                                default:
                                                case AnalyteSymbolType.TypeSymbol:
                                                case AnalyteSymbolType.Invalid:
                                                    failureText =
                                                        $"The operation references an object of type {nts.Name}, " +
                                                         "which is not vault-safe.";
                                                    break;
                                                case AnalyteSymbolType.Field:
                                                    IFieldSymbol fs = (IFieldSymbol)symbol;
                                                    failureText =
                                                        $"The operation references the field {fs.Name} which is of type {nts.Name}," +
                                                         " a type that is not vault-safe.";
                                                    break;
                                                case AnalyteSymbolType.Property:
                                                    IPropertySymbol propSymb = (IPropertySymbol)symbol;
                                                    failureText =
                                                        $"The operation references the property {propSymb.Name} which is of" +
                                                        $" type {nts.Name}, a type that is not vault-safe.";
                                                    break;
                                                case AnalyteSymbolType.Local:
                                                    ILocalSymbol lSymb = (ILocalSymbol)symbol;
                                                    failureText =
                                                        $"The operation references the local {lSymb.Name} which is of " +
                                                        $"type {nts.Name}, a type that is not vault-safe.";
                                                    break;
                                                case AnalyteSymbolType.ParameterSymbol:
                                                    IParameterSymbol paramSymbol = (IParameterSymbol)symbol;
                                                    failureText =
                                                        $"The operation references the parameter {paramSymbol.Name} " +
                                                        $"which is of type {nts.Name}, a type that is not vault safe.";
                                                    break;
                                            }
                                        }
                                    }
                                    break;
                                case IDynamicTypeSymbol dts:
                                    analysisRes = false;
                                    failureText =
                                        $"The operation references an object of dynamic type called {dts.Name}." +
                                        $"  Dynamic types are inherently not vault-safe.";
                                    break;
                                case IPointerTypeSymbol pts:
                                    analysisRes = false;
                                    failureText =
                                        $"The operation references a pointer symbol {pts.Name}." +
                                        $"  Pointer access is considered not to be vault-safe.";
                                    break;
                                case ITypeParameterSymbol tps:
                                    analysisRes =
                                        vaultSafetyAnalyzer.AnalyzeTypeParameterSymbolForVaultSafety(tps,
                                            con.Compilation);
                                    failureText = analysisRes == true ? string.Empty :
                                        $"The operation references an object specified by the type parameter {tps.Name}, " +
                                         "but this type parameter is not vault-safe";
                                    break;
                                case IArrayTypeSymbol ats:
                                    analysisRes = false;
                                    failureText =
                                        $"The operation references the array {ats.Name}." +
                                        "  Arrays are fundamentally not vault-safe.";
                                    break;
                            }

                            if (analysisRes == null && isCompliant != false)
                            {
                                isCompliant = null;
                            }
                            if (analysisRes == false)
                            {
                                isCompliant = false;
                                nonVsCaptures.Add(typeSymbol);
                                nonVsCaptureText.Add(failureText);
                            }
                        }

                        nonVsCaptures.Capacity = nonVsCaptures.Count;
                        nonVsCaptureText.Capacity = nonVsCaptureText.Count;
                        nonVsCaptureTextFinal = nonVsCaptureText.MoveToImmutable();
                        nonVsCapturesFinal = nonVsCaptures.MoveToImmutable();
                        return (isCompliant, nonVsCapturesFinal, nonVsCaptureTextFinal);
                    }

                    DebugLog.Log(PrintSymbolsCapturedInRegion(readOrWrittenSymbols));
                    return (null, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<string>.Empty);
                }

                DebugLog.Log($"target op type: {targetOp.GetType().Name}; target op kind: {targetOp.Kind}");
                DebugLog.Log("Unable to find syntax node...");
                return (null, ImmutableArray<ITypeSymbol>.Empty, ImmutableArray<string>.Empty);
            }

            private (bool IllegalParameterUsageDetected, IdentifierNameSyntax IdentifierSyntax, string explanation) ScanForIllegalParameterUsage([NotNull] SyntaxNode node,
                OperationAnalysisContext con, [NotNull] ITypeSymbol ts, [NotNull] string protectedParameterName)
            {
                if (node == null) throw new ArgumentNullException(nameof(node));
                if (ts == null) throw new ArgumentNullException(nameof(ts));
                if (protectedParameterName == null) throw new ArgumentNullException(nameof(protectedParameterName));
                if (protectedParameterName == null) throw new ArgumentNullException(nameof(protectedParameterName));
                
                bool illegalParameterUsageDetected = false;
                IdentifierNameSyntax identifierSyntax = null;
                string explanation = string.Empty;
                
                foreach (var n in
                    node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    var model = con.Compilation.GetSemanticModel(node.SyntaxTree, true);
                    var symbol = model.GetSymbolInfo(n);
                    if (symbol.Symbol is IMethodSymbol m && m.IsExtensionMethod)
                    {

                        IParameterSymbol firstParam = m.ReducedFrom?.Parameters.FirstOrDefault();
                        if (firstParam != null && SymbolEqualityComparer.Default.Equals(firstParam.Type, ts))
                        {
                            var invocationTarget = n.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
                                .FirstOrDefault();
                            identifierSyntax = invocationTarget?.Expression.DescendantNodesAndSelf()
                                .OfType<IdentifierNameSyntax>()
                                .FirstOrDefault();
                            if (identifierSyntax?.Identifier.ValueText == protectedParameterName)
                            {
                                bool isVaultSafe = ExecuteVsAnalysis();
                                if (isVaultSafe)
                                {
                                    if (firstParam.RefKind != RefKind.None && firstParam.RefKind != RefKind.In)
                                    {
                                        illegalParameterUsageDetected = true;
                                        explanation =
                                            $"The protected type parameter with name {identifierSyntax.Identifier.ValueText} " +
                                            "is vault-safe, but even vault safe parameters may not be passed by reference.";
                                    }
                                    else
                                    {
                                        explanation = string.Empty;
                                    }
                                }
                                else
                                {
                                    illegalParameterUsageDetected = true;
                                    explanation =
                                        $"The protected type parameter with name {identifierSyntax.Identifier.ValueText} " +
                                        "may not be passed outside of the delegate.";
                                }
                            }
                        }
                    }
                    if (illegalParameterUsageDetected)
                        break;
                    foreach (ArgumentSyntax item in n.ArgumentList.Arguments)
                    {
                        IdentifierNameSyntax ins = item.NameColon != null
                            ? item.NameColon.Name
                            : item.Expression as IdentifierNameSyntax;
                        if (ins?.Identifier.ValueText == protectedParameterName)
                        {
                            bool isVaultSafe = ExecuteVsAnalysis();
                            if (isVaultSafe)
                            {
                                if (item.RefKindKeyword != default || item.RefOrOutKeyword != default)
                                {
                                    illegalParameterUsageDetected = true;
                                    explanation =
                                        $"The protected type parameter with name {ins.Identifier.ValueText} " +
                                        "is vault-safe, but even vault safe parameters may not be passed by reference.";
                                    identifierSyntax = ins;
                                }
                                else
                                {
                                    explanation = string.Empty;
                                }
                            }
                            else
                            {
                                illegalParameterUsageDetected = true;
                                explanation =
                                    $"The protected type parameter with name {ins.Identifier.ValueText} " +
                                    "may not be passed outside of the delegate.";
                                identifierSyntax = ins;
                            }
                        }
                        if (illegalParameterUsageDetected)
                            break;

                    }

                }
                return (illegalParameterUsageDetected, identifierSyntax, explanation);
                bool ExecuteVsAnalysis()
                {
                    bool isVaultSafe;
                    var vaultSafetyAnalyzer = VaultSafeAnalyzerFactorySource.CreateAnalyzer();
                    switch (ts)
                    {
                        case INamedTypeSymbol nts:
                            isVaultSafe = vaultSafetyAnalyzer.IsTypeVaultSafe(nts, con.Compilation,
                                con.CancellationToken);
                            break;
                        default:
                            isVaultSafe = false;
                            break;
                    }

                    return isVaultSafe;
                }

             
            }

            private static (bool Success, DataFlowAnalysis Analysis, ImmutableHashSet<FindSymbolResult> IdentifiedStaticMembers)
                DataFlowAnalyzeNode(SyntaxNode n, SemanticModel m, CancellationToken tkn)
            {
                var fieldAndPropertyFinder = StaticMemberSymbolIdentifierSource.DefaultFactory();
                ImmutableHashSet<FindSymbolResult> identifiedStaticMembers;
                bool success;
                DataFlowAnalysis analysis;
                try
                {
                    switch (n)
                    {
                        case MethodDeclarationSyntax mds:
                            Debug.Assert(mds.ExpressionBody?.Expression != null ||
                                         (mds.Body != null && mds.Body.Statements.Any()));
                            if (mds.ExpressionBody?.Expression != null)
                            {
                                analysis = m.AnalyzeDataFlow(mds.ExpressionBody.Expression);
                                identifiedStaticMembers =
                                    fieldAndPropertyFinder.FindPropertyOrFieldSymbols(m, mds.ExpressionBody.Expression,
                                        tkn);
                            }
                            else
                            {
                                analysis = m.AnalyzeDataFlow(mds.Body.Statements.First(), mds.Body.Statements.Last());
                                identifiedStaticMembers =
                                    fieldAndPropertyFinder.FindPropertyOrFieldSymbols(m, mds.Body.Statements, tkn);
                            }
                            success = analysis?.Succeeded == true;
                            break;
                        case LambdaExpressionSyntax lambdaExpressionSyntax:
                            analysis = m.AnalyzeDataFlow(lambdaExpressionSyntax);
                            identifiedStaticMembers =
                                fieldAndPropertyFinder.FindPropertyOrFieldSymbols(m, lambdaExpressionSyntax, tkn);
                            success = analysis?.Succeeded == true;
                            break;
                        case ExpressionStatementSyntax exprStateSyn:
                            analysis = m.AnalyzeDataFlow(exprStateSyn);
                            identifiedStaticMembers =
                                fieldAndPropertyFinder.FindPropertyOrFieldSymbols(m, exprStateSyn, tkn);
                            success = analysis?.Succeeded == true;
                            break;
                        case ExpressionSyntax exprSyntax:
                            analysis = m.AnalyzeDataFlow(exprSyntax);
                            identifiedStaticMembers =
                                fieldAndPropertyFinder.FindPropertyOrFieldSymbols(m, exprSyntax, tkn);
                            success = analysis?.Succeeded == true;
                            break;
                        case null:
                            DebugLog.Log("Received null syntax node for data flow analysis.");
                            success = false;
                            analysis = null;
                            identifiedStaticMembers = ImmutableHashSet<FindSymbolResult>.Empty;
                            break;
                        default:
                            DebugLog.Log($"Unrecognized syntax node received for data flow analysis." +
                                         $"  Type: {n.GetType().Name}");
                            TraceLog.Log($"Unrecognized syntax node received for data flow analysis." +
                                         $"  Type: {n.GetType().Name}");
                            success = false;
                            analysis = null;
                            identifiedStaticMembers = ImmutableHashSet<FindSymbolResult>.Empty;
                            break;
                    }
                }
                catch (Exception e)
                {
                    TraceLog.Log(e);
                    success = false;
                    analysis = null;
                    identifiedStaticMembers = ImmutableHashSet<FindSymbolResult>.Empty;
                }
                return (success, analysis, identifiedStaticMembers);

            }

            private static string PrintSymbolsCapturedInRegion(IEnumerable<ISymbol> symbols)
            {
                var sb = new StringBuilder($"Symbols captured in region: {Environment.NewLine}");
                foreach (var symbol in symbols)
                {
                    sb.AppendFormat("\t\tName: [{0}], Kind: [{1}], Type: [{2}]{3}", symbol.Name, symbol.Kind,
                        symbol.GetType().Name, Environment.NewLine);
                }

                return sb.ToString();
            }

            private static (bool FoundIt, SyntaxNode Node) ExtractSyntaxNode(IOperation op, CancellationToken token)
            {
                bool foudnIt;
                SyntaxNode node;
                if (op != null)
                {
                    SyntaxReference syntax = FindSyntaxReference(op);
                    if (syntax != null)
                    {
                        node = syntax.GetSyntax(token);
                        foudnIt = node != null;
                    }
                    else
                    {
                        foudnIt = false;
                        node = null;
                    }
                }
                else
                {
                    foudnIt = false;
                    node = null;
                }

                return (foudnIt, node);
            }

            private static SyntaxReference FindSyntaxReference(IOperation op)
            {
                SyntaxReference ret;
                switch (op)
                {
                    case IMethodReferenceOperation methRefOp:
                        ret = methRefOp.Method.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault();
                        break;
                    case IAnonymousFunctionOperation anonFunc:
                        ret = anonFunc.Syntax.GetReference();
                        break;
                    default:
                        ret = null;
                        break;
                }
                return ret;
            }

            private enum AnalyteSymbolType
            {
                Invalid = 0,
                Field,
                Property,
                Local,
                TypeSymbol,
                ParameterSymbol,

            }

            private static ImmutableArray<(ParameterSyntax Syntax, ITypeSymbol Type, string IdentifierName)>
                GetNodeParameterSymbols(SyntaxNode node, SemanticModel semanticModel, CancellationToken conCancellationToken)
            {
                var ret = ImmutableArray<(ParameterSyntax Syntax, ITypeSymbol Type, string IdentifierName)>.Empty;
                var builder = ret.ToBuilder();

                switch (node)
                {
                    case MethodDeclarationSyntax mds:
                        Debug.Assert(mds.ExpressionBody?.Expression != null ||
                                     (mds.Body != null && mds.Body.Statements.Any()));
                        builder.AddRange(from ParameterSyntax ps in mds.ParameterList.Parameters
                                         let psyntax = ps
                                         let tsInfo = semanticModel.GetSymbolInfo(psyntax.Type)
                                         where tsInfo.Symbol is ITypeSymbol
                                         select (psyntax, (ITypeSymbol)tsInfo.Symbol, psyntax.Identifier.ValueText));
                        break;
                    case ParenthesizedLambdaExpressionSyntax pLambda:
                        builder.AddRange(from ps in pLambda.ParameterList.Parameters
                                         let psyntax = ps
                                         let tsInfo = semanticModel.GetSymbolInfo(psyntax.Type)
                                         where tsInfo.Symbol is ITypeSymbol
                                         select (psyntax, (ITypeSymbol)tsInfo.Symbol, psyntax.Identifier.ValueText));
                        break;
                    case SimpleLambdaExpressionSyntax sLambda:
                        var sLambParam = sLambda.Parameter;
                        var symbolInfo = sLambParam != null ? semanticModel.GetSymbolInfo(sLambParam.Type).Symbol : null;
                        if (symbolInfo is ITypeSymbol ts)
                        {
                            builder.Add((sLambParam, ts, sLambParam.Identifier.ValueText));
                        }
                        break;
                    case null:
                        DebugLog.Log("NULL syntax node submitted for parameter symbol extraction.");
                        break;
                    default:
                        DebugLog.Log(
                            $"Unrecongized syntax node submitted for parameter symbol extraction. Type {node.GetType().Name}," +
                            $" Kind: {node.Kind()}, Text: {node.ToFullString()}");
                        break;

                }
                conCancellationToken.ThrowIfCancellationRequested();
                return builder.ToImmutable();

            }

        } 
        #endregion

        static DelegateNoNonVsCaptureAnalyzerSource()
        {
            TheFactoryInstance = new LocklessWriteOnce<DelegateNoVsCaptureAnalyzerFactory>(() => DefaultDelNoVsCaptureAnalyzer.CreateInstance);
        }

        private static readonly LocklessWriteOnce<DelegateNoVsCaptureAnalyzerFactory> TheFactoryInstance;
        private static readonly DefaultDelNoVsCaptureAnalyzerFactory TheDefaultFactory =
            DefaultDelNoVsCaptureAnalyzer.CreateDefaultInstance;
    }
}
