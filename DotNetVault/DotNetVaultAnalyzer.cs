using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.ExtensionMethods;
using DotNetVault.Logging;
using DotNetVault.UtilitySources;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using VaultSafeTypeAnalyzer = DotNetVault.UtilitySources.VaultSafeAnalyzerFactorySource
    .VaultSafeTypeAnalyzerV2;

[assembly: InternalsVisibleTo("DotNetVault.Test")]

namespace DotNetVault
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class DotNetVaultAnalyzer : DiagnosticAnalyzer
    {
        #region Public Fields, Properties and Constants
        // ReSharper disable InconsistentNaming
        internal const string DiagnosticId_VaultSafeTypes = "DotNetVault_VaultSafe";
        internal const string DiagnosticId_UsingMandatory = "DotNetVault_UsingMandatory";
        internal const string DotNetVault_VsDelegateCapture = "DotNetVault_VsDelegateCapture";
        internal const string DotNetVault_VsTypeParams = "DotNetVault_VsTypeParams";
        internal const string DotNetVault_VsTypeParams_Method_Invoke = "DotNetVault_VsTypeParams_MethodInvoke";
        internal const string DotNetVault_VsTypeParams_Object_Create = "DotNetVault_VsTypeParams_ObjectCreate";
        internal const string DotNetVault_VsTypeParams_DelegateCreate = "DotNetVault_VsTypeParams_DelegateCreate";
        internal const string DotNetVault_NotVsProtectable = "DotNetVault_NotVsProtectable";
        // ReSharper restore InconsistentNaming
        #endregion        

        #region Public Methods

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors; 
           

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
#if DEBUG
         //   Debugger.Launch();
#endif
            using var dummy =
                EntryExitLog.CreateEntryExitLog(true, typeof(DotNetVaultAnalyzer), nameof(Initialize), context);
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information

            try
            {
                context.RegisterSymbolAction(AnalyzeTypeSymbolForVsTypeParams, SymbolKind.NamedType);
                context.RegisterSymbolAction(AnalyzeNamedTypeSymbolForVaultSafety, SymbolKind.NamedType);
                context.RegisterSyntaxNodeAction(AnalyzeInvocationForUmCompliance, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeMethodInvokeForVsTpCompliance, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeObjectCreationForVsTpCompliance,
                    SyntaxKind.ObjectCreationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeForIllegalUseOfNonVsProtectableResource, SyntaxKind.ObjectCreationExpression, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeObjectCreationForVsTpCompliance, SyntaxKind.LocalDeclarationStatement);
                context.RegisterOperationAction(AnalyzeAssignmentsForDelegateCompliance, OperationKind.DelegateCreation);
                context.RegisterOperationAction(AnalyzeDelegateCreationForBadNonVsCapture, OperationKind.DelegateCreation);
                context.EnableConcurrentExecution();
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        #endregion

        #region Primary Analysis Operations
        private void AnalyzeDelegateCreationForBadNonVsCapture(OperationAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeDelegateCreationForBadNonVsCapture);
            using var unused = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            var analyzer = DelegateNoNonVsCaptureAnalyzerSource.DefaultFactoryInstance();
            try
            {
                var scanResult = analyzer.ScanForNoNonVsCaptureAttribAndRetrieveAnalyteData(context);
                if (scanResult.IdentifiedAttribute)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    IDelegateCreationOperation delegateCreationOp = scanResult.CreationOp;
                    INamedTypeSymbol createdDelegateType = scanResult.CreationOpType;
                    IOperation targetOperation = scanResult.Target;

                    //DebugLog.Log(
                    //    $"Found delegate creation operation assigning to " +
                    //    $"delegate type [{createdDelegateType}], which has attribute " +
                    //    $"[{typeof(NoNonVsCaptureAttribute).FullName}].  " +
                    //    $"The delegate creation operation is {delegateCreationOp} and the target being " +
                    //    $"assigned to the newly created delegate is {targetOperation}");

                    (bool? IsCompliant, ImmutableArray<ITypeSymbol> NonVsCaptures, ImmutableArray<string>
                        NonVsCaptureText) complianceScanRes =
                            analyzer.AnalyzeOperationForCompliance(targetOperation, delegateCreationOp,
                                createdDelegateType, context);
                    Debug.Assert(complianceScanRes.NonVsCaptures.Length == complianceScanRes.NonVsCaptureText.Length);
                    LogResult(complianceScanRes);

                    if (complianceScanRes.IsCompliant == false)
                    {
                        string problem = GetComplianceScanResultText(complianceScanRes);
                        string pluralString = complianceScanRes.NonVsCaptureText.Length > 1 ? "s" : string.Empty;
                        var location = targetOperation.Syntax.GetLocation();
                        Debug.Assert(location != null && location != Location.None);
                        var reportMe = Diagnostic.Create(AnnotatedDelegatesMayNotReferenceNonVaultSafeSymbols,
                            location, pluralString, problem);
                        context.ReportDiagnostic(reportMe);
                    }

                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }
        

        private void AnalyzeAssignmentsForDelegateCompliance(OperationAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeAssignmentsForDelegateCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                IDelegateCreationOperation delegateCreationOp = (IDelegateCreationOperation) context.Operation;
                string typeName = ExtractTypeName(delegateCreationOp);
                IOperation target = delegateCreationOp.Target;
                if (target != null && !string.IsNullOrWhiteSpace(typeName))
                {
                    if (delegateCreationOp.Type is INamedTypeSymbol nts && nts.IsGenericType)
                    {
                        INamedTypeSymbol madeFrom = nts.ConstructedFrom;
                        var vsTypAttribSymbol = FindVaultSafeTypeParamAttribute(context.Compilation);
                        if (madeFrom != null && vsTypAttribSymbol != null)
                        {
                            TypeSymbolVsTpAnalysisResult analResult = AnalyzeTypeSymbolVsTpAnal(nts, vsTypAttribSymbol,
                                context.Compilation, context.CancellationToken);
                            if (!analResult.Passes)
                            {
                                string msg = analResult.PrintDiagnosticInfo();
                                context.ReportDiagnostic(Diagnostic.Create(GenericDelegateTypeArgumentsMustBeVaultSafe,
                                    nts.Locations[0], nts.Name, msg));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }

            static string ExtractTypeName(IDelegateCreationOperation op) => op?.Type?.MetadataName ?? string.Empty;
        }

        private void AnalyzeObjectCreationForVsTpCompliance(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeObjectCreationForVsTpCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                if (context.Node.Kind() == SyntaxKind.ObjectCreationExpression)
                {
                    INamedTypeSymbol nts;

                    var model = context.SemanticModel;
                    var compilation = context.Compilation;
                    var si = model.GetSymbolInfo(context.Node);
                    var vsTypAttribSymbol = FindVaultSafeTypeParamAttribute(compilation);
                    switch (si.Symbol)
                    {
                        case IErrorTypeSymbol ets:
                        {
                            var resolved = ets.ResolveErrorTypeSymbol(compilation);
                            if (resolved is INamedTypeSymbol nt)
                            {
                                nts = nt;
                            }
                            else
                            {
                                DebugLog.Log(
                                    "Analyzing obj creat expr [{context.Node}] for vstp compliance, " +
                                    "error type symbol {ets} could not be resolved.");
                                return;
                            }

                            break;
                        }
                        case INamedTypeSymbol nt:
                            nts = nt;
                            break;
                        case IMethodSymbol ms when ms.MethodKind == MethodKind.Constructor && ms.ContainingType != null:
                            nts = ms.ContainingType;
                            break;
                        default:
                            return;
                    }

                    Debug.Assert(nts != null);
                    if (nts.IsGenericType)
                    {
                        TypeSymbolVsTpAnalysisResult result = AnalyzeTypeSymbolVsTpAnal(nts, vsTypAttribSymbol,
                            context.Compilation, context.CancellationToken);
                        if (!result.Passes)
                        {
                            string msg = result.PrintDiagnosticInfo();
                            context.ReportDiagnostic(Diagnostic.Create(GenericTypeArgumentMustBeVaultSafe,
                                context.Node.GetLocation(), nts.Name, msg));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        private void AnalyzeInvocationForUmCompliance(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeInvocationForUmCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                var node = context.Node;
                InvocationExpressionSyntax syntax = node as InvocationExpressionSyntax;
                if (syntax != null && !UsingStatementAnalyzerUtilitySource.CreateStatementAnalyzer()
                        .IsPartOfUsingConstruct(syntax))
                {
                    var semanticModel = context.SemanticModel;
                    var usingMandatoryAttributeFinder =
                        UsingMandatoryAttributeFinderSource.GetDefaultAttributeFinder();
                    if (usingMandatoryAttributeFinder.HasUsingMandatoryReturnTypeSyntax(syntax, semanticModel))
                    {
                        var diagnostic = Diagnostic.Create(UsingMandatoryAttributeRequiresUsingConstruct,
                            node.GetLocation(), syntax.Expression);
                        //Generate diagnostic
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else if (syntax != null
                ) //&&UsingStatementAnalyzerUtilitySource.CreateStatementAnalyzer().IsPartOfUsingConstruct(syntax)
                {
                    //TODO FIXIT BUG 50 -- Impose requirement that variable assigned to declared inline to bind access thereto to lexical scope.


                }


            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        private void AnalyzeMethodInvokeForVsTpCompliance(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeMethodInvokeForVsTpCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                INamedTypeSymbol vaultSafeTpAttribSymbol = FindVaultSafeTypeParamAttribute(context.Compilation);
                var node = context.Node;
                if (node.IsKind(SyntaxKind.InvocationExpression))
                {
                    if (node is InvocationExpressionSyntax ies)
                    {
                        var model = context.SemanticModel;
                        var sym = model.GetSymbolInfo(ies);
                        if (sym.Symbol is IMethodSymbol methSym)
                        {
                            if (methSym.IsGenericMethod && HasSubstitutedTypeSymbol(methSym))
                            {
                                (bool HasAnyVsTpAttributes, ImmutableArray<int> IndicesOfVsTps) scanResult =
                                    ScanForVaultSafeTypeParamAttribs(methSym, vaultSafeTpAttribSymbol);
                                if (scanResult.HasAnyVsTpAttributes)
                                {
                                    var set = (
                                        from idx in scanResult.IndicesOfVsTps
                                        let symbol = methSym.TypeArguments[idx]
                                        where symbol is INamedTypeSymbol || symbol is IArrayTypeSymbol ||
                                              symbol is IDynamicTypeSymbol
                                        select symbol).ToImmutableHashSet();
                                    var vaultSafeAnalyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                                    var nonConformingSymbols =
                                        set.Where(sym2 =>
                                            {
                                                bool ret;
                                                switch (sym2)
                                                {
                                                    case INamedTypeSymbol nts:
                                                        ret = !vaultSafeAnalyzer.IsTypeVaultSafe(nts,
                                                            context.Compilation);
                                                        break;
                                                    case IArrayTypeSymbol _:
                                                    case IDynamicTypeSymbol _:
                                                        ret = true;
                                                        break;
                                                    default:
                                                        ret = false;
                                                        break;
                                                }

                                                return ret;
                                            })
                                            .ToImmutableArray();
                                    if (nonConformingSymbols.Any())
                                    {

                                        var formatStringArgs = GetArgumentsForDiagnosticFormatString(methSym,
                                            scanResult.IndicesOfVsTps, nonConformingSymbols);
                                        LogDiagnosticFormatString(VsTp_MessageFormat_MethInv,
                                            formatStringArgs.MethodName, formatStringArgs.IndexList,
                                            formatStringArgs.NonConformingTypes);
                                        context.ReportDiagnostic(Diagnostic.Create(
                                            GenericMethodTypeArgumentMustBeVaultSafe, node.GetLocation(),
                                            formatStringArgs.MethodName, formatStringArgs.IndexList,
                                            formatStringArgs.NonConformingTypes));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }

            static (string MethodName, string IndexList, string NonConformingTypes)
                GetArgumentsForDiagnosticFormatString(IMethodSymbol methodSymbol, ImmutableArray<int> indicesOfVsTps,
                    ImmutableArray<ITypeSymbol> nonConformingSymbols)
            {
                string methName = methodSymbol.Name ?? string.Empty;
                string indexList = indicesOfVsTps.ConvertToCommaSeparatedList();
                string nonConformingTypes = nonConformingSymbols.ConvertToCommaSeparatedList(ncs => ncs.Name);
                return (methName, indexList, nonConformingTypes);
            }
        }

        private void AnalyzeNamedTypeSymbolForVaultSafety(SymbolAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeNamedTypeSymbolForVaultSafety);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                var namedTypeSymbol = (INamedTypeSymbol) context.Symbol;
                INamedTypeSymbol vaultSafeAttribSymbol = FindVaultSafeAttribute(context.Compilation);

                // Find just those named type symbols with names containing lowercase letters.
                if (DoesNamedTypeHaveAttribute(namedTypeSymbol, vaultSafeAttribSymbol) &&
                    !ConstructedWithFirstParamTrue(namedTypeSymbol,
                        vaultSafeAttribSymbol, context.Compilation) &&
                    !Analyzer.IsTypeVaultSafe(namedTypeSymbol, context.Compilation))
                {
                    // For all such symbols, produce a diagnostic.
                    var diagnostic = Diagnostic.Create(VaultSafeTypesMustBeVaultSafe, namedTypeSymbol.Locations[0],
                        namedTypeSymbol.Name);

                    context.ReportDiagnostic(diagnostic);

                    TraceLog.Log($"Named type symbol: [{namedTypeSymbol.Name}] was found not to be VaultSafe.  Diagnostic: [{diagnostic.ToString()}].");
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        private void AnalyzeTypeSymbolForVsTypeParams(SymbolAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeTypeSymbolForVsTypeParams);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                var namedTypeSymbol = (INamedTypeSymbol) context.Symbol;
                INamedTypeSymbol vaultSafeTpAttribSymbol = FindVaultSafeTypeParamAttribute(context.Compilation);
                Debug.Assert(vaultSafeTpAttribSymbol != null && namedTypeSymbol != null);
                Debug.WriteLine($"Evaluating {namedTypeSymbol} for vs type param attribs.");
                TypeSymbolVsTpAnalysisResult result = AnalyzeTypeSymbolVsTpAnal(namedTypeSymbol,
                    vaultSafeTpAttribSymbol,
                    context.Compilation, context.CancellationToken);
                if (!result.Passes)
                {
                    context.ReportDiagnostic(Diagnostic.Create(GenericVaultSafeTypeParamsMustBeVaultSafe,
                        context.Symbol.Locations.FirstOrDefault(), namedTypeSymbol));
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        private void AnalyzeForIllegalUseOfNonVsProtectableResource(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeForIllegalUseOfNonVsProtectableResource);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);

            try
            {
                var attribute = context.Compilation.FindNotVsProtectableAttributeSymbol();

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse DEBUG VS RELEASE
                if (attribute == null)
                {
                    DebugLog.Log(
                        $"Unable to identify attribute [{typeof(NotVsProtectableAttribute).Name}].  " +
                        $"Syntax node [{context.Node?.ToString() ?? "NULL NODE"}] cannot be evaluated for Illegal Use of " +
                        $"Non Vs Protectable resource.");
                    return;
                }

                SyntaxKind? k = ExtractSyntaxKind(context);
                if (k != null)
                {
                    var model = context.SemanticModel;
                    INamedTypeSymbol returnedOrCreatedType;

                    //for reals:
                    var vaultSymbol = context.Compilation.GetTypeByMetadataName("DotNetVault.Vaults.Vault`1");
                    //for testing:
                    //var vaultSymbol =
                    //    context.Compilation.GetTypeByMetadataName("DotNetVault.Test.TestCases.FakeVault`1");
                    var si = model.GetSymbolInfo(context.Node);
                    Debug.Assert(vaultSymbol != null);

                    switch (si.Symbol)
                    {
                        case INamedTypeSymbol nt:
                            returnedOrCreatedType = nt;
                            break;
                        case IMethodSymbol ms when ms.MethodKind == MethodKind.Constructor && ms.ContainingType != null:
                            returnedOrCreatedType = ms.ContainingType;
                            break;
                        case IMethodSymbol ms when ms.ReturnType is INamedTypeSymbol nt2:
                            returnedOrCreatedType = nt2;
                            break;
                        default:
                            returnedOrCreatedType = null;
                            break;
                    }

                    if (returnedOrCreatedType != null)
                    {
                        var setOfBaseTypes = GetBaseSymbols(returnedOrCreatedType, context.CancellationToken);
                        IEnumerable<(INamedTypeSymbol Closed, INamedTypeSymbol Open)> symbols =
                            (from item in setOfBaseTypes
                                let n = item as INamedTypeSymbol
                                where n != null &&
                                      n.ConstructedFrom?.Equals(vaultSymbol, SymbolEqualityComparer.Default) ==
                                      true && ThrowOnToken(context.CancellationToken)
                                let closed = n
                                let open = n.ConstructedFrom
                                select (closed, open));

                        var gotIt = symbols.FirstOrDefault();

                        if (gotIt.Closed?.TypeArguments.FirstOrDefault() is INamedTypeSymbol typeArgument)
                        {
                            DebugLog.Log($"Found type argument {typeArgument.Name}");

                            bool hasAttribute = DoesNamedTypeHaveAttribute(typeArgument, attribute);
                            // ReSharper disable once RedundantAssignment
                            string doesOrDoesnt = hasAttribute ? "has" : "does not have";
                            Debug.WriteLine(
                                $"The type {typeArgument.Name} {doesOrDoesnt} the {attribute.Name} attribute.");
                            if (hasAttribute)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(NotVsProtectableTypeCannotBeStoredInVault,
                                    context.Node.GetLocation(), typeArgument.Name,
                                    typeof(NotVsProtectableAttribute).Name));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DebugLog.Log($"{methodName} operation was cancelled.");
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }

            static SyntaxKind? ExtractSyntaxKind(SyntaxNodeAnalysisContext c)
            {
                SyntaxKind? kind;

                switch (c.Node.Kind())
                {
                    case SyntaxKind.ObjectCreationExpression:
                        kind = SyntaxKind.ObjectCreationExpression;
                        break;
                    case SyntaxKind.InvocationExpression:
                        kind = SyntaxKind.ObjectCreationExpression;
                        break;
                    default:
                        kind = null;
                        break;
                }

                return kind;

            }

            static bool ThrowOnToken(CancellationToken t)
            {
                t.ThrowIfCancellationRequested();
                return true;
            }

            static ImmutableHashSet<ITypeSymbol> GetBaseSymbols(ITypeSymbol ts, CancellationToken token)
            {
                Debug.Assert(ts != null);
                var immutHs = ImmutableHashSet.Create<ITypeSymbol>(SymbolEqualityComparer.Default, ts);
                var builder = immutHs.ToBuilder();
                foreach (var item in ts.AllInterfaces.Where(intf => intf != null))
                {
                    token.ThrowIfCancellationRequested();
                    builder.Add(item);
                }

                ITypeSymbol baseClass = ts.BaseType;
                while (baseClass != null)
                {
                    token.ThrowIfCancellationRequested();
                    builder.Add(baseClass);
                    baseClass = baseClass.BaseType;
                }

                return (builder.ToImmutable());
            }
        }

        #endregion

        #region Ancillary Methods
        // ReSharper disable UnusedMember.Local TODO -- consider removing
        private TypeSymbolVsTpAnalysisResult AnalyzeTypeSymbolVsTpAnal([NotNull] INamedTypeSymbol namedType,
            [NotNull] INamedTypeSymbol vsTpAttrib, [NotNull] Compilation compilation) =>
            AnalyzeTypeSymbolVsTpAnal(namedType, vsTpAttrib, compilation, CancellationToken.None);
        private bool HasSubstitutedTypeSymbol(INamedTypeSymbol nts) =>
            true == nts?.TypeArguments.Any(ta => ta?.Kind == SymbolKind.NamedType || ta?.Kind == SymbolKind.ArrayType || ta?.Kind == SymbolKind.DynamicType);
        // ReSharper restore UnusedMember.Local
        private bool HasSubstitutedTypeSymbol(IMethodSymbol methSym) =>
            true == methSym?.TypeArguments.Any(ta => ta?.Kind == SymbolKind.NamedType || ta?.Kind == SymbolKind.ArrayType || ta?.Kind == SymbolKind.DynamicType);
        private static INamedTypeSymbol FindVaultSafeAttribute(Compilation compilation) =>
            compilation?.FindVaultSafeAttribute();
        private static INamedTypeSymbol FindVaultSafeTypeParamAttribute(Compilation compilation) =>
            compilation?.FindVaultSafeTypeParamAttribute();

        [UsedImplicitly]
        private static INamedTypeSymbol FindUsingMandatoryAttribute(Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(UsingMandatoryAttribute).FullName);

        private TypeSymbolVsTpAnalysisResult AnalyzeTypeSymbolVsTpAnal([NotNull] INamedTypeSymbol namedType,
            [NotNull] INamedTypeSymbol vsTpAttrib, [NotNull] Compilation compilation, CancellationToken token)
        {
            HashSet<INamedTypeSymbol> typeSet = new HashSet<INamedTypeSymbol>();
            if (namedType.IsGenericType)
            {
                typeSet.Add(namedType);
            }
            typeSet.UnionWith(namedType.Interfaces.Where(intf => intf.IsGenericType));

            INamedTypeSymbol currentSymbol = namedType;
            while (currentSymbol != null)
            {
                currentSymbol = GetBaseClasses(currentSymbol, typeSet);
            }

            var temp =
                new List<(INamedTypeSymbol IndividualType, SortedSet<IndividualFailureTriplet> Triplets)>(
                    typeSet.Count);
            foreach (var currentNts in typeSet)
            {
                token.ThrowIfCancellationRequested();
                Debug.WriteLine($"Analyzing base type {currentNts} during analysis of {namedType} for vs type param attribs.");
                if (currentNts.IsGenericType)
                {
                    bool hasSubstitutedSymbols = (currentNts.TypeArguments.Any(ta => ta.Kind == SymbolKind.NamedType || ta.Kind == SymbolKind.ArrayType || ta.Kind == SymbolKind.DynamicType));
                    if (hasSubstitutedSymbols)
                    {
                        SortedSet<IndividualFailureTriplet> failureTriplets = new SortedSet<IndividualFailureTriplet>();
                        var scanResult =
                            ScanForVaultSafeTypeParamAttribs(currentNts, vsTpAttrib);
                        if (scanResult.HasAnyVsTpAttributes)
                        {
                            var vaultSafetyAnalyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                            foreach (var idx in scanResult.IndicesOfVsTps)
                            {
                                token.ThrowIfCancellationRequested();
                                switch (currentNts.TypeArguments[idx])
                                {
                                    case INamedTypeSymbol typeArgSymb:
                                    {
                                        var isVaultSafe = vaultSafetyAnalyzer.IsTypeVaultSafeAsync(typeArgSymb,
                                            compilation, token).Result;
                                        if (!isVaultSafe.Result)
                                        {
                                            if (isVaultSafe.Error == null)
                                            {
                                                var paramType = currentNts.TypeParameters[idx];
                                                var offendingType = typeArgSymb;
                                                failureTriplets.Add(
                                                    new IndividualFailureTriplet(idx, paramType, offendingType));
                                            }
                                        }
                                        break;
                                    }
                                    case IArrayTypeSymbol ats:
                                    {
                                        var paramType = currentNts.TypeParameters[idx];
                                        var offendingType = ats;
                                        failureTriplets.Add(
                                            new IndividualFailureTriplet(idx, paramType, offendingType));
                                    }
                                    break;
                                    case IDynamicTypeSymbol dts:
                                    {
                                        var paramType = currentNts.TypeParameters[idx];
                                        var offendingType = dts;
                                        failureTriplets.Add(
                                            new IndividualFailureTriplet(idx, paramType, offendingType));
                                    }
                                    break;
                                }
                            }

                            if (failureTriplets.Any())
                            {
                                temp.Add((currentNts, failureTriplets));
                            }
                        }
                    }
                }
            }
            ImmutableArray<IndividualAnalysisResult> resultArray = ImmutableArray<IndividualAnalysisResult>.Empty;
            if (temp.Any())
            {
                resultArray = resultArray.AddRange(temp.Select(tupl =>
                    new IndividualAnalysisResult(tupl.IndividualType, tupl.Triplets.ToImmutableArray())));
            }
            return new TypeSymbolVsTpAnalysisResult(namedType, resultArray);
        }



        private INamedTypeSymbol GetBaseClasses
            (ITypeSymbol myBaseClassIfGenericAndItsInterfacesIfGeneric, HashSet<INamedTypeSymbol> symbolSet)
        {
            var baseType = myBaseClassIfGenericAndItsInterfacesIfGeneric.BaseType;
            if (baseType != null)
            {
                if (baseType.IsGenericType)
                {
                    symbolSet.Add(baseType);
                }

                symbolSet.UnionWith(baseType.Interfaces.Where(intf => intf.IsGenericType));
            }
            return baseType;
        }

        private (bool HasAnyVsTpAttributes, ImmutableArray<int> IndicesOfVsTps) ScanForVaultSafeTypeParamAttribs(
            [NotNull] INamedTypeSymbol scanMe, [NotNull] INamedTypeSymbol vsTpCanonicalAttribute)
        {
            ImmutableArray<int> indices = ImmutableArray<int>.Empty;
            if (scanMe.IsGenericType)
            {
                var builder = indices.ToBuilder();

                builder.AddRange(from tupl in scanMe.TypeParameters.EnumerateWithIndices()
                                 let idx = tupl.Index
                                 let attribList = tupl.Val?.GetAttributes() ?? ImmutableArray<AttributeData>.Empty
                                 from attribData in attribList
                                 where IsNtsAnAttributeOfTypeAttributeSymbol(attribData.AttributeClass, vsTpCanonicalAttribute)
                                 select idx);
                builder.Capacity = builder.Count;
                indices = builder.MoveToImmutable();
            }

            return (indices.Any(), indices);
        }

        private (bool HasAnyVsTpAttributes, ImmutableArray<int> IndicesOfVsTps) ScanForVaultSafeTypeParamAttribs(
            [NotNull] IMethodSymbol scanMe, [NotNull] INamedTypeSymbol vsTpCanonicalAttribute)
        {
            ImmutableArray<int> indices = ImmutableArray<int>.Empty;
            if (scanMe.IsGenericMethod)
            {
                var builder = indices.ToBuilder();

                builder.AddRange(from tupl in scanMe.TypeParameters.EnumerateWithIndices()
                                 let idx = tupl.Index
                                 let attribList = tupl.Val?.GetAttributes() ?? ImmutableArray<AttributeData>.Empty
                                 from attribData in attribList
                                 where IsNtsAnAttributeOfTypeAttributeSymbol(attribData.AttributeClass, vsTpCanonicalAttribute)
                                 select idx);
                builder.Capacity = builder.Count;
                indices = builder.MoveToImmutable();
            }

            return (indices.Any(), indices);
        }

        private static bool ConstructedWithFirstParamTrue(INamedTypeSymbol querySymbol, INamedTypeSymbol vaultSafeAttrib, Compilation model)
        {
            if (querySymbol == null || vaultSafeAttrib == null) return false;
            var queryRes = FindFirstMatchingVaultSafeAttribData(querySymbol, vaultSafeAttrib);
            if (queryRes == default) return false;

            bool ret = false;

            var matchingData = queryRes.AttribData;
            var matchingAttrbClass = queryRes.MatchingAttribDataClass;
            if (matchingAttrbClass?.Equals(vaultSafeAttrib, SymbolEqualityComparer.Default) == true && matchingData != null)
            {
                ret = matchingData.ConstructorArguments.Length == 1 &&
                       matchingData.ConstructorArguments[0].Value is bool b && b;
            }
            else if (matchingData != null && matchingAttrbClass != null)
            {
                var syntaxTree = matchingData.ApplicationSyntaxReference.SyntaxTree;
                if (syntaxTree != null)
                {
                    var nodes = syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                        .FirstOrDefault(tds => tds.Identifier.Text == querySymbol.Name);
                    var attributeListSyntax = nodes?.DescendantNodes().OfType<AttributeListSyntax>();

                    var attributeSyntaxes = attributeListSyntax?.SelectMany(als => als.Attributes).ToList() ??
                                            new List<AttributeSyntax>();
                    var attribInQuestion = attributeSyntaxes.FirstOrDefault(attrsyn =>
                        attrsyn.DescendantNodes().OfType<IdentifierNameSyntax>().Any(ins =>
                            ins.Identifier.ValueText == nameof(VaultSafeAttribute) ||
                            ins.Identifier.ValueText == "VaultSafe"));

                    bool hasArguments = attribInQuestion?.ArgumentList?.Arguments.Count == 1;
                    if (hasArguments)
                    {
                        var firstArgument = attribInQuestion.ArgumentList?.Arguments[0];
                        var firstArgumentExpression = firstArgument?.Expression;
                        if (firstArgumentExpression != null)
                        {
                            switch (firstArgumentExpression.Kind())
                            {
                                case SyntaxKind.TrueLiteralExpression:
                                    ret = true;
                                    break;
                                case SyntaxKind.FalseLiteralExpression:
                                    // ReSharper disable once RedundantAssignment (clarity of what is done by this case)
                                    ret = false;
                                    break;
                                default:
                                    var semanticModel = model.GetSemanticModel(syntaxTree);
                                    var help = semanticModel.GetConstantValue(firstArgumentExpression);
                                    ret = help.HasValue && help.Value is bool b && b;
                                    break;
                            }
                        }
                    }
                }
            }

            return ret;

            static (AttributeData AttribData, INamedTypeSymbol MatchingAttribDataClass) FindFirstMatchingVaultSafeAttribData(
                INamedTypeSymbol nts, INamedTypeSymbol canonical) =>
                (from ad in nts.GetAttributes()
                 let isMatch = IsNtsAnAttributeOfTypeAttributeSymbol(ad.AttributeClass, canonical)
                 where isMatch
                 select (ad, ad.AttributeClass)).FirstOrDefault();
        }

        [Conditional("DEBUG")]
        private void LogDiagnosticFormatString([NotNull] string formatString, [NotNull] params string[] args)
        {
            // ReSharper disable once CoVariantArrayConversion
            // ReSharper disable once RedundantAssignment
            string message = string.Format(formatString, args);
            DebugLog.Log(message);
        }

        [Conditional("DEBUG")]
        private static void LogResult(
           (bool? IsCompliant, ImmutableArray<ITypeSymbol> NonVsCaptures, ImmutableArray<string> NonVsCaptureText)
               complianceScanRes)
        {
            switch (complianceScanRes.IsCompliant)
            {
                case true:
                    DebugLog.Log($"Res ... COMPLIANT.{Environment.NewLine}");
                    DebugLog.Log(Environment.NewLine);
                    break;
                case false:
                    DebugLog.Log($"Res ... NON-COMPLIANT.{Environment.NewLine}");
                    Debug.Assert(complianceScanRes.NonVsCaptureText.Any() &&
                                 complianceScanRes.NonVsCaptureText.Length == complianceScanRes.NonVsCaptures.Length);
                    for (int i = 0; i < complianceScanRes.NonVsCaptures.Length; ++i)
                    {
                        DebugLog.Log(
                            $"\t\tNon-compliant symbol: {ExtractName(complianceScanRes.NonVsCaptures[i])}\tExplanation: " +
                            $"{complianceScanRes.NonVsCaptureText[i] ?? "NULL EXPLANATION"}{Environment.NewLine}");
                    }
                    break;
                case null:
                    DebugLog.Log($"Res ... INCONCLUSIVE-COMPLIANCE");
                    Debug.Assert(complianceScanRes.NonVsCaptureText.Length == complianceScanRes.NonVsCaptures.Length);
                    for (int i = 0; i < complianceScanRes.NonVsCaptures.Length; ++i)
                    {
                        DebugLog.Log(
                            $"\t\tNon-compliant symbol: {ExtractName(complianceScanRes.NonVsCaptures[i])}\tExplanation: " +
                            $"{complianceScanRes.NonVsCaptureText[i] ?? "NULL EXPLANATION"}{Environment.NewLine}");
                    }
                    break;
            }

         
        }

        static string ExtractName(ITypeSymbol ts)
        {
            if (ts is INamedTypeSymbol nts) return nts.Name;

            return $"Type symbol of kind: {ts.TypeKind}";
        }

        private static string GetComplianceScanResultText(
            (bool? IsCompliant, ImmutableArray<ITypeSymbol> NonVsCaptures, ImmutableArray<string> NonVsCaptureText)
                complianceScanRes)
        {
            if (complianceScanRes.IsCompliant != false) return string.Empty;

            var sb = new StringBuilder();
            Debug.Assert(complianceScanRes.NonVsCaptures.Any() && complianceScanRes.NonVsCaptures.Length ==
                             complianceScanRes.NonVsCaptureText.Length);
            for (int i = 0; i < complianceScanRes.NonVsCaptures.Length; ++i)
            {
                sb.Append("Non - compliant symbol: ");
                sb.Append(ExtractName(complianceScanRes.NonVsCaptures[i]));
                sb.Append("; \tExplanation: ");
                sb.AppendLine(complianceScanRes.NonVsCaptureText[i] ?? "NULL EXPLANATION");
            }

            return sb.ToString();
        }

        private static bool DoesNamedTypeHaveAttribute(INamedTypeSymbol querySymbol, INamedTypeSymbol canonicalSymbolToFind)
        {
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer),
                nameof(DoesNamedTypeHaveAttribute), querySymbol, canonicalSymbolToFind);

            if (querySymbol == null || canonicalSymbolToFind == null) return false;

            return querySymbol.DoesNamedTypeHaveAttribute(canonicalSymbolToFind);
        }

        private static bool IsNtsAnAttributeOfTypeAttributeSymbol(INamedTypeSymbol nts,
            INamedTypeSymbol attributeSymbol) => nts.IsNtsAnAttributeOfTypeAttributeSymbol(attributeSymbol);

        #endregion

        #region Private Constants, fields and properties
        private VaultSafeTypeAnalyzer Analyzer
        {
            get
            {
                VaultSafeTypeAnalyzer anal = _analyzer;
                if (anal == null)
                {
                    var newAnal = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                    Debug.Assert(newAnal != null);
                    Interlocked.CompareExchange(ref _analyzer, newAnal, null);
                    anal = _analyzer;
                    Debug.Assert(anal != null);
                }
                return anal;
            }
        }

        private static ImmutableArray<DiagnosticDescriptor> Descriptors => TheDiagnosticDescriptors.Value;

        private static ImmutableArray<DiagnosticDescriptor> CreateDiagnosticDescriptors()
        {
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer),
                nameof(CreateDiagnosticDescriptors));
            try
            {
                return ImmutableArray.Create(VaultSafeTypesMustBeVaultSafe)
                    .Add(UsingMandatoryAttributeRequiresUsingConstruct)
                    .Add(GenericVaultSafeTypeParamsMustBeVaultSafe)
                    .Add(GenericMethodTypeArgumentMustBeVaultSafe)
                    .Add(GenericTypeArgumentMustBeVaultSafe)
                    .Add(GenericDelegateTypeArgumentsMustBeVaultSafe)
                    .Add(AnnotatedDelegatesMayNotReferenceNonVaultSafeSymbols)
                    .Add(NotVsProtectableTypeCannotBeStoredInVault);
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        // ReSharper disable InconsistentNaming
        private static readonly LocalizableString Vst_Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Vst_MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Vst_Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "VaultSafety";

        private const string Um_Title = "Value returned from method invocation must be a part of using statement.";
        private const string Um_MessageFormat = "Value returned by expression [{0}] is not guarded by using construct.";
        private const string UsingMandatoryAttribute = nameof(UsingMandatoryAttribute);
        private const string Um_Description = "Values returned by methods annotated with the " +
                                              UsingMandatoryAttribute +
                                              " must be subject to a using construct.";

        private const string VsDelCapt_Title = "A delegate annotated with the " + nameof(NoNonVsCaptureAttribute) +
                                               " attribute cannot access certain non-vault safe symbols.";
        private const string VsDelCapt_MessageFormat = "The delegate is annotated with the " +
                                                       nameof(NoNonVsCaptureAttribute) +
                                                       " attribute but captures or references the following non-vault safe type{0}- [{1}]";
        private const string VsDelCapt_Description =
            "Delegates annotated with the " + nameof(NoNonVsCaptureAttribute) +
            " cannot capture any non-vault safe symbols (including \"this\") and " +
            "cannot refer to any static fields or properties that are not vault-safe.";

        private const string VsNotVsProtect_Title = "A type marked with the " + nameof(NotVsProtectableAttribute) +
                                                 " attribute may not be a protected resource inside a vault.";
        private const string VsNotVsProtect_MessageFormat =
            "The type {0} has the {1} attribute.  It cannot be stored as a protected resource inside a vault.";
        private const string VsNotVsProtect_Description =
            "Types marked with the " + nameof(NotVsProtectableAttribute) +
            " are considered VaultSafe for certain purposed.  They are not, however, eligible for protection inside a Vault.";

        //private const string VaultSafeTypeParamAttributeName = nameof(VaultSafeTypeParamAttribute);
        private const string VsTp_Title = "The type argument must be vault-safe.";
        private const string VsTp_MessageFormat =
            "The type {0} or one of its ancestors requires a vault-safe type argument but the argument supplied is not vault-safe.";
        private const string VsTp_MessageFormat_MethInv =
            "The generic method {0} requires vault-safety for the type arguments at the following indices: {1}.  The following types are not vault-safe: {2}.";
        private const string VsTp_MessageFormat_ObjCreate =
            "The generic type {0} has type arguments that do not comply with vault-safety requirements.  Diagnostic: [{1}].";
        private const string VsTp_MessageFormat_DelCreate =
            "The generic delegate {0} has type arguments that do not comply with vault-safety requirements.  Diagnostic: [{1}].";
        private const string VsTp_Description_MethodInv =
            "Generic methods with type parameters annotated with the attribute require that all arguments corresponding to those parameters be vault-safe.";
        private const string VsTp_Description =
            "Generic types that have annotated their type parameters with the {0} attribute require that all such arguments be vault-safe.";
        private const string VsTp_Description_ObjCreate =
            "Generic types with type parameters annotated with the VaultSafeTypeParam attribute require that all arguments corresponding to those parameters be vault-safe.";
        private const string VsTp_Description_DelCreate =
            "Generic delegates with type parameters annotated with the VaultSafeTypeParam attribute require that all arguments corresponding to those parameters be vault-safe.";
        // ReSharper restore InconsistentNaming

        private static readonly DiagnosticDescriptor AnnotatedDelegatesMayNotReferenceNonVaultSafeSymbols =
            new DiagnosticDescriptor(DotNetVault_VsDelegateCapture, VsDelCapt_Title, VsDelCapt_MessageFormat, Category,
                DiagnosticSeverity.Error, true, VsDelCapt_Description);
        private static readonly DiagnosticDescriptor VaultSafeTypesMustBeVaultSafe =
            new DiagnosticDescriptor(DiagnosticId_VaultSafeTypes, Vst_Title, Vst_MessageFormat, Category,
                DiagnosticSeverity.Error, isEnabledByDefault: true, description: Vst_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryAttributeRequiresUsingConstruct =
            new DiagnosticDescriptor(DiagnosticId_UsingMandatory, Um_Title, Um_MessageFormat, Category,
                DiagnosticSeverity.Error, true, Um_Description);
        private static readonly DiagnosticDescriptor GenericVaultSafeTypeParamsMustBeVaultSafe =
            new DiagnosticDescriptor(DotNetVault_VsTypeParams, VsTp_Title, VsTp_MessageFormat, Category,
                DiagnosticSeverity.Error, true, VsTp_Description);
        private static readonly DiagnosticDescriptor GenericMethodTypeArgumentMustBeVaultSafe =
            new DiagnosticDescriptor(DotNetVault_VsTypeParams_Method_Invoke, VsTp_Title, VsTp_MessageFormat_MethInv,
                Category, DiagnosticSeverity.Error, true, VsTp_Description_MethodInv);
        private static readonly DiagnosticDescriptor GenericTypeArgumentMustBeVaultSafe =
            new DiagnosticDescriptor(DotNetVault_VsTypeParams_Object_Create, VsTp_Title, VsTp_MessageFormat_ObjCreate,
                Category, DiagnosticSeverity.Error, true, VsTp_Description_ObjCreate);
        private static readonly DiagnosticDescriptor GenericDelegateTypeArgumentsMustBeVaultSafe =
            new DiagnosticDescriptor(DotNetVault_VsTypeParams_DelegateCreate, VsTp_Title, VsTp_MessageFormat_DelCreate,
                Category, DiagnosticSeverity.Error, true, VsTp_Description_DelCreate);
        private static readonly DiagnosticDescriptor NotVsProtectableTypeCannotBeStoredInVault =
            new DiagnosticDescriptor(DotNetVault_NotVsProtectable, VsNotVsProtect_Title, VsNotVsProtect_MessageFormat,
                Category, DiagnosticSeverity.Error, true, VsNotVsProtect_Description);
        private static readonly WriteOnce<ImmutableArray<DiagnosticDescriptor>> TheDiagnosticDescriptors = new WriteOnce<ImmutableArray<DiagnosticDescriptor>>(CreateDiagnosticDescriptors);
        private volatile VaultSafeTypeAnalyzer _analyzer;
        private const bool EnableEntryExitLogging = false;

        #endregion
    }

    internal static class StringConversionExtensions
    {
        //if converter is null, uses .ToString()   
        //e.g. array of ints -> "{1, 75, 3}", empty col -> "{ }"
        internal static string ConvertToCommaSeparatedList<T>([NotNull] [ItemNotNull] this IReadOnlyList<T> items, 
            [CanBeNull] Func<T, string> converter = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            
            if (items.Count < 1) return "{ }";
            converter ??= i => i.ToString();
            StringBuilder sb = new StringBuilder("{");
            foreach (var item in items)
            {
                sb.Append($" {converter(item)},");
            }
            sb.Append("}");
            return sb.ToString();
        }
    }

    internal static class HashSetBuilderExtensions
    {
        internal static void RemoveAll<T>([NotNull] this ImmutableHashSet<T>.Builder hsBuilder,
            [NotNull] Func<T, bool> predicate)
        {
            if (hsBuilder == null) throw new ArgumentNullException(nameof(hsBuilder));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            ImmutableHashSet<T> immut = hsBuilder.ToImmutable();
            foreach (var item in immut)
            {
                if (predicate(item))
                    hsBuilder.Remove(item);
            }
        }
    }
}
