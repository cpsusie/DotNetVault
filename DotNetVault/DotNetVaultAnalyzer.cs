using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
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
using BvProtResAnalyzer = DotNetVault.UtilitySources.BvProtResAnalyzerFactorySource.BvProtResAnalyzerImpl;
[assembly: InternalsVisibleTo("DotNetVault.Test")]

namespace DotNetVault
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    // ReSharper disable once InconsistentNaming
    internal class DotNetVaultAnalyzer : DiagnosticAnalyzer
    {
        #region Public Fields, Properties and Constants
        // ReSharper disable InconsistentNaming
        internal const string DotNetVault_ReportWhiteLists = "DotNetVault_ReportWhiteLists";
        internal const string DiagnosticId_VaultSafeTypes = "DotNetVault_VaultSafe";
        internal const string DiagnosticId_UsingMandatory = "DotNetVault_UsingMandatory";
        internal const string DiagnosticId_UsingMandatory_Inline = "DotNetVault_UsingMandatory_DeclaredInline";
        internal const string DotNetVault_UsingMandatory_NoCopyIllegalPass =
            "DotNetVault_UsingMandatory_NoCopyIllegalPass";
        internal const string DotNetVault_UsingMandatory_NoCopyIllegalPass_ExtMethod =
            "DotNetVault_UsingMandatory_NoCopyIllegalPass_ExtMethod";
        internal const string DotNetVault_UsingMandatory_NoLockedResourceWrappersAllowedInScope =
            "DotNetVault_UsingMandatory_NoProtectedResourceWrappersAllowedInScope";
        internal const string DotNetVault_UsingMandatory_NoCopyAssignment =
            "DotNetVault_UsingMandatory_NoCopyAssignment";
        internal const string DotNetVault_UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope =
            "DotNetVault_UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope";

        internal const string DotNetVault_VsDelegateCapture = "DotNetVault_VsDelegateCapture";
        internal const string DotNetVault_VsTypeParams = "DotNetVault_VsTypeParams";
        internal const string DotNetVault_VsTypeParams_Method_Invoke = "DotNetVault_VsTypeParams_MethodInvoke";
        internal const string DotNetVault_VsTypeParams_Object_Create = "DotNetVault_VsTypeParams_ObjectCreate";
        internal const string DotNetVault_VsTypeParams_DelegateCreate = "DotNetVault_VsTypeParams_DelegateCreate";
        internal const string DotNetVault_NotVsProtectable = "DotNetVault_NotVsProtectable";
        internal const string DotNetVault_NotDirectlyInvocable = "DotNetVault_NotDirectlyInvocable";
        internal const string DotNetVault_UnjustifiedEarlyDispose = "DotNetVault_UnjustifiedEarlyDispose";
        internal const string DotNetVault_EarlyDisposeJustification = "DotNetVault_EarlyDisposeJustification";
        internal const string DotNetVault_NoExplicitByRefAlias = "DotNetVault_NoExplicitByRefAlias";

        internal const string DotNetVault_OnlyOnRefStruct = "DotNetVault_OnlyOnRefStruct";
        // ReSharper restore InconsistentNaming
        #endregion        

        #region Public Methods
        internal static ImmutableArray<DiagnosticDescriptor> DiagnosticDescriptors => TheDiagnosticDescriptors.Value;
        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors;


        /// <inheritdoc />
#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext context)

        {
#if DEBUG
            //   Debugger.Launch();
#endif
            using var dummy =
                EntryExitLog.CreateEntryExitLog(true, typeof(DotNetVaultAnalyzer), nameof(Initialize), context);
            try
            {
                context.RegisterSymbolAction(AnalyzeForWhiteListPaths, SymbolKind.NamedType);
                context.RegisterSyntaxNodeAction(AnalyzeRefExpressionForProtectedResource, SyntaxKind.RefExpression);
                context.RegisterSymbolAction(AnalyzeTypeSymbolForVsTypeParams, SymbolKind.NamedType);
                context.RegisterSymbolAction(AnalyzeNamedTypeSymbolForVaultSafety, SymbolKind.NamedType);
                //  context.RegisterSymbolAction(AnalyzeTypeForNoCopyFields, SymbolKind.NamedType);
                context.RegisterSyntaxNodeAction(AnalyzeInvocationForUmCompliance, SyntaxKind.InvocationExpression);
                context.RegisterSymbolAction(AnalyzeTypeDeclarationForIllegalUsageOfRefStructAttribute, SymbolKind.NamedType);
                context.RegisterSyntaxNodeAction(AnalyzeMethodInvokeForVsTpCompliance, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeObjectCreationForVsTpCompliance,
                    SyntaxKind.ObjectCreationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeForIllegalUseOfNonVsProtectableResource, SyntaxKind.ObjectCreationExpression, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeObjectCreationForVsTpCompliance, SyntaxKind.LocalDeclarationStatement);
                context.RegisterOperationAction(AnalyzeAssignmentsForDelegateCompliance, OperationKind.DelegateCreation);
                context.RegisterOperationAction(AnalyzeDelegateCreationForBadNonVsCapture, OperationKind.DelegateCreation);
                context.RegisterSyntaxNodeAction(AnalyzeMethodInvocationForNoDirectInvokeAttrib, SyntaxKind.InvocationExpression);
                context.RegisterSyntaxNodeAction(AnalyzeMethodInvocationForEarlyReleaseWithoutJustification, SyntaxKind.InvocationExpression);
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

        private void AnalyzeForWhiteListPaths(SymbolAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeForWhiteListPaths);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer),
                methodName, context);
            var token = context.CancellationToken;
            INamedTypeSymbol reportWhiteListFilesNts = context.Compilation.FindReportWhiteListLocationsAttribute();
            if (reportWhiteListFilesNts != null && context.Symbol is INamedTypeSymbol nts)
            {
                try
                {
                    if (DoesNamedTypeHaveAttribute(nts, reportWhiteListFilesNts))
                    {
                        var analyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                        
                            (FileInfo whiteListFile, FileInfo conditWhiteListFile) = analyzer.WhiteListFilePaths;
                            whiteListFile.Refresh();
                            token.ThrowIfCancellationRequested();
                            conditWhiteListFile.Refresh();
                            token.ThrowIfCancellationRequested();
                            var diagnostic = Diagnostic.Create(ReportWhiteListFilePathsOnRequest,
                                nts.Locations.FirstOrDefault(), DiagnosticSeverity.Warning, nts.Locations.Skip(1),  null, whiteListFile.FullName,
                                ExistenceString(whiteListFile.Exists), conditWhiteListFile.FullName,
                                ExistenceString(conditWhiteListFile.Exists));
                            context.ReportDiagnostic(diagnostic);
                    }
                }
                catch (IOException ex)
                {
                    TraceLog.Log(ex);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    DebugLog.Log($"{methodName} operation was cancelled.");
                }
                catch (Exception e)
                {
                    TraceLog.Log(e);
                    throw;
                }
                
            }

            static string ExistenceString(bool exists) => exists ? "exists" : "does not exist";
        }

        private void AnalyzeTypeDeclarationForIllegalUsageOfRefStructAttribute(SymbolAnalysisContext obj)
        {
            const string methodName = nameof(AnalyzeTypeDeclarationForIllegalUsageOfRefStructAttribute);
            CancellationToken token = obj.CancellationToken;
            CSharpCompilation compilation = (CSharpCompilation)obj.Compilation;
            if (obj.Symbol is INamedTypeSymbol nts)
            {
                try
                {
                    if (!nts.IsRefLikeType)
                    {
                        var refStructAttribute = compilation.FindRefStructAttribute();
                        token.ThrowIfCancellationRequested();
                        Debug.Assert(refStructAttribute != null);
                        if (DoesNamedTypeHaveAttribute(nts, refStructAttribute))
                        {
                            var diagnostic = Diagnostic.Create(NoRefStructAttrExceptOnRefStruct,
                                nts.Locations.FirstOrDefault(), DiagnosticSeverity.Error, nts.Locations.Skip(1), null,
                                nts.Name);
                            obj.ReportDiagnostic(diagnostic);
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
        }

        private void AnalyzeMethodInvocationForEarlyReleaseWithoutJustification(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeMethodInvocationForEarlyReleaseWithoutJustification);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer),
                methodName, context);
            try
            {
                CSharpCompilation compilation = (CSharpCompilation) context.Compilation;
                if (context.Node is InvocationExpressionSyntax ies)
                {
                    EarlyReleaseReason? justification;
                    var earlyReleaseAnalyzer = EarlyReleaseAnalyzerFactorySource.Factory();
                    bool hasEarlyReleaseAttribute =
                        earlyReleaseAnalyzer.IsEarlyReleaseCall(compilation, ies, context.CancellationToken);
                    DebugLog.Log($"Invocation expression {ies.ToString()} " +
                                 (hasEarlyReleaseAttribute ? "has" : "does not have") +
                                 $" the {typeof(EarlyReleaseAttribute).Name} attribute.");
                    if (hasEarlyReleaseAttribute)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var justRes =
                            earlyReleaseAnalyzer.GetEarlyReleaseJustification(compilation, ies,
                                context.CancellationToken);
                        DebugLog.Log("Early release justification: " +
                                     $"[{justRes.Reason?.ToString() ?? "NONE"}]");
                        justification = justRes.Reason;
                        if (justification.HasValue)
                        {
                            //information diagnostic documenting justification
                            var documentJustification = Diagnostic.Create(JustificationOfEarlyDispose,
                                justRes.InvocationLocation, ies.ToString(), justRes.EnclosingMethodSymbol,
                                justRes.Reason.ToString());
                            DebugLog.Log(documentJustification.GetMessage());
                            context.ReportDiagnostic(documentJustification);
                        }
                        else
                        {
                            //ERROR -- early dispose REQUIRES justification
                            var unjustifiedEarlyDisposeDx = Diagnostic.Create(UnjustifiedEarlyDisposeDiagnostic,
                                justRes.InvocationLocation, justRes.EnclosingMethodLocation.AsEnumerable(), ies.ToString(),
                                justRes.EnclosingMethodSymbol);
                            DebugLog.Log(unjustifiedEarlyDisposeDx.GetMessage());
                            context.ReportDiagnostic(unjustifiedEarlyDisposeDx);
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

        private void AnalyzeMethodInvocationForNoDirectInvokeAttrib(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeMethodInvocationForNoDirectInvokeAttrib);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer),
                methodName, context);
            try
            {
                CancellationToken token = context.CancellationToken;
                token.ThrowIfCancellationRequested();

                var noInvAttrib = context.Compilation.FindNoDirectInvokeAttribute();
                Debug.Assert(noInvAttrib != null, "noInvAttrib != null");
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- DEBUG vs RELEASE
                if (noInvAttrib != null)
                {
                    if (context.Node.Kind() == SyntaxKind.InvocationExpression &&
                        context.Node is InvocationExpressionSyntax ies)
                    {
                        var model = context.SemanticModel;
                        var symbol = model.GetSymbolInfo(ies);
                        if (symbol.Symbol is IMethodSymbol ms)
                        {
                            var attribList = ms.GetAttributes();
                            bool hasNoDirectInvokeAttrib = attribList.Any(atd =>
                                atd.AttributeClass?.Equals(noInvAttrib, SymbolEqualityComparer.Default) == true);
                            if (hasNoDirectInvokeAttrib)
                            {
                                var reportMe = Diagnostic.Create(NotDirectlyInvocableDiagnosticDescriptor,
                                    ies.GetLocation(), ms.Name);
                                context.ReportDiagnostic(reportMe);
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
        }

        private void AnalyzeDelegateCreationForBadNonVsCapture(OperationAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeDelegateCreationForBadNonVsCapture);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
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
                IDelegateCreationOperation delegateCreationOp = (IDelegateCreationOperation)context.Operation;
                string typeName = ExtractTypeName(delegateCreationOp);
                IOperation target = delegateCreationOp.Target;
                if (target != null && !string.IsNullOrWhiteSpace(typeName))
                {
                    if (delegateCreationOp.Type is INamedTypeSymbol nts && nts.IsGenericType)
                    {
                        var vsTypAttribSymbol = FindVaultSafeTypeParamAttribute(context.Compilation);
                        if (vsTypAttribSymbol != null)
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
        
        private void AnalyzeRefExpressionForProtectedResource(SyntaxNodeAnalysisContext obj)
        {
            const string methodName = nameof(AnalyzeRefExpressionForProtectedResource);
            using var _ =
                EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, obj);
            try
            {
                var syntax = (RefExpressionSyntax)obj.Node;
                var compilation = obj.Compilation;
                
                BvProtResAnalyzer analyzerUtil = BvProtResAnalyzerFactorySource.DefaultFactoryInstance();
                obj.CancellationToken.ThrowIfCancellationRequested();

                bool foundIllegalUsage = analyzerUtil.QueryContainsIllegalRefExpression(compilation, syntax,
                    obj.SemanticModel, obj.CancellationToken);
                if (foundIllegalUsage)
                {
                    var diagnostic = Diagnostic.Create(NoExplicitByRefAlias, obj.Node.GetLocation());
                    obj.ReportDiagnostic(diagnostic);
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

        private void AnalyzeObjectCreationForVsTpCompliance(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeObjectCreationForVsTpCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                var compilation = context.Compilation;
                if (context.Node.Kind() == SyntaxKind.ObjectCreationExpression)
                {
                    INamedTypeSymbol nts;

                    var model = context.SemanticModel;
                   
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
                            compilation, context.CancellationToken);
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
                var compilation = (CSharpCompilation) context.Compilation;
                var node = context.Node;
                InvocationExpressionSyntax syntax = node as InvocationExpressionSyntax;
                var usingStatementAnalyzer = UsingStatementAnalyzerUtilitySource.CreateStatementAnalyzer();
                if (syntax != null && !usingStatementAnalyzer.IsPartOfUsingConstruct(syntax))
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
                else if (syntax != null) //Is part of using construct 
                    //&& usingStatement.IsPartOfUsingConstruct(syntax)
                {
                    //TODO FIXIT BUG 50 -- Impose requirement that variable assigned to declared inline to bind access thereto to lexical scope.
                    //Bug 50 Fix Algorithm
                    // - If not annotated with using mandatory, quit
                    // - Otherwise, determine if the using syntax is a declaration.  
                    // - If a declaration, quit
                    // - Otherwise emit diagnostic

                    var semanticModel = context.SemanticModel;
                    var usingMandatoryAttributeFinder = UsingMandatoryAttributeFinderSource.GetDefaultAttributeFinder();
                    if (usingMandatoryAttributeFinder.HasUsingMandatoryReturnTypeSyntax(syntax, semanticModel))
                    {
                        //Case Has Um Attribute
                        (bool isInlineDeclaration, VariableDeclarationSyntax terminalParent) =
                            usingStatementAnalyzer.IsPartOfInlineDeclUsingConstruct(syntax);
                        if (!isInlineDeclaration)
                        {
                            var diagnostic = Diagnostic.Create(
                                UsingMandatoryAttributeAssignmentMustBeToVariableDeclaredInline,
                                node.GetLocation(), syntax.Expression);
                            //Generate diagnostic
                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            context.CancellationToken.ThrowIfCancellationRequested();
                            Debug.Assert(terminalParent != null);
                            (var _, ITypeSymbol ts, VariableDeclaratorSyntax ins) =
                                FindTypeInfo(terminalParent, context);
                            context.CancellationToken.ThrowIfCancellationRequested();
                            if (ts is INamedTypeSymbol protectedType && ins != null)
                            {
                                INamedTypeSymbol noCopyAttribute = context.Compilation.FindNoCopyAttribute();
                                INamedTypeSymbol refStructAttribute = context.Compilation.FindRefStructAttribute();
                                Debug.Assert(refStructAttribute != null);
                                bool typeHasNoCopyAttribute = DoesNamedTypeHaveAttribute(protectedType, noCopyAttribute);
                                if (typeHasNoCopyAttribute)
                                {
                                    (bool foundRelevantBlock, BlockSyntax bs, UsingStatementSyntax uss) =
                                        usingStatementAnalyzer.FindBlockOrUsingStatement(syntax);
                                    if (!foundRelevantBlock)
                                    {
                                        DebugLog.Log(
                                            $"Expected to find block syntax or using statement related to expression {syntax}, but they were not found.");
                                        return;
                                    }


                                    bool isBlockSyntax = bs != null;
                                    SyntaxNode searchMe = isBlockSyntax ? (SyntaxNode) bs : uss;
                                    var protectedSymbol =
                                        semanticModel.GetDeclaredSymbol(ins, context.CancellationToken);
                                    if (protectedSymbol != null)
                                    {

                                        //search for value copy operations on the right side
                                        var rightSideOfAssingments = searchMe.DescendantNodes()
                                            .OfType<AssignmentExpressionSyntax>()
                                            .Where(aes => aes.Right is IdentifierNameSyntax)
                                            .Select(aes => (IdentifierNameSyntax) aes.Right);
                                        var illegalAssignments = (from rsoa in rightSideOfAssingments
                                            let symb = semanticModel.GetSymbolInfo(rsoa, context.CancellationToken)
                                            where true == symb.Symbol?.Equals(protectedSymbol,
                                                SymbolEqualityComparer.Default)
                                            select (symb, rsoa)).ToImmutableArray();

                                        bool assignedFrom = illegalAssignments.Any();
                                        if (assignedFrom)
                                        {
                                            //report any illegal value copies
                                            foreach (var item in illegalAssignments)
                                            {
                                                var diagnostic = Diagnostic.Create(UsingMandatoryNoCopyAssignment,
                                                    item.rsoa.GetLocation(),
                                                    item.symb.Symbol?.Locations.IsDefault == false
                                                        ? item.symb.Symbol.Locations
                                                        : ImmutableArray<Location>.Empty, ts.Name,
                                                    item.symb.Symbol?.Name ?? string.Empty);
                                                context.ReportDiagnostic(diagnostic);
                                            }
                                        }

                                        //search on left hand side for copy assignments
                                        //and lhs is of same type as protected resource
                                        IEnumerable<(IdentifierNameSyntax AssignmentTarget, ExpressionSyntax
                                            RightHandSide)> lhsesToCheck =
                                            from searchNode in searchMe.DescendantNodes()
                                                .OfType<AssignmentExpressionSyntax>()
                                            let lhs = searchNode.Left as IdentifierNameSyntax
                                            let rhs = searchNode.Right
                                            where ThrowIfCanc(context.CancellationToken) && lhs != null && rhs != null
                                            let lhsTypeInfo =
                                                semanticModel.GetTypeInfo(lhs, context.CancellationToken).ConvertedType
                                                    as INamedTypeSymbol
                                            where ThrowIfCanc(context.CancellationToken) &&
                                                  SymbolEqualityComparer.Default.Equals(protectedType, lhsTypeInfo)
                                            select (lhs, rhs);

                                        var illegalLhsCopyAssignments = ImmutableArray
                                            .CreateBuilder<(IdentifierNameSyntax AssignmentTarget, ExpressionSyntax
                                                RightHandSide)>();
                                        foreach ((IdentifierNameSyntax assignmentTarget,
                                            ExpressionSyntax assignmentSource) in lhsesToCheck)
                                        {
                                            
                                            InvocationExpressionSyntax ies = assignmentSource as InvocationExpressionSyntax;
                                            bool isLegal = ies != null && usingMandatoryAttributeFinder
                                                .HasUsingMandatoryReturnTypeSyntax(ies, semanticModel);
                                            context.CancellationToken.ThrowIfCancellationRequested();
                                            if (!isLegal)
                                            {
                                                illegalLhsCopyAssignments.Add((assignmentTarget, assignmentSource));
                                            }
                                        }

                                        var badAssignments =
                                            illegalLhsCopyAssignments.Count == illegalLhsCopyAssignments.Capacity
                                                ? illegalLhsCopyAssignments.MoveToImmutable()
                                                : illegalLhsCopyAssignments.ToImmutable();

                                        foreach ((IdentifierNameSyntax assignmentTarget,
                                            ExpressionSyntax _) in badAssignments)
                                        {
                                            var diagnostic = Diagnostic.Create(UsingMandatoryNoCopyAssignment,
                                                assignmentTarget.GetLocation(), ts.Name);
                                            context.ReportDiagnostic(diagnostic);
                                        }

                                        //search for standard method invocations other than by constant reference
                                        var invocationsWhereProtectedInArgumentList =
                                            (from n in searchMe.DescendantNodes()
                                                where n is InvocationExpressionSyntax
                                                let ies = (InvocationExpressionSyntax) n
                                                where ThrowIfCanc(context.CancellationToken)
                                                from ArgumentSyntax arg in ies.ArgumentList.Arguments
                                                let identifier = arg.Expression as IdentifierNameSyntax
                                                where identifier != null
                                                let symbolIn =
                                                    semanticModel.GetSymbolInfo(identifier, context.CancellationToken)
                                                where symbolIn.Symbol != null
                                                where SymbolEqualityComparer.Default.Equals(symbolIn.Symbol,
                                                    protectedSymbol)
                                                select (arg, symbolIn.Symbol));

                                        var illegalBcNotByConstRef = invocationsWhereProtectedInArgumentList
                                            .Where(inv => inv.arg.RefKindKeyword.Kind() != SyntaxKind.InKeyword)
                                            .ToImmutableArray();


                                        //report illegal locked reosure method invocations for standard method.
                                        foreach (var item in illegalBcNotByConstRef)
                                        {
                                            Diagnostic d = Diagnostic.Create(UsingMandatoryIllegalPass,
                                                item.arg.GetLocation(), item.Symbol.Locations,
                                                ts.Name, item.Symbol.Name);
                                            context.ReportDiagnostic(d);
                                        }

                                        //Find method member access with 'dot methodinvocationexpression' with protectedResource being the item whose method 
                                        //is being member-accessed
                                        var memberAccessExpressionsWhereLhsIsLockedResource =
                                            from n in searchMe.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                                            where n.Kind() == SyntaxKind.SimpleMemberAccessExpression
                                            let found = FindMemberAccessOperands(n)
                                            where found.accessedObject != null && found.accessedMethod != null
                                            let symbols = FindMatchingSymbols(ts, protectedSymbol, found.accessedObject,
                                                found.accessedMethod, semanticModel, context.CancellationToken)
                                            where symbols.accessedObjSymb != null && symbols.methodSymbol != null
                                            select (n, found.accessedObject, found.accessedMethod,
                                                symbols.accessedObjSymb,
                                                symbols.methodSymbol);

                                        //Narrow it down to member accesses that 1- are really extension-method invocations 
                                        //and 2- break the rule because the this parameter is being passed by value or non-const reference
                                        ImmutableArray<(IMethodSymbol IMethodSymbol, IMethodSymbol ReducedFrom,
                                            IdentifierNameSyntax MethodIdentifier)
                                        > illegalExtensionMethods =
                                            (from val in memberAccessExpressionsWhereLhsIsLockedResource
                                                let methSymb = val.methodSymbol
                                                where methSymb?.IsExtensionMethod == true
                                                let reduced = methSymb.ReducedFrom
                                                where reduced != null
                                                let methId = val.accessedMethod
                                                let firstParam = reduced?.Parameters.FirstOrDefault()
                                                where firstParam != null && firstParam.RefKind != RefKind.In
                                                select (methSymb, reduced, methId)).ToImmutableArray();
                                        //emit diagnostic for such illegal by-value or by-non-const reference extension method invocation
                                        //on protected resource
                                        foreach (var item in illegalExtensionMethods)
                                        {

                                            // $"Illegal invocation of method [{item.MethodIdentifier}] which is an extension method invocation
                                            // of [{item.ReducedFrom.Name}] declared at [{TextFromImmutLocArr(item.ReducedFrom.Locations)}]: the locked resource object [{protectedSymbol.Name}, declared at {TextFromImmutLocArr(protectedSymbol.Locations)}] of type [{ts.Name}] is passed {TextFromRefKind(item.ReducedFrom.Parameters.FirstOrDefault()?.RefKind ?? RefKind.None)}.  Consider updating the extension method signature to accept the first parameter by constant reference: (e.g. static void PrintLockedResource(this in LockedResource lr).";
                                            Diagnostic d = Diagnostic.Create(UsingMandatoryIllegalPassExtMeth,
                                                item.MethodIdentifier.GetLocation(),
                                                item.ReducedFrom.Locations.Union(protectedSymbol.Locations),
                                                item.MethodIdentifier, item.ReducedFrom.Name,
                                                TextFromImmutLocArr(item.ReducedFrom.Locations), protectedSymbol,
                                                TextFromImmutLocArr(protectedSymbol.Locations), ts.Name,
                                                TextFromRefKind(
                                                    item.ReducedFrom.Parameters.FirstOrDefault()?.RefKind ??
                                                    RefKind.None));
                                            context.ReportDiagnostic(d);
                                            context.CancellationToken.ThrowIfCancellationRequested();
                                        }

                                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                        if (refStructAttribute != null && protectedType.IsRefLikeType(refStructAttribute) && compilation != null)
                                        {
                                            //Now the fun begins.  We are looking for any ref struct variables whose
                                            //scope overlaps with the scope of local locked resource object that was obtained from
                                            //the vault.  If any such scope-overlapping ref struct locals contain ... at any level of object nesting
                                            //a (non-static ... ref structs can't be static fields anywho) field of the same type of the locked resource object,
                                            //it is disallowed.

                                            SemanticModel model = context.SemanticModel;
                                            CancellationToken token = context.CancellationToken;

                                            //Scan the locked resource object's scope (searchMe ... either a using statement or the enclosing scope of a using declaration)
                                            //for locals that are ref structs.  This result, of course, will include the locked resource object, but since it can't recursively contain
                                            //an field of its own type, that will not be a problem.  For each such ref struct local, find it's identifier syntax, its local symbol and it's type
                                            IEnumerable<(IdentifierNameSyntax IdentifierSyntax, ILocalSymbol LocalSymbol, INamedTypeSymbol LocalSymbolType )> identifierNameSyntax = 
                                                from item in searchMe.DescendantNodesAndSelf()
                                                    .OfType<IdentifierNameSyntax>()
                                                let symbolInfo = model.GetSymbolInfo(item, token)
                                                where symbolInfo.Symbol is ILocalSymbol
                                                let localSymbol = (ILocalSymbol) symbolInfo.Symbol
                                                let symbolType = localSymbol.Type as INamedTypeSymbol 
                                                where symbolType != null && ThrowIfCanc(token) &&
                                                symbolType.IsRefLikeType(refStructAttribute)
                                                select (item, localSymbol, symbolType);

                                            IEnumerable<(SyntaxToken Identifier, ILocalSymbol LocalSymbol,
                                                INamedTypeSymbol LocalSymbolType)> identifierTokens =
                                                (from findTokenNode in searchMe.DescendantNodesAndSelf()
                                                        .OfType<VariableDeclarationSyntax>()
                                                    from declarator in findTokenNode.Variables
                                                    let symbolInfo = model.GetDeclaredSymbol(declarator,
                                                        context.CancellationToken)
                                                    where symbolInfo is ILocalSymbol
                                                    let localSymbol = (ILocalSymbol) symbolInfo
                                                    let symbolType = localSymbol.Type as INamedTypeSymbol
                                                    where symbolType != null && ThrowIfCanc(token) &&
                                                          symbolType.IsRefLikeType(refStructAttribute)
                                                    select (declarator.Identifier, localSymbol, symbolType));


                                            IEnumerable<(SyntaxNodeOrToken Identifier, ILocalSymbol LocalSymbol,
                                                INamedTypeSymbol LocalSymbolType)> merged = Merge(identifierTokens,
                                                identifierNameSyntax, token);
                                                         //Now we go through the results of the query above and look for ref structs that contain fields of the prohibited type 
                                                         //(the locked resource object type).  We can eliminate from our recursive search any fields that are not ref structs 
                                                         //because only ref structs can contain ref structs.  If we find a field of the locked resource type in any local variable
                                                         //(as above, overlapping in scope with our LockedResource object), generate a compiler error.
                                                         (SyntaxNodeOrToken badFieldHavingIdentifierSyntax, ILocalSymbol localSymbolWithBadField, INamedTypeSymbol typeOfBadFieldHavingLocal, IFieldSymbol badFieldSymbol) =
                                                                                     (from item in merged
                                                                                      where item.Identifier != null && item.LocalSymbol != null && item.LocalSymbolType != null
                                                                                      let identifierWithBadField = item.Identifier
                                                                                      let localInstanceOfIdentifierWithBadField = item.LocalSymbol
                                                                                      let typeOfIdentifierWithBadField = item.LocalSymbolType
                                                                                      let result =
                                       FindRefStructFieldOfTypeInObjectMap(typeOfIdentifierWithBadField, //this func does the recursive exam
                                           protectedType, token, refStructAttribute)
                                                                                      where result.FoundMatchingRefTypeInObjectMap && result.FieldSymb != null
                                                                                      select (identifierWithBadField, localInstanceOfIdentifierWithBadField, typeOfIdentifierWithBadField, result.FieldSymb)).FirstOrDefault();
                                            Debug.Assert(
                                                ((badFieldHavingIdentifierSyntax == null) ==
                                                 (localSymbolWithBadField == null)) &&
                                                ((typeOfBadFieldHavingLocal == null) == (localSymbolWithBadField == null) && ((typeOfBadFieldHavingLocal == null) == (badFieldSymbol == null))),
                                                "All should be null or none.");
                                            if (badFieldHavingIdentifierSyntax != null) 
                                            {
                                                //we found that a local overlapping in scope with the locked resource object ... 
                                                //at some level of nesting ... contains a field of the locked resource object's type.  
                                                //this is not allowed.

                                                //find the syntax that declared our protected resource object local
                                                VariableDeclaratorSyntax protectedResourceDeclaration =
                                                    searchMe.DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>()
                                                        .FirstOrDefault();
                                                //get its location
                                                Location syntaxLocation = protectedResourceDeclaration?.GetLocation() ??
                                                                          searchMe.GetLocation();
                                                //get the location of the field of the protected resource type in the 
                                                //ref struct that overlaps in scope with the bad resource.
                                                Location fieldLocation = badFieldSymbol.Locations.FirstOrDefault();

                                                //join these locations together to match expectation of diagnostic's 
                                                //factory function
                                                IEnumerable<Location> moreLocations = syntaxLocation.AsEnumerable()
                                                    .Concat(fieldLocation != null
                                                        ? fieldLocation.AsEnumerable()
                                                        : Enumerable.Empty<Location>());
                                                
                                                //Create diagnostic that will trigger compilation error
                                                Diagnostic dx = Diagnostic.Create(
                                                    UsingMandatoryNoLockedResourceWrappersAllowedInScope,
                                                    localSymbolWithBadField.Locations.FirstOrDefault(),
                                                    localSymbolWithBadField.Locations.Skip(1).Concat(moreLocations),
                                                    localSymbolWithBadField.Name, typeOfBadFieldHavingLocal.Name, badFieldSymbol.Name,
                                                    protectedType.Name,  protectedResourceDeclaration ?? searchMe);
                                                DebugLog.Log(dx.ToString());
                                                context.ReportDiagnostic(dx);
                                                //emit diagnostic here.
                                                //DebugLog.Log("Bad wrapper detected.  Bad local: [" +
                                                //             localSymbolWithBadField +
                                                //             "]; Type of Local: [" + typeOfBadFieldHavingLocal +
                                                //             "]; Bad field: [" + badFieldSymbol +
                                                //             "]; Prohibited Type: [" + nts + "].");
                                            }

                                            //Next step of the fun.  Examine all local declarations that don't have usings in them
                                            IEnumerable<(LocalDeclarationStatementSyntax Declaration, bool HasUsingSyntax, SyntaxToken
                                                IdentifierToken, EqualsValueClauseSyntax EqValClause)> scannedDeclarations  =
                                                (from myNode in searchMe.DescendantNodes()
                                                    .OfType<LocalDeclarationStatementSyntax>()
                                                where ThrowIfCanc(context.CancellationToken)
                                                let hasUsing = myNode.UsingKeyword != default
                                                let varDecl = myNode.Declaration
                                                let varTypeSyntax = varDecl.Type
                                                where varTypeSyntax != null && ThrowIfCanc(context.CancellationToken)
                                                let declaredType =
                                                    model.GetTypeInfo(varTypeSyntax).ConvertedType as INamedTypeSymbol
                                                where ThrowIfCanc(context.CancellationToken) &&
                                                      SymbolEqualityComparer.Default.Equals(protectedType,
                                                          declaredType)
                                                let declaredVariables = varDecl.Variables
                                                from declarator in declaredVariables
                                                let identifierToken = declarator.Identifier
                                                let init = declarator.Initializer
                                                where ThrowIfCanc(context.CancellationToken) 
                                                select (myNode, hasUsing, identifierToken, init));

                                            IEnumerable<(UsingStatementSyntax Statement, SyntaxToken Identifier, EqualsValueClauseSyntax EqVal)> scannedUsingStatementSyntaxes =
                                                (from someNode in searchMe.DescendantNodes()
                                                        .OfType<UsingStatementSyntax>()
                                                    let variableDeclaration = someNode.Declaration
                                                    where variableDeclaration != null &&
                                                          ThrowIfCanc(context.CancellationToken)
                                                    let typeSyntax = variableDeclaration.Type
                                                    where typeSyntax != null && ThrowIfCanc(context.CancellationToken)
                                                    let typeInfo =
                                                        model.GetTypeInfo(typeSyntax).ConvertedType as INamedTypeSymbol
                                                    where SymbolEqualityComparer.Default.Equals(protectedType, typeInfo)
                                                    let vDeclarator = variableDeclaration?.Variables.FirstOrDefault()
                                                    where vDeclarator != null && ThrowIfCanc(context.CancellationToken)
                                                    let identifierToken = vDeclarator.Identifier
                                                    let eqVClause = vDeclarator?.Initializer
                                                    where eqVClause != null && ThrowIfCanc(context.CancellationToken)
                                                    select (someNode, identifierToken, eqVClause));

                                            var illegalStatementBuilder = ImmutableArray
                                                .CreateBuilder<(StatementSyntax IllegalDeclaration, SyntaxToken
                                                    IdentifierToken)>();
                                            foreach (var item in scannedDeclarations)
                                            {
                                                if (item.HasUsingSyntax)
                                                {
                                                    EqualsValueClauseSyntax eqValSyntax = item.EqValClause;
                                                    bool legal = eqValSyntax?.Value is InvocationExpressionSyntax ies &&
                                                                 usingMandatoryAttributeFinder
                                                                     .HasUsingMandatoryReturnTypeSyntax(ies,
                                                                         semanticModel);
                                                    context.CancellationToken.ThrowIfCancellationRequested();
                                                    if (!legal)
                                                    {
                                                        illegalStatementBuilder.Add((item.Declaration, item.IdentifierToken));
                                                    }
                                                }
                                                else
                                                {
                                                    illegalStatementBuilder.Add(
                                                        (item.Declaration, item.IdentifierToken));
                                                }
                                            }

                                            foreach (var item in scannedUsingStatementSyntaxes)
                                            {
                                                bool legal = item.EqVal?.Value is InvocationExpressionSyntax ies &&
                                                             usingMandatoryAttributeFinder
                                                                 .HasUsingMandatoryReturnTypeSyntax(ies,
                                                                     semanticModel);
                                                context.CancellationToken.ThrowIfCancellationRequested();
                                                if (!legal)
                                                {
                                                    illegalStatementBuilder.Add((item.Statement, item.Identifier));
                                                }
                                            }

                                            var irregularDeclarations =
                                                illegalStatementBuilder.Capacity == illegalStatementBuilder.Count
                                                    ? illegalStatementBuilder.MoveToImmutable()
                                                    : illegalStatementBuilder.ToImmutable();
                                            DebugLog.Log("Illegal token count: [" + irregularDeclarations.Length + "].");

                                            {
                                                string lockedResourceString =
                                                    protectedSymbol?.ToDisplayString() ?? string.Empty;

                                                foreach ((StatementSyntax declaration, SyntaxToken identifier) in
                                                    irregularDeclarations)
                                                {
                                                    context.CancellationToken.ThrowIfCancellationRequested();
                                                    VariableDeclarationSyntax irregularlyDeclared;
                                                    string tokenName = identifier.Text ?? string.Empty;
                                                    switch (declaration)
                                                    {
                                                        case LocalDeclarationStatementSyntax lds:
                                                            irregularlyDeclared = lds.Declaration;
                                                            break;
                                                        case UsingStatementSyntax ustatement:
                                                            irregularlyDeclared = ustatement.Declaration;
                                                            break;
                                                        default:
                                                            irregularlyDeclared = null;
                                                            break;
                                                    }

                                                    if (irregularlyDeclared != null)
                                                    {
                                                        Location irregularlyDeclaredLocation = identifier.GetLocation();
                                                        if (irregularlyDeclaredLocation != null)
                                                        {
                                                            var dx = Diagnostic.Create(
                                                                NoIrregularLockedResourcesAllowedInScope,
                                                                irregularlyDeclaredLocation, protectedSymbol.Locations,
                                                                lockedResourceString, tokenName);
                                                            DebugLog.Log(dx.ToString());
                                                            context.ReportDiagnostic(dx);
                                                        }
                                                    }
                                                }
                                            }


                                            //foreach (var illegalDeclaration in scannedDeclarations)
                                            //{
                                            //    DebugLog.Log(
                                            //            "Illegal declaration detected. Has using: ["+illegalDeclaration.HasUsingSyntax + "]; Declaration: [" +
                                            //            illegalDeclaration + "]; Identifier: [" +
                                            //            illegalDeclaration.IdentifierToken.Text + "].");
                                            //}


                                        }
                                    }
                                }
                            }
                            else
                            {
                                DebugLog.Log(
                                    $"Expected {nameof(ts)} to be of type {nameof(INamedTypeSymbol)} " +
                                    $"and {nameof(ins)} to be non null.  {nameof(ts)} is of type " +
                                    $"{ts?.GetType().Name ?? "NULL"} and {nameof(ins)}'s value is " +
                                    $"{ins?.ToString() ?? string.Empty}.");
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

            #region Local Helpers

            static string TextFromRefKind(RefKind rk)
            {
                string ret;
                switch (rk)
                {
                    case RefKind.None:
                        ret = "by value";
                        break;
                    case RefKind.Ref:
                        ret = "by mutable reference";
                        break;
                    case RefKind.Out:
                        ret = "by write-mandatory 'out' mutable reference.";
                        break;
                    case RefKind.In:
                        ret = "by constant reference.";
                        break;
                    default:
                        TraceLog.Log("New and unexpected value to refkind enum: [" + rk + "] not account for in [" +
                                     nameof(AnalyzeInvocationForUmCompliance) +
                                     "] analyzer method -- will treat as pass by value.");
                        ret = "by value";
                        break;
                }

                return ret;
            }

            static string TextFromImmutLocArr(ImmutableArray<Location> locations)
            {
                const string defaultRet = "UKNOWN";
                string ret;

                if (!locations.IsDefaultOrEmpty)
                {
                    var first = locations.FirstOrDefault();
                    ret = first != null ? first.ToString() : defaultRet;
                }
                else
                {
                    ret = defaultRet;
                }

                return ret;
            }

            static bool ThrowIfCanc(CancellationToken tkn)
            {
                tkn.ThrowIfCancellationRequested();
                return true;
            }
            static (SymbolInfo Info, ITypeSymbol TypeSymbol, VariableDeclaratorSyntax NameSyntax) FindTypeInfo(VariableDeclarationSyntax n, SyntaxNodeAnalysisContext context)
            {
                var model = context.SemanticModel;
                SymbolInfo x = model.GetSymbolInfo(n.Type);
                ITypeSymbol sym = (ITypeSymbol)x.Symbol;
                VariableDeclaratorSyntax identifier =
                    n.ChildNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                Debug.Assert(sym != null && identifier != null);
                return (x, sym, identifier);
            }
            static (ITypeSymbol accessedObjSymb, IMethodSymbol methodSymbol) FindMatchingSymbols(
                ITypeSymbol protectedType, ISymbol protectedLocal,
                IdentifierNameSyntax accesObjSyntax, IdentifierNameSyntax methName,
                SemanticModel model, CancellationToken tkn)
            {
                ITypeSymbol tsForProtectedResource;
                SymbolInfo siForAccessObj = model.GetSymbolInfo(accesObjSyntax, tkn);
                tsForProtectedResource = SymbolEqualityComparer.Default.Equals(siForAccessObj.Symbol, protectedLocal) ? protectedType : null;
                IMethodSymbol ms = tsForProtectedResource != null ? (model.GetSymbolInfo(methName, tkn).Symbol as IMethodSymbol) : null;
                tsForProtectedResource = ms != null ? tsForProtectedResource : null;
                Debug.Assert((ms == null) == (tsForProtectedResource == null));
                return (tsForProtectedResource, ms);

            }
            static (IdentifierNameSyntax accessedObject, IdentifierNameSyntax accessedMethod)
                FindMemberAccessOperands(MemberAccessExpressionSyntax maexsynt)
            {
                ImmutableArray<IdentifierNameSyntax> arr = maexsynt.ChildNodes()
                    .OfType<IdentifierNameSyntax>().ToImmutableArray();
                if (arr.Length == 2)
                {
                    return (arr[0], arr[1]);
                }

                return (null, null);
            } 
            #endregion
        }

        private IEnumerable<(SyntaxNodeOrToken Identifier, ILocalSymbol LocalSymbol, INamedTypeSymbol LocalSymbolType)> 
            Merge(IEnumerable<(SyntaxToken Identifier, ILocalSymbol LocalSymbol, INamedTypeSymbol LocalSymbolType)> identifierTokens, 
                IEnumerable<(IdentifierNameSyntax IdentifierSyntax, ILocalSymbol LocalSymbol, INamedTypeSymbol LocalSymbolType)> 
                    identifierNameSyntax, CancellationToken token)
        {
#pragma warning disable RS1024 // Compare symbols correctly
            HashSet<ILocalSymbol> symbols = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
#pragma warning restore RS1024 // Compare symbols correctly

            foreach (var item in identifierTokens)
            {
                token.ThrowIfCancellationRequested();
                if (item.LocalSymbol != null && item.Identifier != default && symbols.Add(item.LocalSymbol))
                {
                    yield return (item.Identifier, item.LocalSymbol, item.LocalSymbolType);
                }
            }

            foreach (var item in identifierNameSyntax)
            {
                token.ThrowIfCancellationRequested();
                if (item.LocalSymbol != null && item.IdentifierSyntax != null && symbols.Add(item.LocalSymbol))
                {
                    yield return (item.IdentifierSyntax, item.LocalSymbol, item.LocalSymbolType);
                }
            }
        }

        private void AnalyzeMethodInvokeForVsTpCompliance(SyntaxNodeAnalysisContext context)
        {
            const string methodName = nameof(AnalyzeMethodInvokeForVsTpCompliance);
            using var _ = EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(DotNetVaultAnalyzer), methodName, context);
            try
            {
                CSharpCompilation compilation = (CSharpCompilation) context.Compilation;
                {
                    INamedTypeSymbol vaultSafeTpAttribSymbol = FindVaultSafeTypeParamAttribute(compilation);
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
                                            select symbol).ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                                        var vaultSafeAnalyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
                                        var nonConformingSymbols =
                                            set.Where(sym2 =>
                                                {
                                                    bool ret;
                                                    switch (sym2)
                                                    {
                                                        case INamedTypeSymbol nts:
                                                            ret = !vaultSafeAnalyzer.IsTypeVaultSafe(nts,
                                                                compilation);
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
                string methName = methodSymbol?.Name ?? string.Empty;
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
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
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
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
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
                        $"Syntax node [{context.Node}] cannot be evaluated for Illegal Use of " +
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
                             where n != null && SymbolEqualityComparer.Default.Equals(n.ConstructedFrom, vaultSymbol) 
                                             && ThrowOnToken(context.CancellationToken)
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
                            DebugLog.Log(
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
        /// <summary>
        /// Recursively examine the ref-struct denoted by <paramref name="nts"/> for any fields of the prohibited type,
        /// denoted by <paramref name="forbiddenFieldType"/>.  <paramref name="nts"/> should be a ref struct.  <paramref name="forbiddenFieldType"/>
        /// should be the type of the locked resource object (also a ref struct) whose scope overlaps with a local ref struct of <paramref name="nts"/>'s type.
        ///
        /// Non ref struct fields can be ignored: only ref structs can contain ref struct fields.
        ///
        /// Static fields can be ignored: ref structs cannot be stored in static memory
        ///
        /// <paramref name="refStructAttribute"/> is a type symbol referring to the <see cref="RefStructAttribute"/>. 
        /// Currently, ref structs from the analyzer are loaded during testing from metadata.  Loading the types
        /// from metadata seems to cause a bug in Roslyn where ref structs are not recognized as such.
        /// The <see cref="RefStructAttribute"/> has therefore been applied to all ref-structs defined in the library
        /// so that they can be recognized as ref structs .... when the analyzer analyzes types defined within itself.
        /// I believe this is a testing artifact and ... if the bug is fixed, this may not be required.  
        /// The parameter <paramref name="refStructAttribute"/> is used to identify ref structs defined in the analyzer itself. Thus if a
        /// named type symbol's .IsRefLikeType property is true OR if it is decorated with the <seealso cref="RefStructAttribute"/>,
        /// we conclude that that type is a ref struct.
        ///
        /// <paramref name="token"/> is a cancellation token the compiler can use to cancel this analysis.
        /// </summary>
        /// <returns>When the first (if any) such field is identified, returns true and the offending field symbol which will be of
        /// the type denoted by <paramref name="forbiddenFieldType"/>.
        ///
        /// If no such is found, returns (false, null)</returns>
        /// <remarks>It is the callers responsibility to ensure that the parameters submitted are non-null and are what they purport to be.</remarks>
        private (bool FoundMatchingRefTypeInObjectMap, IFieldSymbol FieldSymb)
            FindRefStructFieldOfTypeInObjectMap([NotNull] INamedTypeSymbol nts,
                [NotNull] INamedTypeSymbol forbiddenFieldType, CancellationToken token, [NotNull] INamedTypeSymbol refStructAttribute)
        {
            Debug.Assert(nts != null && forbiddenFieldType != null && refStructAttribute != null);

            bool foundIt;
            IFieldSymbol offendingField;
            if (nts.IsRefLikeType(refStructAttribute) && forbiddenFieldType.IsRefLikeType(refStructAttribute))
            {
                token.ThrowIfCancellationRequested();
                //find all fields of ref struct type in nts
                IEnumerable<(IFieldSymbol IFieldSymbol, INamedTypeSymbol TypeSymbol)> searchMe =
                    EnumerateNonStaticRefStructFields(nts, refStructAttribute, token);

                //iterate all such fields
                foreach (var item in searchMe)
                {
                    token.ThrowIfCancellationRequested();
                    //evaluate each field recursively to see if it (or any of ITS non-static ref-struct fields)
                    //is of the forbidden type
                    (foundIt, offendingField, _) = EvaluateRecursively(item.TypeSymbol, item.IFieldSymbol,
                        refStructAttribute, forbiddenFieldType, token);
                    Debug.Assert(foundIt == (offendingField != null),
                        "found it symmetrically implies that the offending field is non-null.");
                    if (foundIt)
                    {
                        return (true, offendingField); //stop as soon as it is found.
                    }
                }
            }
            //didn't find it
            return (false, null);

            static IEnumerable<(IFieldSymbol FieldSymbol, INamedTypeSymbol TypeSymbol)> EnumerateNonStaticRefStructFields(INamedTypeSymbol enumerateMyFields,
                INamedTypeSymbol refStructAttribute, CancellationToken token)
            {
                Debug.Assert(enumerateMyFields != null && refStructAttribute != null && enumerateMyFields.IsRefLikeType(refStructAttribute), "nts should be a ref struct symbol");
                return (from fieldSymbol in enumerateMyFields.GetMembers().OfType<IFieldSymbol>()
                        where fieldSymbol?.IsStatic == false && ThrowIfCanc(token)
                        let fieldType = fieldSymbol.Type as INamedTypeSymbol
                        where fieldType != null && fieldType.IsRefLikeType(refStructAttribute)
                        select (fieldSymbol, fieldType));
            }

            static (bool foundIt, IFieldSymbol FirstFoundForbiddenField, INamedTypeSymbol ForbiddenType)
                EvaluateRecursively(INamedTypeSymbol evalMeRecursively, IFieldSymbol namedTypeField, INamedTypeSymbol refStructSymbol, INamedTypeSymbol forbiddenType, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                //check the supplied type itself to see if it is of the forbidden type.  If so, we are done -- we found an offending field
                if (SymbolEqualityComparer.Default.Equals(evalMeRecursively, forbiddenType))
                {
                    return (true, namedTypeField, forbiddenType);
                }

                //now get the sub fields that are non-static ref structs (retrieving the field symbol and type symbol
                var subFields = EnumerateNonStaticRefStructFields(evalMeRecursively, refStructSymbol, token).ToImmutableArray();

                foreach (var subItemPair in subFields.Where(
                    sif => sif.FieldSymbol != null && sif.TypeSymbol != null))
                {
                    //for each such sub field this function will call itself to evaluate the sub field
                    return EvaluateRecursively(subItemPair.TypeSymbol, subItemPair.FieldSymbol, refStructSymbol,
                        forbiddenType, token);
                }
                //There were not any non-static sub fields that were of ref struct type
                return (false, null, null);
            }

            //helper used so compiler can cancel this operation in the middle of linq queries
            static bool ThrowIfCanc(CancellationToken t)
            {
                t.ThrowIfCancellationRequested();
                return true;
            }

        }

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
            compilation?.GetTypeByMetadataName(typeof(UsingMandatoryAttribute).FullName ?? string.Empty);

        private TypeSymbolVsTpAnalysisResult AnalyzeTypeSymbolVsTpAnal([NotNull] INamedTypeSymbol namedType,
            [NotNull] INamedTypeSymbol vsTpAttrib, [NotNull] Compilation compilation, CancellationToken token)
        {
            IEqualityComparer<INamedTypeSymbol> comp = SymbolEqualityComparer.Default;
#pragma warning disable RS1024 // Compare symbols correctly
            HashSet<INamedTypeSymbol> typeSet = new HashSet<INamedTypeSymbol>(comp);
#pragma warning restore RS1024 // Compare symbols correctly
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
                var syntaxTree = matchingData.ApplicationSyntaxReference?.SyntaxTree;
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
                        AttributeArgumentSyntax firstArgument = attribInQuestion.ArgumentList.Arguments[0];
                        var firstArgumentExpression = firstArgument.Expression;
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
#pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer (not sure there is any other choice)
                                var semanticModel = model.GetSemanticModel(syntaxTree);
#pragma warning restore RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
                                var help = semanticModel.GetConstantValue(firstArgumentExpression);
                                ret = help.HasValue && help.Value is bool b && b;
                                break;
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
                    .Add(NotVsProtectableTypeCannotBeStoredInVault)
                    .Add(UsingMandatoryAttributeAssignmentMustBeToVariableDeclaredInline)
                    .Add(NotDirectlyInvocableDiagnosticDescriptor)
                    .Add(UnjustifiedEarlyDisposeDiagnostic)
                    .Add(JustificationOfEarlyDispose)
                    .Add(NoExplicitByRefAlias)
                    .Add(UsingMandatoryNoCopyAssignment)
                    .Add(UsingMandatoryIllegalPass)
                    .Add(UsingMandatoryIllegalPassExtMeth)
                    .Add(NoRefStructAttrExceptOnRefStruct)
                    .Add(UsingMandatoryNoLockedResourceWrappersAllowedInScope)
                    .Add(NoIrregularLockedResourcesAllowedInScope)
                    .Add(ReportWhiteListFilePathsOnRequest);
            }
            catch (Exception ex)
            {
                TraceLog.Log(ex);
                throw;
            }
        }


        // ReSharper disable InconsistentNaming
        private static readonly LocalizableString Vst_Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Vst_MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Vst_Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "VaultSafety";

        private const string ReportWhiteLists_Title = "Report on location of Whitelist files";
        private const string ReportWhiteLists_MessageFormat =
            "The vault-safe whitelist is found at [{0}] and {1}.  The conditionally vault-safe generic whitelist is found at [{2}] and {3}.";
        private const string ReportWhiteLists_Description =
            "Optionally report on the location of the whitelist files used by the analyzer.";

        private const string OnlyOnRefStruct_Title =
            "The " + nameof(RefStructAttribute) + " may only be applied to ref structs";
        private const string OnlyOnRefStruct_MessageFormat =
            "The type [{0}] is annotated with the " + nameof(RefStructAttribute) +
            " attribute but is not a ref struct";
        private const string OnlyOnRefStruct_Description =
            "The " + nameof(RefStructAttribute) +
            " attribute is intended to identify ref structs.  " +
            "Annotating types that are not ref structs with this attribute is forbidden.";

        private const string ExplicitByRef_Illegal_Title =
            "Explicitly access property of protected resource is forbidden";
        private const string ExplicitByRef_Illegal_MessageFormat =
            "Explicit by-reference aliasing of the LockedVaultObject's Value property is forbidden";
        private const string ExplicitByRef_Description =
            "Access the Value property by reference as mediated by the LockedResourceObject.  Explicitly by-ref aliasing is " +
            "forbidden because that reference might outlive the lock and cause unsynchronized access to the protected resource.";

        private const string UsingMandatory_IllegalCopyAssign_Title =
            "Locked resources annotated with the " + nameof(NoCopyAttribute) + " and protected by a using statement may not be copied through assignment";
        private const string UsingMandatory_IllegalCopyAssign_MessageFormat =
            "The locked resource of type [{0}] named [{1}] may not be copy assigned";
        private const string UsingMandatory_IllegalCopyAssign_Description =
            "Copying a locked resource provides no value or usefulness and may allow unsynchronized access to the protected resource.";

        private const string UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_Title =
            "No locked resource objects not subject to the using mandatory rules may be declared in overlapping scope with a protected locked resource object";
        private const string UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_MessageFormat =
            "The locked resource object [{0}] may not be declared within overlapping scope with the protected locked resource object  [{1}]";
        private const string UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_Description =
            "Locked resource objects that are not returned from an invocation annotating its return value as UsingMandatory may not appear in scope " +
            "with locked resource objects, that are subject to UsingMandatory, and are of the same type.";


        private const string UsingMandatory_NoProtectedResourceWrappersAllowedInScope_Title =
            "No ref structs containing fields of the protected resource are allowed in the same scope as the protected resource";
        private const string UsingMandatory_NoProtectedResourceWrappersAllowedInScope_MessageFormat =
            "The local [{0}] (type: [{1}]), at some level of nesting, contains a field named [{2}] that is of locked resource type [{3}] and may not appear " +
            "within the same scope as the protected resource ([{4}])";
        private const string UsingMandatory_NoProtectedResourceWrappersAllowedInScope_Description =
            "Ref structs containing fields of the same type as locked resource object are not allowed in the same scope as the locked resource object.";

        private const string UsingMandatory_IllegalPass_Title =
            "Locked resources annotated with the " + nameof(NoCopyAttribute) +
            " and protected by a using statement may not be passed by value or non-constant reference";
        private const string UsingMandatory_IllegalPass_MessageFormat =
            "The locked resource of type [{0}] named [{1}] not be passed by value or by non-constant reference.  " +
            "Consider using the 'in' keyword in both the called method signature and at each callsite.";
        private const string UsingMandatory_IllegalPass_Description =
            "Locked resources are designed to be short-lived objects and may have non-trivial copy times.  " +
            "Thus, passing them to a method is only allowed by constant reference.  " +
            "Use the 'in' keyword in the called method signature and at the callsite.";
        private const string UsingMandatory_IllegalPass_ExtMeth_MessageFormat =
            "Illegal invocation of method [{0}] which is an extension method invocation of [{1}] " +
            "declared at [{2}]: the locked resource object [{3}, declared at {4}] of type [{5}] " +
            "is passed {6}.  Consider updating the extension method signature to accept " +
            "the first parameter by constant reference: " +
            "(e.g. static void PrintLockedResource(this in LockedResource lr)).";
        
        private const string UED_Title = "Invocation of early dispose method requires justification";
        private const string UED_MessageFormat =
            "The method [{0}] is an early disposal method on a LockedResourceObject.  The enclosing method in which it is called [{1}]" +
            " does not provide any justification for the call.  There are two acceptable reasons for early dispose: " +
            "1-a custom LockedResource Object is disposing the object it wraps in its own method, or 2-a disposal on exception from a " +
            "Lock, Spinlock or similar method. Every method that makes such a call must be annotated with the " +
            nameof(EarlyReleaseJustificationAttribute) +
            " constructed with the justification for the early dispose.  " +
            "In no event should an early-disposed object ever be available for further access by client code.";
        private const string UED_Description =
            "Calls to a locked resource object's early dispose method is only correct under very limited circumstances.  " +
            "Accordingly, any method which calls a LockedResourceObject's early dispose method must document " +
            "the justification for it with the " + nameof(EarlyReleaseJustificationAttribute) + "attribute.";

        private const string EDJ_Title = "Justification for early dispose of LockedResourceObject";
        private const string EDJ_MessageFormat =
            "The method call [{0}] is an early dispose method of a LockedResourceObject.  " +
            "It is called in method [{1}], which documents the justifcation for the call as: [{2}].  " +
            "Periodic review of this justification during code review is recommended and also" +
            "should establish that no further access to the LockedResourceObject is made in " +
            "the enclosing method after the early dispose and that further access from client code is impossible.";
        private const string EDJ_Description =
            "Documentation of Justification for Early Dispose of LockedResource Object.";

        private const string Ndi_Title = "This method must not be invoked directly";
        private const string Ndi_Message_Format =
            "The method [{0}] is annotated with the " + nameof(NoDirectInvokeAttribute) + " attribute and may not be invoked directly";
        private const string Ndi_Description = "Methods annotated with the " + nameof(NoDirectInvokeAttribute) + " attribute may not be invoked directly.";

        private const string Um_Inline_Title =
            "Value returned from method invocation must be declared inline as part of using statement or declaration";
        private const string Um_Title = "Value returned from method invocation must be a part of using statement";
        private const string Um_MessageFormat = "Value returned by expression [{0}] is not guarded by using construct";
        private const string Um_Inline_Message_Format =
            "Value returned by expression [{0}] is not assigned to a variable declared inline";
        private const string UsingMandatoryAttribute = nameof(UsingMandatoryAttribute);
        private const string Um_Description = "Values returned by methods annotated with the " +
                                              UsingMandatoryAttribute +
                                              " must be subject to a using construct.";

        private const string Um_Inline_Description = "Values returned by methods annotated with the " +
                                                     UsingMandatoryAttribute +
                                                     " if assigned, must be assigned to a variable declared inline.";
        private const string VsDelCapt_Title = "A delegate annotated with the " + nameof(NoNonVsCaptureAttribute) +
                                               " attribute cannot access certain non-vault safe symbols";
        private const string VsDelCapt_MessageFormat = "The delegate is annotated with the " +
                                                       nameof(NoNonVsCaptureAttribute) +
                                                       " attribute but captures or references the following non-vault safe type{0}- [{1}]";
        private const string VsDelCapt_Description =
            "Delegates annotated with the " + nameof(NoNonVsCaptureAttribute) +
            " cannot capture any non-vault safe symbols (including \"this\") and " +
            "cannot refer to any static fields or properties that are not vault-safe.";

        private const string VsNotVsProtect_Title = "A type marked with the " + nameof(NotVsProtectableAttribute) +
                                                 " attribute may not be a protected resource inside a vault";
        private const string VsNotVsProtect_MessageFormat =
            "The type {0} has the {1} attribute.  It cannot be stored as a protected resource inside a vault.";
        private const string VsNotVsProtect_Description =
            "Types marked with the " + nameof(NotVsProtectableAttribute) +
            " are considered VaultSafe for certain purposed.  They are not, however, eligible for protection inside a Vault.";

        //private const string VaultSafeTypeParamAttributeName = nameof(VaultSafeTypeParamAttribute);
        private const string VsTp_Title = "The type argument must be vault-safe";
        private const string VsTp_MessageFormat =
            "The type {0} or one of its ancestors requires a vault-safe type argument but the argument supplied is not vault-safe";
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

        private static readonly DiagnosticDescriptor ReportWhiteListFilePathsOnRequest =
            new DiagnosticDescriptor(DotNetVault_ReportWhiteLists, ReportWhiteLists_Title,
                ReportWhiteLists_MessageFormat, Category, DiagnosticSeverity.Warning, true, ReportWhiteLists_Description);
        private static readonly DiagnosticDescriptor NoIrregularLockedResourcesAllowedInScope =
            new DiagnosticDescriptor(DotNetVault_UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope,
                UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_Title,
                UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_MessageFormat, Category,
                DiagnosticSeverity.Error, true,
                UsingMandatory_IrregularLockedResourceObjects_NotAllowedInScope_Description);
        private static readonly DiagnosticDescriptor NoRefStructAttrExceptOnRefStruct =
            new DiagnosticDescriptor(DotNetVault_OnlyOnRefStruct, OnlyOnRefStruct_Title, OnlyOnRefStruct_MessageFormat,
                Category, DiagnosticSeverity.Error, true, OnlyOnRefStruct_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryNoCopyAssignment = new DiagnosticDescriptor(
            DotNetVault_UsingMandatory_NoCopyAssignment, UsingMandatory_IllegalCopyAssign_Title,
            UsingMandatory_IllegalCopyAssign_MessageFormat, Category, DiagnosticSeverity.Error, true,
            UsingMandatory_IllegalCopyAssign_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryIllegalPass = new DiagnosticDescriptor(
            DotNetVault_UsingMandatory_NoCopyIllegalPass, UsingMandatory_IllegalPass_Title,
            UsingMandatory_IllegalPass_MessageFormat, Category, DiagnosticSeverity.Error, true,
            UsingMandatory_IllegalPass_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryIllegalPassExtMeth = new DiagnosticDescriptor(
            DotNetVault_UsingMandatory_NoCopyIllegalPass_ExtMethod, UsingMandatory_IllegalPass_Title,
            UsingMandatory_IllegalPass_ExtMeth_MessageFormat, Category, DiagnosticSeverity.Error, true,
            UsingMandatory_IllegalPass_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryNoLockedResourceWrappersAllowedInScope
            = new DiagnosticDescriptor(DotNetVault_UsingMandatory_NoLockedResourceWrappersAllowedInScope,
                UsingMandatory_NoProtectedResourceWrappersAllowedInScope_Title,
                UsingMandatory_NoProtectedResourceWrappersAllowedInScope_MessageFormat, Category,
                DiagnosticSeverity.Error, true, UsingMandatory_NoProtectedResourceWrappersAllowedInScope_Description);
        private static readonly DiagnosticDescriptor NoExplicitByRefAlias =
            new DiagnosticDescriptor(DotNetVault_NoExplicitByRefAlias, ExplicitByRef_Illegal_Title,
                ExplicitByRef_Illegal_MessageFormat, Category, DiagnosticSeverity.Error, true,
                ExplicitByRef_Description);
        private static readonly DiagnosticDescriptor UsingMandatoryAttributeAssignmentMustBeToVariableDeclaredInline =
            new DiagnosticDescriptor(DiagnosticId_UsingMandatory_Inline, Um_Inline_Title, Um_Inline_Message_Format,
                Category, DiagnosticSeverity.Error, true, Um_Inline_Description);
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
        private static readonly DiagnosticDescriptor NotDirectlyInvocableDiagnosticDescriptor =
            new DiagnosticDescriptor(DotNetVault_NotDirectlyInvocable, Ndi_Title, Ndi_Message_Format, Category,
                DiagnosticSeverity.Error, true, Ndi_Description);
        private static readonly DiagnosticDescriptor UnjustifiedEarlyDisposeDiagnostic =
            new DiagnosticDescriptor(DotNetVault_UnjustifiedEarlyDispose, UED_Title, UED_MessageFormat, Category,
                DiagnosticSeverity.Error, true, UED_Description);
        private static readonly DiagnosticDescriptor JustificationOfEarlyDispose =
            new DiagnosticDescriptor(DotNetVault_EarlyDisposeJustification, EDJ_Title, EDJ_MessageFormat, Category,
                DiagnosticSeverity.Info, true, EDJ_Description);
        private static readonly WriteOnce<ImmutableArray<DiagnosticDescriptor>> TheDiagnosticDescriptors = new WriteOnce<ImmutableArray<DiagnosticDescriptor>>(CreateDiagnosticDescriptors);
        private volatile VaultSafeTypeAnalyzer _analyzer;
        private const bool EnableEntryExitLogging = false;
        #endregion
    }


    internal static class StringConversionExtensions
    {
        //if converter is null, uses .ToString()   
        //e.g. array of ints -> "{1, 75, 3}", empty col -> "{ }"
        internal static string ConvertToCommaSeparatedList<T>([NotNull][ItemNotNull] this IReadOnlyList<T> items,
            [CanBeNull] Func<T, string> converter = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count < 1) return "{ }";

            converter ??= t => t.ToString();
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

    internal static class MiscellaneousExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this T val)
        {
            yield return val;
        }
    }

  
}
