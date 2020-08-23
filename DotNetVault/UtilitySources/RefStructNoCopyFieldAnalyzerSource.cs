using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.ExtensionMethods;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using RefStructCopyFieldAnalyzer = DotNetVault.UtilitySources.RefStructCopyFieldAnalyzerSource.RefStructFieldAnalyzerImpl;
namespace DotNetVault.UtilitySources
{
    

    internal static class RefStructCopyFieldAnalyzerSource
    {
        public static RefStructCopyFieldAnalyzer CreateCopyAnalyzer() => new RefStructFieldAnalyzerImpl();

        internal readonly struct RefStructFieldAnalyzerImpl
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="searchMeForFields"></param>
            /// <param name="fieldsOfThisType"></param>
            /// <param name="token"></param>
            /// <returns>(IsRefStruct -- true iff <paramref name="searchMeForFields"/> is a ref struct,
            /// HasFieldMatchingType -- true iff <paramref name="searchMeForFields"/> has one or more non-static member ref-struct fields of type <paramref name="fieldsOfThisType"/>.
            /// IdentifiedField -- HasFieldMatchingType is true, the field symbol of that field, otherwise null
            /// NoCopyFieldType -- the type of IdentifiedField if identified field not null (should match <paramref name="fieldsOfThisType"/>), otherwise, null
            /// </returns>
            /// <exception cref="ArgumentNullException">One or more parameters was null</exception>
            /// <exception cref="OperationCanceledException">The operation was cancelled</exception>
            public (bool IsRefStruct, bool HasFieldMatchingType, IFieldSymbol IdentifiedField, INamedTypeSymbol FieldType) AnalyzeType(
                [NotNull] INamedTypeSymbol searchMeForFields, [NotNull] INamedTypeSymbol fieldsOfThisType, CancellationToken token)
            {
                bool isRefStruct, hasMatchingFieldOfType;
                IFieldSymbol identifiedField;
                INamedTypeSymbol fieldType;

                if (searchMeForFields == null) throw new ArgumentNullException(nameof(searchMeForFields));
                if (fieldsOfThisType == null) throw new ArgumentNullException(nameof(fieldsOfThisType));
                Debug.Assert(fieldsOfThisType.IsRefLikeType, "If the field isn't a ref-like type, why bother analyzing?");
                var namedTypeSymbol = searchMeForFields;
                
                isRefStruct = namedTypeSymbol.IsRefLikeType;
                if (isRefStruct)
                {
                    IEnumerable<(IFieldSymbol Field, ITypeSymbol FieldType)> fieldSymbolsToCheck =
                        from item in namedTypeSymbol.GetFieldSymbolMembersIncludingBaseTypesExclObject()
                        where ThrowIfCanc(token) && item?.Type != null && item.Type.IsRefLikeType
                        select (item, item.Type);

                    (IFieldSymbol Field, INamedTypeSymbol FieldType) matchingFields =
                        (from itm in fieldSymbolsToCheck
                            let namedFieldType = itm.FieldType as INamedTypeSymbol
                            where namedFieldType != null && ThrowIfCanc(token)
                                                         && namedFieldType.IsRefLikeType &&
                                                         SymbolEqualityComparer.Default.Equals(namedFieldType,
                                                             fieldsOfThisType)
                            select (itm.Field, namedFieldType)).FirstOrDefault();
                    hasMatchingFieldOfType = matchingFields.Field != null;
                    identifiedField = matchingFields.Field;
                    fieldType = matchingFields.FieldType;

                }
                else
                {
                    hasMatchingFieldOfType = false;
                    identifiedField = null;
                    fieldType = null;
                }
                Debug.Assert((fieldType == null) == (identifiedField == null), "Should be both or nothing.");
                Debug.Assert((fieldType == null && identifiedField == null) ||
                             SymbolEqualityComparer.Default.Equals(fieldType, fieldsOfThisType));
                Debug.Assert( (isRefStruct && hasMatchingFieldOfType) || (fieldType == null && identifiedField == null));
                Debug.Assert(hasMatchingFieldOfType == (fieldType != null && identifiedField != null));
                return (isRefStruct, hasMatchingFieldOfType, identifiedField, fieldType);
            }

            static bool ThrowIfCanc(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return true;
            }
        }
    }
}
