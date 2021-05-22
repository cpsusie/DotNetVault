using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetVault.Attributes;
using DotNetVault.ExtensionMethods;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VaultSafeAnalyzerFactory = System.Func<DotNetVault.Interfaces.IVaultSafeTypeAnalyzer>;
using VaultSafeTypeAnalyzer = DotNetVault.UtilitySources.VaultSafeAnalyzerFactorySource.VaultSafeTypeAnalyzerV2;
using ImmGenTypeLkEnumerator = System.Collections.Immutable.ImmutableDictionary<string, Microsoft.CodeAnalysis.INamedTypeSymbol>.Enumerator;
namespace DotNetVault.UtilitySources
{
    using IImmutableGenericTypeLookup = IImmutableGenericTypeLookup<ImmGenTypeLkEnumerator>;

    internal static class VaultSafeAnalyzerFactorySource
    {
        public static IVaultSafeTypeAnalyzer CreateAnalyzer() => FactoryInstance();

        public static bool SupplyAlternateAnalyzerFactory([NotNull] VaultSafeAnalyzerFactory alternate) =>
            TheDefaultFactory.SetToNonDefaultValue(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        internal static VaultSafeTypeAnalyzer CreateDefaultAnalyzer() => VaultSafeTypeAnalyzer.CreateInstance();

        #region Nested Types

        internal sealed class   VaultSafeTypeAnalyzerV2 : IVaultSafeTypeAnalyzer
        {
            internal static VaultSafeTypeAnalyzerV2 CreateInstance() => new VaultSafeTypeAnalyzerV2();

            internal ImmutableHashSet<string> ConditionalGenericWhiteList => _conditionalGenericWhiteList;
            internal DirectoryInfo DataFolder => TheDataFileDir;
         

            public Task<(bool Result, Exception Error)> IsTypeVaultSafeAsync(INamedTypeSymbol nts,
                Compilation comp, CancellationToken token)
            {
                return Task.Run(delegate
                {
                    bool result;
                    Exception error;
                    try
                    {
                        result = IsTypeVaultSafe(nts, comp, token);
                        error = null;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceLog.Log(e);
                        result = false;
                        error = e;
                    }

                    return (result, error);
                }, token);
            }

            public bool IsTypeVaultSafe(INamedTypeSymbol nts, Compilation comp) =>
                IsTypeVaultSafe(nts, comp, CancellationToken.None);

            public bool IsTypeVaultSafe(INamedTypeSymbol nts, Compilation comp,
                CancellationToken token)
            {
                if (nts == null) throw new ArgumentNullException(nameof(nts));
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                const string methodName = nameof(IsTypeVaultSafe);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName,
                    nts, comp, token))
                {
                    
                    var vsAttrib = FindVaultSafeAttribute(comp);
                    if (vsAttrib == null) throw new Exception();

                    if (nts.IsValueType)
                    {
                        var temp = nts.ExtractUnderlyingTypeIfNullableStruct(comp);
                        if (temp is INamedTypeSymbol ants)
                        {
                            nts = ants;
                        }
                    }

                    bool isVaultSafe;
                    if (IsTypeBlackListed(nts))
                    {
                        isVaultSafe = false;
                    }
                    else if (IsTypeWhiteListed(nts))
                    {
                        isVaultSafe = true;
                    }
                    else
                    {
                        var checkImmColCondVsRes =
                            CheckPossibleImmutableCollectionConditionalVaultSafety(nts, comp, token);
                        if (checkImmColCondVsRes.IsVaultSafe)
                        {
                            isVaultSafe = true;
                        }
                        else
                        {
                            if (nts.IsUnmanagedType()) return true;
                            if (nts.SpecialType == SpecialType.System_String) return true;
                            bool hasVsAttrib = DoesNamedTypeHaveAttribute(nts, vsAttrib);
                            bool onFaith = hasVsAttrib && ConstructedWithFirstParamTrue(nts, vsAttrib, comp);
                            if (hasVsAttrib)
                            {
                                if (onFaith)
                                {
                                    isVaultSafe = true;
                                }
                                else
                                {
                                    if (nts.IsReferenceType && !nts.IsSealed)
                                    {
                                        isVaultSafe = false;
                                    }
                                    else
                                    {
                                        ImmutableHashSet<INamedTypeSymbol> parents =
                                            ImmutableHashSet<INamedTypeSymbol>.Empty;
                                        isVaultSafe =
                                            AnalyzeChildrenVaultSafety(nts, vsAttrib, parents, comp,
                                                checkImmColCondVsRes.Lookup, token, false);
                                    }
                                }
                            }
                            else
                            {
                                isVaultSafe = false;
                            }
                        }
                    }

                    if (isVaultSafe)
                    {
                        WhiteListType(nts);
                    }
                    else
                    {
                        BlackListType(nts);
                    }

                    return isVaultSafe;
                }
            }
            public bool AnalyzeTypeParameterSymbolForVaultSafety(ITypeParameterSymbol tps,
                Compilation comp)
            {
                const string methodName = nameof(AnalyzeTypeParameterSymbolForVaultSafety);

                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, tps, comp))
                {
                    try
                    {
                        bool ret = false;
                        LogTypeParameterConstraints(tps);
                        if (tps.HasUnmanagedTypeConstraint)
                        {
                            ret = true;
                        }
                        else
                        {
                            INamedTypeSymbol declaringType = tps.DeclaringType;
                            var findMe = declaringType?.TypeParameters.FirstOrDefault(tp =>
                                true == tp?.Equals(tps, SymbolEqualityComparer.Default));
                            if (findMe != null)
                            {
                                var vaultSafeTpAttrib = FindVaultSafeTypeParamAttribute(comp);
                                if (vaultSafeTpAttrib != null)
                                {
                                    ret = DoesNamedTypeHaveAttribute(tps, vaultSafeTpAttrib);
                                }
                            }
                        }

                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        TraceLog.Log(e);
                        throw;
                    }
                }
            }
            private bool AnalyzeChildrenVaultSafety([NotNull] ITypeSymbol ts,
                [NotNull] INamedTypeSymbol attribSymb,
                [NotNull] ImmutableHashSet<INamedTypeSymbol> parents, [NotNull] Compilation comp, [NotNull] IImmutableGenericTypeLookup igtl,
                CancellationToken token,
                bool anyReferenceParents)
            {
                const string methodName = nameof(AnalyzeChildrenVaultSafety);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, ts, attribSymb,
                    parents, comp, igtl, token, anyReferenceParents))
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        bool isThisTypeAReferenceType = ts.IsReferenceType;
                        bool areAllChildrenVaultSafe = true;

                        switch (ts)
                        {
                            case INamedTypeSymbol nts:
                                parents.Add(nts);
                                foreach (var child in nts.GetFieldSymbolMembersIncludingBaseTypesExclObject().Where(fs => !fs.IsStatic))
                                {
                                    bool isThisChildReadOnly = child.IsReadOnly || child.IsConst;
                                    anyReferenceParents = isThisTypeAReferenceType || anyReferenceParents;
                                    bool isChildVaultSafe = AnalyzeChildForVaultSafety(child.Type, attribSymb, parents,
                                        comp, token,
                                        isThisChildReadOnly, anyReferenceParents, igtl);
                                    if (!isChildVaultSafe)
                                    {
                                        areAllChildrenVaultSafe = false;
                                        break;
                                    }
                                }

                                break;
                            case IDynamicTypeSymbol _:
                            case IArrayTypeSymbol _:
                                areAllChildrenVaultSafe = false;
                                break;
                        }


                        return areAllChildrenVaultSafe;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Log(ex);
                        throw;
                    }
                }
            }

            private bool AnalyzeChildForVaultSafety([NotNull] ITypeSymbol nts,
                [NotNull] INamedTypeSymbol attribSymb,
                [NotNull] ImmutableHashSet<INamedTypeSymbol> parents, [NotNull] Compilation comp,
                CancellationToken token, bool isReadOnlyField,
                bool anyReferenceParents, [NotNull] IImmutableGenericTypeLookup igtl)
            {
                const string methodName = nameof(AnalyzeChildForVaultSafety);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, nts, attribSymb,
                    parents, comp, igtl, token, isReadOnlyField, anyReferenceParents, igtl))
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        if (nts is IErrorTypeSymbol ets)
                        {
                            var resolution = ets.ResolveErrorTypeSymbol(comp) ?? nts;
                            nts = resolution;
                        }

                        if (isReadOnlyField && nts.IsUnmanagedType)
                        {
                            return true;
                        }

                        bool hasVsAttrib = DoesNamedTypeHaveAttribute(nts, attribSymb);
                        bool onFaith = hasVsAttrib && ConstructedWithFirstParamTrue(nts, attribSymb, comp);
                        bool mightBeVsSpecImm = igtl.FindMatch(nts).IsDesignated;
                        bool isVsSpecImm = mightBeVsSpecImm &&
                                           CheckPossibleImmutableCollectionConditionalVaultSafety(nts, comp,
                                               igtl, token);
                        bool notVsRefParentsNotReadOnly = anyReferenceParents &&
                            NotVaultSafeBcRefStatusAndNotReadOnly(nts, onFaith, true, isReadOnlyField,
                                isVsSpecImm);

                        if (notVsRefParentsNotReadOnly) return false;
                        if (IsTypeBlackListed(nts)) return false;
                        if (nts is IArrayTypeSymbol || nts is IDynamicTypeSymbol) return false;
                        if (anyReferenceParents)
                        {
                            bool anyWriteableChildren = DoWriteableFieldScan(nts, comp, isReadOnlyField);
                            if (anyWriteableChildren)
                            {
                                return false;
                            }
                        }

                        if ((nts.IsUnmanagedType() || IsTypeWhiteListed(nts) || onFaith)) return true;


                        bool ret;



                        switch (nts)
                        {
                            case ITypeParameterSymbol tps:
                                LogTypeParameterConstraints(tps);
                                ret = IsTpsVaultSafe(tps, comp);
                                break;
                            case INamedTypeSymbol named:
                                bool refTypeButNotSealed = named.IsReferenceType && !named.IsSealed;
                                bool primaFacieOk = (hasVsAttrib && !refTypeButNotSealed) || isVsSpecImm;
                                anyReferenceParents = anyReferenceParents || named.IsReferenceType;
                                ret = isVsSpecImm || (primaFacieOk && AnalyzeChildrenVaultSafety(named, attribSymb,
                                                          parents.Add(named), comp, igtl,
                                                          token, anyReferenceParents));
                                break;
                            default:
                                DebugLog.Log(
                                    $"The type of {nts.TypeKind} is neither a type parameter symbol nor a named type symbol.",
                                    true);
                                ret = false;
                                break;

                        }

                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Log(ex);
                        throw;

                    }
                }

                #region Local Helpers

                bool NotVaultSafeBcRefStatusAndNotReadOnly(ITypeSymbol t, bool faith, bool refParens,
                    bool readOnly, bool conditionallyVs) => ((refParens || (t.IsReferenceType && !faith)) && !readOnly && !conditionallyVs);

                bool IsTpsVaultSafe(ITypeParameterSymbol tps, Compilation c)
                {
                    bool tpsVs = tps.HasUnmanagedTypeConstraint;
                    if (!tpsVs)
                    {
                        var vsTpAttrib = FindVaultSafeTypeParamAttribute(c);
                        tpsVs = DoesNamedTypeHaveAttribute(tps, vsTpAttrib);
                    }

                    return tpsVs;
                }

                bool DoWriteableFieldScan(ITypeSymbol ts, Compilation c, bool readOnly)
                {
                    if (!readOnly)
                    {
                        return true;
                    }

                    if (!FurtherScanNeeded(ts, c))
                    {
                        return false;
                    }

                    return ts.GetMembers().OfType<IFieldSymbol>()
                               .Where(s => !s.IsStatic)
                               .Count(s => RecursiveScanForWritableFields(s, c)) > 0;
                }

                bool FurtherScanNeeded(ITypeSymbol ts, Compilation c)
                {
                    bool? r=null;
                    ts = ts.ExtractUnderlyingTypeIfNullableStruct(c);
                    if (ts.IsValueType && ts is INamedTypeSymbol nt && nt.SpecialType == SpecialType.System_Nullable_T)
                    {
                        var firstParam = nt.TypeParameters.FirstOrDefault();
                        var firstArg = nt.TypeArguments.FirstOrDefault();
                        if (firstArg != null)
                        {
                            Debug.Assert(firstParam != null);
                            if (firstParam.HasUnmanagedTypeConstraint)
                            {
                                r = false;
                            }
                            else
                            {
                                ts = firstArg; //if we are a nullable type, we consider the underlying type ... nullables 
                            }                   //have a non-readonly field but this is ok for our purposes.
                        }
                    }

                    if (r == null)
                    {
                        if (ts.TypeKind == TypeKind.Enum && ts.IsUnmanagedType)
                        {
                            r = false;
                        }
                        else
                        {
                            switch (ts.SpecialType)
                            {

                                case SpecialType.System_Enum:
                                case SpecialType.System_Void:
                                case SpecialType.System_Boolean:
                                case SpecialType.System_Char:
                                case SpecialType.System_SByte:
                                case SpecialType.System_Byte:
                                case SpecialType.System_Int16:
                                case SpecialType.System_UInt16:
                                case SpecialType.System_Int32:
                                case SpecialType.System_UInt32:
                                case SpecialType.System_Int64:
                                case SpecialType.System_UInt64:
                                case SpecialType.System_Decimal:
                                case SpecialType.System_Single:
                                case SpecialType.System_Double:
                                case SpecialType.System_String:
                                case SpecialType.System_IntPtr:
                                case SpecialType.System_UIntPtr:
                                case SpecialType.System_Array:
                                case SpecialType.System_DateTime:
                                    r = false;
                                    break;
                                default:
                                    r = !IsTypeExemptFromFieldScan(ts, c, igtl, token);
                                    break;
                            }
                        }
                    }
                    return r.Value;
                }
                
                bool RecursiveScanForWritableFields(IFieldSymbol fs, Compilation c)
                {
                    bool writable = false;
                    if (!fs.IsReadOnly)
                    {
                        writable = true;
                    }
                    else if (FurtherScanNeeded(fs.Type, c))
                    {
                        var type = fs.Type;
                        if (type is IErrorTypeSymbol e)
                        {
                            var resolved = e.ResolveErrorTypeSymbol(c);
                            if (resolved != null)
                                type = resolved;
                        }

                        foreach (var child in type.GetMembers().OfType<IFieldSymbol>().Where(s => !s.IsStatic))
                        {
                            token.ThrowIfCancellationRequested();
                            bool isChildWriteable = RecursiveScanForWritableFields(child, c);
                            if (isChildWriteable)
                            {
                                writable = true;
                                break;
                            }
                        }

                    }

                    return writable;

                }

                #endregion
            }

            [UsedImplicitly]
            private bool IsVaultSafeReferenceType([NotNull] INamedTypeSymbol nts, [NotNull] INamedTypeSymbol attribSymb,
                [NotNull] Compilation comp)
            {
                if (nts == null) throw new ArgumentNullException(nameof(nts));
                if (comp == null) throw new ArgumentNullException(nameof(comp));
                const string methodName = nameof(IsVaultSafeReferenceType);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, nts, attribSymb,
                    comp))
                {
                    try
                    {
                        if (nts.IsReferenceType)
                        {
                            bool hasAttribute = DoesNamedTypeHaveAttribute(nts, attribSymb);
                            bool onFaith = hasAttribute && ConstructedWithFirstParamTrue(nts, attribSymb, comp);
                            if (onFaith)
                            {
                                return true;
                            }

                            if (!hasAttribute)
                            {
                                return false;
                            }

                            return AreAllChildrenReadOnlyFieldsWithTheAttribute(nts, attribSymb);

                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Log(ex);
                        throw;
                    }
                }

                static bool AreAllChildrenReadOnlyFieldsWithTheAttribute(INamedTypeSymbol ns,
                    INamedTypeSymbol vsAttrib)
                {
                    const string innerMethodName = nameof(AreAllChildrenReadOnlyFieldsWithTheAttribute);
                    using (EntryExitLog.CreateEntryExitLog( EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), innerMethodName, ns, vsAttrib))
                    {
                        try
                        {
                            foreach (var child in ns.GetMembers().OfType<IFieldSymbol>())
                            {
                                if (!child.IsReadOnly)
                                {
                                    return false;
                                }
                                else if (child.Type is INamedTypeSymbol cnts)
                                {
                                    if (!DoesNamedTypeHaveAttribute(cnts, vsAttrib))
                                    {
                                        return false;
                                    }

                                    bool allOk = AreAllChildrenReadOnlyFieldsWithTheAttribute(cnts, vsAttrib);
                                    if (!allOk)
                                    {
                                        return false;
                                    }
                                }
                            }

                            return true;
                        }
                        catch (Exception ex)
                        {
                            TraceLog.Log(ex);
                            throw;
                        }
                    }
                }
            }

            [UsedImplicitly]
            private bool IsImmutableReferenceType([NotNull] INamedTypeSymbol nts, [NotNull] Compilation comp)
            {
                if (nts == null) throw new ArgumentNullException(nameof(nts));
                if (comp == null) throw new ArgumentNullException(nameof(comp));

                return nts.IsReferenceType && AreAllChildrenReadOnlyFields(nts);

                static bool AreAllChildrenReadOnlyFields(INamedTypeSymbol n)
                {
                    foreach (var child in n.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (!child.IsReadOnly)
                        {
                            return false;
                        }
                        else if (child.Type is INamedTypeSymbol cnts)
                        {
                            bool allOk = AreAllChildrenReadOnlyFields(cnts);
                            if (!allOk)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            static VaultSafeTypeAnalyzerV2() =>
                TheImmutableCollectionsConditionalWhiteList =
                    InitImmutableCollectionsConditionalWhiteList().ToImmutableArray();

            private VaultSafeTypeAnalyzerV2()
            {
            }

            private bool EvaluateVaultSafety([NotNull] INamedTypeSymbol nts, [NotNull] Compilation comp,
                CancellationToken token)
            {
                const string methodName = nameof(EvaluateVaultSafety);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, nts, comp,
                    token))
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        bool ret;
                        if (nts.IsUnmanagedType())
                        {
                            WhiteListType(nts);
                            ret = true;
                        }
                        else
                        {
                            var namedTypeSymbol = nts;
                            INamedTypeSymbol vaultSafeAttribSymbol = FindVaultSafeAttribute(comp);
                            if (vaultSafeAttribSymbol == null)
                            {
                                TraceLog.Log($"Vault safe attribute not found in compilation: [{comp.AssemblyName}].");
                                ret = false;
                            }
                            else
                            {
                                token.ThrowIfCancellationRequested();
                                bool hasAttribute = DoesNamedTypeHaveAttribute(namedTypeSymbol, vaultSafeAttribSymbol);
                                bool onFaith = hasAttribute &&
                                               ConstructedWithFirstParamTrue(namedTypeSymbol, vaultSafeAttribSymbol,
                                                   comp);
                                if (onFaith)
                                {
                                    WhiteListType(nts);
                                    ret = true;
                                }
                                else if (hasAttribute)
                                {
                                    ret = AnalyzeVaultSafety(namedTypeSymbol, ImmutableHashSet<ITypeSymbol>.Empty,
                                        vaultSafeAttribSymbol, comp, token);
                                    if (ret)
                                    {
                                        WhiteListType(namedTypeSymbol);
                                    }
                                    else
                                    {
                                        BlackListType(namedTypeSymbol);
                                    }
                                }
                                else
                                {
                                    ret = false;
                                    BlackListType(namedTypeSymbol);
                                }
                            }
                        }

                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Log(ex);
                        throw;
                    }
                }
            }

          

            private bool AnalyzeVaultSafety([NotNull] ITypeSymbol symbol,
                [NotNull] ImmutableHashSet<ITypeSymbol> parents, [NotNull] INamedTypeSymbol vaultSafeAttributeSymbol,
                [NotNull] Compilation compilation,
                CancellationToken token)
            {
                const string methodName = nameof(AnalyzeVaultSafety);
                using (EntryExitLog.CreateEntryExitLog(EnableEntryExitLogging, typeof(VaultSafeTypeAnalyzer), methodName, symbol, parents,
                    token, vaultSafeAttributeSymbol, compilation, token))
                {
                    if (IsTypeBlackListed(symbol)) return false;
                    if (IsTypeWhiteListed(symbol)) return true;
                    if (symbol.IsUnmanagedType()) return true;
                    if (symbol.SpecialType == SpecialType.System_String) return true;

                    token.ThrowIfCancellationRequested();

                    try
                    {
                        bool ret;

                        if (symbol.IsReferenceType && !symbol.IsSealed)
                        {
                            ret = false;
                        }
                        else
                        {
                            if (symbol.TypeKind != TypeKind.Class && symbol.TypeKind != TypeKind.Struct &&
                                symbol.TypeKind != TypeKind.Enum && symbol.TypeKind != TypeKind.TypeParameter)
                            {
                                ret = false;
                            }
                            else if (symbol.TypeKind == TypeKind.TypeParameter)
                            {
                                //ret = false;
                                ITypeParameterSymbol tps = (ITypeParameterSymbol) symbol;
                                ret = AnalyzeTypeParameterSymbolForVaultSafety(tps, compilation);
                            }
                            else
                            {
                                if (parents.Contains(symbol))
                                {
                                    ret = false;
                                }
                                else
                                {

                                    token.ThrowIfCancellationRequested();

                                    if (symbol is INamedTypeSymbol nts)
                                    {

                                        bool hasAttribute = DoesNamedTypeHaveAttribute(nts, vaultSafeAttributeSymbol);
                                        bool onFaith = hasAttribute &&
                                                       ConstructedWithFirstParamTrue(nts, vaultSafeAttributeSymbol,
                                                           compilation);
                                        if (onFaith)
                                        {
                                            ret = true;
                                        }
                                        else if (hasAttribute)
                                        {
                                            token.ThrowIfCancellationRequested();
                                            parents = parents.Add(symbol);

                                            bool allChildrenOk = true;
                                            foreach (var child in symbol.GetFieldSymbolMembersIncludingBaseTypesExclObject())
                                            {
                                                token.ThrowIfCancellationRequested();
                                                ITypeSymbol childSymbol = child.Type;
                                                if (childSymbol is IErrorTypeSymbol ets)
                                                {
                                                    var temp = ets.ResolveErrorTypeSymbol(compilation);
                                                    if (temp != null)
                                                    {
                                                        childSymbol = temp;
                                                    }
                                                }

                                                if ((!childSymbol.IsStatic &&
                                                     childSymbol.Equals(symbol, SymbolEqualityComparer.Default)) ||
                                                    IsTypeBlackListed(childSymbol) ||
                                                    !AnalyzeVaultSafety(childSymbol, parents, vaultSafeAttributeSymbol,
                                                        compilation, token))
                                                {
                                                    allChildrenOk = false;
                                                    BlackListType(childSymbol);
                                                    break;
                                                }

                                                if (!child.IsReadOnly)
                                                {
                                                    allChildrenOk = false;
                                                    break;
                                                }
                                            }

                                            ret = allChildrenOk;
                                        }
                                        else
                                        {
                                            ret = false;
                                        }
                                    }
                                    else
                                    {
                                        ret = false;
                                    }
                                }

                            }
                        }

                        if (!ret)
                        {
                            BlackListType(symbol);
                        }
                        else if (symbol.TypeKind != TypeKind.TypeParameter)
                        {
                            WhiteListType(symbol);
                        }

                        return ret;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TraceLog.Log(ex);
                        throw;
                    }
                }

            }

            [Conditional("DEBUG")]
            private void LogTypeParameterConstraints(ITypeParameterSymbol tps)
            {
                if (tps == null) return;
                StringBuilder sb =
                    new StringBuilder(
                        $"The type parameter {tps.Name} has {tps.ConstraintTypes.Length} constraint types.{Environment.NewLine}");
                foreach (var item in tps.ConstraintTypes)
                {
                    sb.AppendLine($"\t\tConstraint type: [{item.Name}]");
                }

                sb.AppendLine($"\t\tHas unmanaged constraint: [{tps.HasUnmanagedTypeConstraint}].");
                DebugLog.Log(sb.ToString());
            }



            private bool IsTypeWhiteListed([NotNull] ITypeSymbol ts)
            {
                ImmutableHashSet<string> white = WhiteList;
                return white.Contains(ts.MetadataName) || white.Contains(ts.FullName());
            }

            private bool IsTypeBlackListed(ITypeSymbol ts)
            {
                ImmutableHashSet<string> black = BlackList;
                return black.Contains(ts.MetadataName);
            }

            private void WhiteListType([NotNull] ITypeSymbol ts)
            {
                if (ts.TypeKind == TypeKind.TypeParameter) return;
                lock (_whiteListSyncObject)
                {
                    TraceLog.Log($"White listing symbol: [{ts.MetadataName}]");
                    //_whiteList = _whiteList.Add(ts.MetadataName);
                }
            }

            private void BlackListType([NotNull] ITypeSymbol ts)
            {
                if (ts.TypeKind == TypeKind.TypeParameter) return;
                lock (_blackListSyncObject)
                {
                    TraceLog.Log($"BLACK LISTING type symbol: [{ts.Name}]");
                    //_blackList = _blackList.Add(ts.MetadataName);
                }
            }

            private static bool ConstructedWithFirstParamTrue(ITypeSymbol querySymbol, INamedTypeSymbol vaultSafeAttrib,
                Compilation model)
            {
                if (querySymbol == null || vaultSafeAttrib == null) return false;
                var queryRes = FindFirstMatchingVaultSafeAttribData(querySymbol, vaultSafeAttrib);
                if (queryRes == default) return false;

                bool ret;

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
                                        ret = false;
                                        break;
                                    default:
                                        var semanticModel = model.GetSemanticModel(syntaxTree);
                                        var help = semanticModel.GetConstantValue(firstArgumentExpression);
                                        ret = help.HasValue && help.Value is bool b && b;
                                        break;
                                }
                            }
                            else //firstArgumentExpression == null
                            {
                                ret = false;
                            }
                        }
                        else //!hasArguments
                        {
                            ret = false;
                        }

                    }
                    else //syntaxTree == null
                    {
                        ret = false;
                    }

                }
                else //matchingData == null || matchingAttrbClass == null
                {
                    ret = false;
                }

                return ret;

                static (AttributeData AttribData, INamedTypeSymbol MatchingAttribDataClass)
                    FindFirstMatchingVaultSafeAttribData(
                        ITypeSymbol nts, INamedTypeSymbol canonical) =>
                    (from ad in nts.GetAttributes()
                        let isMatch = IsNtsAnAttributeOfTypeAttributeSymbol(ad.AttributeClass, canonical)
                        where isMatch
                        select (ad, ad.AttributeClass)).FirstOrDefault();
            }



            private static bool DoesNamedTypeHaveAttribute(ISymbol querySymbol, INamedTypeSymbol canonicalSymbolToFind)
            {
                if (querySymbol == null || canonicalSymbolToFind == null) return false;

                return querySymbol.GetAttributes().Any(attribData =>
                    IsNtsAnAttributeOfTypeAttributeSymbol(attribData.AttributeClass, canonicalSymbolToFind));
            }

            private static bool IsNtsAnAttributeOfTypeAttributeSymbol(INamedTypeSymbol nts,
                INamedTypeSymbol attributeSymbol)
            {
                if (ReferenceEquals(nts, attributeSymbol)) return true;
                if (ReferenceEquals(nts, null) || ReferenceEquals(attributeSymbol, null)) return false;
                if (nts.Equals(attributeSymbol, SymbolEqualityComparer.Default)) return true;

                if (nts is IErrorTypeSymbol ets && ets.CandidateReason == CandidateReason.NotAnAttributeType)
                {
                    foreach (var item in ets.CandidateSymbols.OfType<INamedTypeSymbol>())
                    {
                        if (item.Equals(attributeSymbol, SymbolEqualityComparer.Default)) return true;
                    }
                }

                return false;
            }

            private (bool IsVaultSafe, IImmutableGenericTypeLookup Lookup) CheckPossibleImmutableCollectionConditionalVaultSafety([NotNull] ITypeSymbol ts,
                [NotNull] Compilation comp, CancellationToken token)
            {
                IImmutableGenericTypeLookup lookup = CreateGenericTypeLookup(comp);
                return (CheckPossibleImmutableCollectionConditionalVaultSafety(ts, comp, lookup, token), lookup);
            }

            private bool CheckPossibleImmutableCollectionConditionalVaultSafety([NotNull] ITypeSymbol ts,
                [NotNull] Compilation comp, [NotNull] IImmutableGenericTypeLookup lookup, CancellationToken token)
            {
                bool anyOpenTpsFound = false;
                var chkImmutColRes = lookup.FindMatch(ts);
                bool isVaultSafe = IsTypeWhiteListed(ts);
                if (!isVaultSafe && chkImmutColRes.IsDesignated)
                {
                    ImmutableArray<ITypeSymbol> typeArgSource = GetTypeArguments(ts, chkImmutColRes.MatchingOpenTypeSymbol);
                    foreach (ITypeSymbol arg in typeArgSource)
                    {
                        token.ThrowIfCancellationRequested();
                        ITypeSymbol toBeAnalyzed = arg;
                        if (toBeAnalyzed is IErrorTypeSymbol ets)
                        {
                            var resolved = ets.ResolveErrorTypeSymbol(comp);
                            toBeAnalyzed = resolved ?? toBeAnalyzed;
                        }
                        switch (toBeAnalyzed)
                        {
                            case ITypeParameterSymbol tps:
                                isVaultSafe = AnalyzeTypeParameterSymbolForVaultSafety(tps, comp);
                                anyOpenTpsFound = true;
                                break;
                            case INamedTypeSymbol nts:
                                isVaultSafe = nts.IsUnmanagedType || IsTypeWhiteListed(nts) ||  EvaluateVaultSafety(nts, comp, token);
                                break;
                            default:
                                isVaultSafe = false;
                                break;
                        }

                        if (!isVaultSafe)
                        {
                            break;
                        }
                    }
                }
                if (isVaultSafe && !anyOpenTpsFound)
                {
                    WhiteListType(ts);
                    if (chkImmutColRes.IsDesignated) ExemptTypeFromFieldScan(ts);
                }
                return isVaultSafe;

                ImmutableArray<ITypeSymbol> GetTypeArguments(ITypeSymbol s, INamedTypeSymbol foundSymbol)
                {
                    switch (s)
                    {
                        case IErrorTypeSymbol ets:
                            return ets.TypeArguments;
                        case INamedTypeSymbol n:
                            return n.TypeArguments;
                        default:
                            return foundSymbol.TypeArguments;
                    }
                }
            }

            private static IImmutableGenericTypeLookup CreateGenericTypeLookup([NotNull] Compilation comp) =>
                ImmutableGenericTypeLookupFactorySource.CreateFactoryInstance()(
                    TheImmutableCollectionsConditionalWhiteList, comp);

            private static INamedTypeSymbol FindVaultSafeAttribute(Compilation compilation) =>
                compilation?.GetTypeByMetadataName(typeof(VaultSafeAttribute).FullName);


            internal ImmutableHashSet<string> WhiteList
            {
                get
                {
                    lock (_whiteListSyncObject) return _whiteList ??= InitWhiteListAndFieldScanExemptionList();
                }
            }

            private ImmutableHashSet<string> BlackList
            {
                get
                {
                    lock (_blackListSyncObject) return _blackList;
                }
            }

            internal ImmutableHashSet<string> FieldScanExempt
            {
                get
                {
                    lock (_fieldScanSyncObject)
                        return _noRoFieldScanNeededWhiteList ??= InitWhiteListAndFieldScanExemptionList();
                }
                private set
                {
                    lock (_fieldScanSyncObject)
                    {
                        _noRoFieldScanNeededWhiteList = value ?? throw new ArgumentNullException(nameof(value));
                    }
                }
            }

            private void ExemptTypeFromFieldScan(ITypeSymbol ts)
            {
                ImmutableHashSet<string> fieldScanExemptList = FieldScanExempt;
                var temp = fieldScanExemptList.Add(ts.MetadataName);
                FieldScanExempt = temp;
            }

            private bool IsTypeExemptFromFieldScan(ITypeSymbol ts, Compilation c, [NotNull] IImmutableGenericTypeLookup igtl, CancellationToken token)
            {
                ImmutableHashSet<string> set = FieldScanExempt;
                return set.Contains(ts.FullName()) || CheckPossibleImmutableCollectionConditionalVaultSafety(ts, c, igtl, token);
            }

            private ImmutableHashSet<string> _noRoFieldScanNeededWhiteList;
                //ImmutableHashSet<string>.Empty.Union(new[]
                //{
                //    typeof(DateTime).FullName, typeof(Guid).FullName, typeof(TimeSpan).FullName, typeof(string).FullName
                //});

            private ImmutableHashSet<string> _blackList =
                ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

            private ImmutableHashSet<string> _whiteList;


            private static readonly ImmutableArray<string> TheImmutableCollectionsConditionalWhiteList;

            private static ImmutableHashSet<string> InitImmutableCollectionsConditionalWhiteList()
            {
                bool fileIsMissing;
                string contents=string.Empty;
                ImmutableHashSet<string> ret = ImmutableHashSet<string>.Empty;
                try
                {
                    lock (ConditionalWhiteListFileSyncObject)
                    {
                        var temp = ExtractContents(DataFileSpecifier.ConditionalWhiteList);
                        contents = temp.Contents ?? string.Empty;
                        ret = ret.Union(new HashSet<string>(ParseContents(contents)));
                        fileIsMissing = temp.FileMissing;
                    }
                }
                catch (Exception e)
                {
                    TraceLog.Log(e);
                    TraceLog.Log(
                        $"The contents of the white list file at path {ConditionallyImmutableGenericTypePath} were not successfully delineated.  " +
                        "The fallback white-list will be used." + Environment.NewLine +
                        $"File contents: {Environment.NewLine}[{contents}]{Environment.NewLine}");
                    fileIsMissing = false;
                }

                ret = ret.Union(
                    new[]
                    {
                        typeof(KeyValuePair<,>).FullName,
                        typeof(ImmutableArray<>).FullName,
                        typeof(ImmutableArray<>.Enumerator).FullName,
                        typeof(ImmutableList<>).FullName,
                        typeof(ImmutableList<>.Enumerator).FullName,
                        typeof(ImmutableDictionary<,>).FullName,
                        typeof(ImmutableDictionary<,>.Enumerator).FullName,
                        typeof(ImmutableSortedDictionary<,>).FullName,
                        typeof(ImmutableSortedDictionary<,>.Enumerator).FullName,
                        typeof(ImmutableHashSet<>).FullName,
                        typeof(ImmutableHashSet<>.Enumerator).FullName,
                        typeof(ImmutableSortedSet<>).FullName,
                        typeof(ImmutableSortedSet<>.Enumerator).FullName,
                        typeof(ImmutableStack<>).FullName,
                        typeof(ImmutableStack<>.Enumerator).FullName,
                        typeof(ImmutableQueue<>).FullName,
                        typeof(ImmutableQueue<>.Enumerator).FullName,
                    });
                if (fileIsMissing)
                {
                    var task = SaveFileAsync(ret, DataFileSpecifier.ConditionalWhiteList);
                    task.ContinueWith(continueObj =>
                    {
                        if (continueObj.Status != TaskStatus.RanToCompletion)
                        {
                            TraceLog.Log($"Error when trying to save white list to path: [{ConditionallyImmutableGenericTypePath}].  Error: [{continueObj.Exception?.ToString() ?? "NO EXCEPTION AVAILABLE"}]");
                        }
                        else
                        {
                            if (!continueObj.Result.Success)
                            {
                                TraceLog.Log($"Error when trying to save white list to path: [{ConditionallyImmutableGenericTypePath}].  Error: [{continueObj.Result.Error?.ToString() ?? "NO EXCEPTION AVAILABLE"}]");
                            }
                        }

                    });
                }

                return ret;
              


                static IEnumerable<string> ParseContents(string text)
                {
                    var parser = TypeNameParserUtilityFactorySource.ParserFactory();
                    return parser.ParseContents(text);
                }
            }

            private static ImmutableHashSet<string> InitWhiteListAndFieldScanExemptionList()
            {
                bool fileIsMissing;
                string contents=string.Empty;
                ImmutableHashSet<string> ret = ImmutableHashSet<string>.Empty;
                try
                {
                    
                        var temp = ExtractContents(DataFileSpecifier.Whitelist);
                        contents = temp.Contents ?? string.Empty;
                        ret = ret.Union(new HashSet<string>(ParseContents(contents)));
                        fileIsMissing = temp.FileMissing;
                    
                }
                catch (Exception e)
                {
                    TraceLog.Log(e);
                    TraceLog.Log(
                        $"The contents of the white list file at path {VsAndFieldScanWhiteList} were not successfully delineated.  " +
                        "The fallback white-list will be used." + Environment.NewLine +
                        $"File contents: {Environment.NewLine}[{contents}]{Environment.NewLine}");
                    fileIsMissing = false;
                }
                ret = ret.Union(new[]
                {
                    typeof(DateTime).FullName, typeof(Guid).FullName, typeof(TimeSpan).FullName,
                    typeof(string).FullName, typeof(string).Name
                });

                if (fileIsMissing)
                {
                    var task = SaveFileAsync(ret, DataFileSpecifier.Whitelist);
                    task.ContinueWith(continueObj =>
                    {
                        if (continueObj.Status != TaskStatus.RanToCompletion)
                        {
                            TraceLog.Log($"Error when trying to save white list to path: [{VsAndFieldScanWhiteList}].  Error: [{continueObj.Exception?.ToString() ?? "NO EXCEPTION AVAILABLE"}]");
                        }
                        else
                        {
                            if (!continueObj.Result.Success )
                            {
                                TraceLog.Log($"Error when trying to save white list to path: [{VsAndFieldScanWhiteList}].  Error: [{continueObj.Result.Error?.ToString() ?? "NO EXCEPTION AVAILABLE"}]");
                            }
                        }

                    });
                }

                return ret;

              
                

                static IEnumerable<string> ParseContents(string text)
                {
                    var parser = TypeNameParserUtilityFactorySource.ParserFactory();
                    return parser.ParseContents(text);
                }


            }

            static Task<(bool Success, Exception Error)> SaveFileAsync(ImmutableHashSet<string> set, DataFileSpecifier specifier) => Task.Run(delegate
            {
                bool success;
                Exception fault;
                Debug.Assert(specifier == DataFileSpecifier.Whitelist || specifier == DataFileSpecifier.ConditionalWhiteList);

                FileInfo fi;
                object syncObject;
                if (specifier == DataFileSpecifier.Whitelist)
                {
                    fi = TheWhiteListFileInfo.Value;
                    syncObject = WhiteListFileSyncObject;
                }
                else
                {
                    fi = TheConditionalWhiteListFileInfo.Value;
                    syncObject = ConditionalWhiteListFileSyncObject;
                }

                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in set.Where(itm => !string.IsNullOrWhiteSpace(itm)))
                    {
                        sb.Append(item.Trim());
                        sb.Append(';');
                    }
                    string writeMe = sb.ToString();
                    lock (syncObject)
                    {
                       
                        if (!fi.Exists)
                        {
                            using (var sw = fi.CreateText())
                            {
                                sw.Write(writeMe);
                            }
                        }
                        success = true;
                    }

                    fault = null;
                }
                catch (Exception ex)
                {
                    TraceLog.Log(ex);
                    fault = ex;
                    success = false;
                }

                return (success, fault);
            });

            private static (bool FileMissing, string Contents) ExtractContents(DataFileSpecifier specifier)
            {
                object syncObject;
                FileInfo fi;
                Debug.Assert(specifier == DataFileSpecifier.ConditionalWhiteList || specifier == DataFileSpecifier.Whitelist);
                if (specifier == DataFileSpecifier.Whitelist)
                {
                    fi = TheWhiteListFileInfo.Value;
                    syncObject = WhiteListFileSyncObject;
                }
                else
                {
                    fi = TheConditionalWhiteListFileInfo.Value;
                    syncObject = ConditionalWhiteListFileSyncObject;
                }

                bool missing;
                string text;

                lock (syncObject)
                {
                    if (!fi.Exists)
                    {
                        missing = true;
                        text = string.Empty;
                    }
                    else
                    {
                        using (var sr = fi.OpenText())
                        {
                            text = sr.ReadToEnd();
                        }

                        missing = false;
                    }
                }

                return (missing, text);
            }


            private static FileInfo GetFileInfoForConditionalWhiteList()
            {

                var di = TheDataFileDir.Value;
                string path = di.FullName.EndsWith("\\") ? di.FullName + VsAndFieldScanWhiteList : di.FullName + "\\" + ConditionallyImmutableGenericTypePath;
                return new FileInfo(path);
            }

            private static DirectoryInfo GetDataFolderDirectoryInfo()
            {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                folder = folder.EndsWith("\\")
                    ? folder + AnalyzerDataFolderName
                    : folder + "\\" + AnalyzerDataFolderName;
                var temp = new DirectoryInfo(folder);
                lock (TheDataDirectoryCreationSyncObject)
                {
                    if (!temp.Exists)
                    {
                        temp.Create();
                        temp.Refresh();
                        Debug.Assert(temp.Exists);
                    }
                }
                return temp;
            }

            private static FileInfo GetFileInfoForVsAndFieldScanWhiteList()
            {
                var di = TheDataFileDir.Value;
                string path = di.FullName.EndsWith("\\") ? di.FullName + VsAndFieldScanWhiteList : di.FullName + "\\" + VsAndFieldScanWhiteList;
                return new FileInfo(path);
            }

            private static readonly LocklessWriteOnce<FileInfo> TheConditionalWhiteListFileInfo = new LocklessWriteOnce<FileInfo>(GetFileInfoForConditionalWhiteList);
            private const bool EnableEntryExitLogging = false;
            private static readonly object TheDataDirectoryCreationSyncObject = new object();
            private const string AnalyzerDataFolderName = "VaultAnalysisData";
            private static readonly LocklessWriteOnce<FileInfo> TheWhiteListFileInfo = new LocklessWriteOnce<FileInfo>(GetFileInfoForVsAndFieldScanWhiteList);
            private static readonly LocklessWriteOnce<DirectoryInfo> TheDataFileDir = new LocklessWriteOnce<DirectoryInfo>(GetDataFolderDirectoryInfo);
            private const string VsAndFieldScanWhiteList = "vaultsafewhitelist.txt";
            private const string ConditionallyImmutableGenericTypePath = "condit_generic_whitelist.txt";
            private static readonly object WhiteListFileSyncObject = new object();
            private static readonly object ConditionalWhiteListFileSyncObject = new object();
            private readonly object _blackListSyncObject = new object();
            private readonly object _whiteListSyncObject = new object();
            private readonly object _fieldScanSyncObject = new object();
            private readonly LocklessWriteOnce<ImmutableHashSet<string>> _conditionalGenericWhiteList =
                new LocklessWriteOnce<ImmutableHashSet<string>>(() =>
                    TheImmutableCollectionsConditionalWhiteList.ToImmutableHashSet());
        }
        #endregion

        private static INamedTypeSymbol FindVaultSafeTypeParamAttribute(Compilation compilation) =>
            compilation?.GetTypeByMetadataName(typeof(VaultSafeTypeParamAttribute).FullName);
        private static VaultSafeAnalyzerFactory FactoryInstance => TheDefaultFactory;
        
        private static readonly LocklessWriteOnce<VaultSafeAnalyzerFactory> TheDefaultFactory = new LocklessWriteOnce<VaultSafeAnalyzerFactory>(() => CreateDefaultAnalyzer);

        private enum DataFileSpecifier
        {
            Whitelist,
            ConditionalWhiteList
        }
    }

    internal static class TypeSymbolExtensions
    {
        public static string FullMetaDataName([NotNull] this ITypeSymbol ts) => FullMetaDataName(ts, false);

        public static string FullMetaDataName([NotNull] this ITypeSymbol ts, bool recursive)
        {
            const bool alwaysEntryExit = false;

            using var logger = EntryExitLog.CreateEntryExitLog(alwaysEntryExit, typeof(TypeSymbolExtensions), nameof(FullMetaDataName),
                ts, recursive);
            string ret;
            if (ts == null) throw new ArgumentNullException(nameof(ts));
            if (!recursive)
            {
                string ns = ts.ContainingNamespace.Name;
                string name = ts.MetadataName;
                if (!string.IsNullOrEmpty(ns)) ns += ".";
                ret = ns + name;
            }
            else
            {
                StringBuilder sb = new StringBuilder(ts.MetadataName);
                ret = ts.ContainingNamespace != null
                    ? AddContainingNamespaceNameIf(ts.ContainingNamespace, sb)
                    : sb.ToString();
            }

            return ret;

            string AddContainingNamespaceNameIf(INamespaceSymbol nss, StringBuilder str)
            {
                if (nss == null) throw new ArgumentNullException(nameof(nss));
                if (str == null) throw new ArgumentNullException(nameof(str));

                using var innerLogger = EntryExitLog.CreateEntryExitLog(alwaysEntryExit, typeof(TypeSymbolExtensions),
                    nameof(AddContainingNamespaceNameIf), nss, str);
                while (true)
                {
                    //const string stringBuilderNullStatus = "NOT NULL";
                    //DebugLog.Log(
                    //    $"Examining namespace symbol: {nss?.ToString() ?? "NULL"}, strBuilder -- {stringBuilderNullStatus}.");
                    //try
                    //{
                        if (string.IsNullOrWhiteSpace(nss?.Name))
                        {
                            return str.ToString();
                        }

                        str.Insert(0, $"{nss.Name}.");
                        nss = nss.ContainingNamespace;
                    //}
                    //catch (NullReferenceException ex)
                    //{
                    //    string nssNullStatus = nss == null ? "NULL" : $"NOT NULL [value: {nss}]";
                    //    string nssNameNullStatus = nss?.Name == null ? "NULL" : $"NOT NULL [value: {nss.Name}]";
                    //    string nssContainingNamespaceNullStatus =
                    //        nss?.ContainingNamespace == null ? "NULL" : $"NOT NULL [value: {nss.ContainingNamespace}]"; 
                    //    DebugLog.Log(ex);
                    //    string logMessage =
                    //        $"Null reference exception thrown.  StringBuilder null status: {stringBuilderNullStatus}; " +
                    //        $"nss null status: {nssNullStatus}; nss name null status: {nssNameNullStatus}; " +
                    //        $"nss Containing Namespace null status: [{nssContainingNamespaceNullStatus}]";
                    //    DebugLog.Log(logMessage);
                    //    throw;
                    //}
                }
            }
        }

        public static string FullName([NotNull] this ITypeSymbol ts, bool recursive)
        {
            string ret;
            if (ts == null) throw new ArgumentNullException(nameof(ts));
            if (!recursive)
            {
                string ns = ts.ContainingNamespace?.Name ?? string.Empty;
                string name = ts.Name;
                if (!string.IsNullOrEmpty(ns)) ns += ".";
                ret = ns + name;
            }
            else
            {
                StringBuilder sb = new StringBuilder(ts.Name);
                ret = AddContainingNamespaceNameIf(ts.ContainingNamespace, sb);
            }

            return ret;

            string AddContainingNamespaceNameIf(INamespaceSymbol nss, StringBuilder str)
            {
                while (true)
                {
                    if (string.IsNullOrWhiteSpace(nss.Name))
                    {
                        return str.ToString();
                    }

                    str.Insert(0, $"{nss.Name}.");
                    nss = nss.ContainingNamespace;
                }
            }
        }

        public static string FullName([NotNull] this ITypeSymbol ts) => ts.FullName(false);
   

        public static bool IsUnmanagedType([NotNull] this ITypeSymbol ts)
        {
            if (ts == null) throw new ArgumentNullException(nameof(ts));
            return ts.IsUnmanagedType;
            //New tooling has IsUnmanagedType property.  The below is how we did it manually before
            //we got that little gem.

            //bool ret;
            //if (ts is ITypeParameterSymbol tps)
            //{
            //    ret = tps.HasUnmanagedTypeConstraint;
            //}
            //else if (ts.IsReferenceType || !ts.IsValueType || ts.TypeKind == TypeKind.Pointer )
            //{
            //    ret = false;
            //}
            //else if (ts.IsValueType )
            //{
            //    switch (ts.SpecialType)
            //    {
            //        case SpecialType.System_Enum:
            //        case SpecialType.System_Boolean:
            //        case SpecialType.System_Char:
            //        case SpecialType.System_SByte:
            //        case SpecialType.System_Byte:
            //        case SpecialType.System_Int16:
            //        case SpecialType.System_UInt16:
            //        case SpecialType.System_Int32:
            //        case SpecialType.System_UInt32:
            //        case SpecialType.System_Int64:
            //        case SpecialType.System_UInt64:
            //        case SpecialType.System_Decimal:
            //        case SpecialType.System_Single:
            //        case SpecialType.System_Double:
            //        case SpecialType.System_DateTime:
            //            ret = true;
            //            break;
            //        default:
            //            ret = AreChildFieldsAlsoComplaint(ts);
            //            break;
            //    }
            //}
            //else
            //{
            //    ret = false;
            //}
            //return ret;
            //static bool AreChildFieldsAlsoComplaint(ITypeSymbol tsParent)
            //{
                
            //    ITypeSymbol[] nonStaticChildFields = tsParent.GetMembers().OfType<IFieldSymbol>()
            //        .Where(fs => !fs.IsStatic && fs.Type != null).Select(fs => fs.Type).ToArray();
            //    return nonStaticChildFields.Length == 0 || nonStaticChildFields.All(kid => kid.IsUnmanagedType());
            //}

           
        }

        [CanBeNull]
        public static ITypeSymbol ResolveErrorTypeSymbol([NotNull] this IErrorTypeSymbol ets, [NotNull] Compilation comp, [CanBeNull] INamedTypeSymbol hint = null)
        {
            if (ets == null) throw new ArgumentNullException(nameof(ets));
            if (comp == null) throw new ArgumentNullException(nameof(comp));
            if (hint != null)
            {
                var match = ets.CandidateSymbols.OfType<INamedTypeSymbol>().FirstOrDefault(itm => true == itm?.Equals(hint, SymbolEqualityComparer.Default));
                if (match != null)
                {
                    return match;
                }
            }

            if (ets.CandidateSymbols.Length == 1 && ets.CandidateSymbols.First() is ITypeSymbol t && t.Kind != SymbolKind.ErrorType)
            {
                return t;
            }
            else
            {
                var help = comp.GetTypeByMetadataName(ets.MetadataName);
                help ??= comp.GetTypeByMetadataName(ets.Name);

                help ??= comp.GetTypeByMetadataName(GetFullName(ets));
                if (help != null && help is ITypeSymbol ts)
                {
                    return ts;
                }

                string GetFullName(IErrorTypeSymbol e)
                {
                    if (!string.IsNullOrWhiteSpace(e.ContainingNamespace.Name))
                    {
                        return $"{e.ContainingNamespace?.Name ?? string.Empty}.{e.Name ?? string.Empty}";
                    }
                    else
                    {
                        return e.Name ?? string.Empty;
                    }
                }
            }
            return null;
        }

        public static ITypeSymbol ExtractUnderlyingTypeIfNullableStruct([NotNull] this ITypeSymbol ts,
            [NotNull] Compilation c)
        {
            if (ts == null) throw new ArgumentNullException(nameof(ts));
            if (c == null) throw new ArgumentNullException(nameof(c));
            ITypeSymbol nullableT = c.GetTypeByMetadataName(typeof(Nullable<>).FullName);
            if (nullableT != null && ts.IsValueType && ts is INamedTypeSymbol nt && nt.IsValueType)
            {
                if (true == nt.ConstructedFrom?.Equals(nullableT, SymbolEqualityComparer.Default))
                {
                    ts = nt.TypeArguments.FirstOrDefault() ?? ts;
                }
            }

            return ts;
        }
    }
}
