using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DotNetVault.Interfaces;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using IUsingStatementSyntaxAnalyzer = DotNetVault.Interfaces.IUsingStatementSyntaxAnalyzer<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>;

namespace DotNetVault.UtilitySources
{
    internal static class UsingStatementAnalyzerUtilitySource
    {
        public static IUsingStatementSyntaxAnalyzer CreateUsingStatementSyntaxAnalyzer() =>
            UsingStatementAnalyzerFactory();

        public static bool TrySupplyAlternateFactory([NotNull] Func<IUsingStatementSyntaxAnalyzer> alternate)
        {
            if (alternate == null) throw new ArgumentNullException(nameof(alternate));
            var old = Interlocked.CompareExchange(ref _usingStatementAnalyzerFactory, alternate, null);
            Debug.Assert(old != null || _usingStatementAnalyzerFactory == alternate);
            return old != null;
        }

        internal static UsingStatementAnalyzer CreateStatementAnalyzer() => new UsingStatementAnalyzer();

        
        internal readonly struct UsingStatementAnalyzer : IUsingStatementSyntaxAnalyzer<InvocationExpressionSyntax>
        {
            public bool IsPartOfUsingConstruct(InvocationExpressionSyntax syntax)
            {
                if (syntax == null) throw new ArgumentNullException(nameof(syntax));

                ParentNodeType pnt = ParentNodeType.FindTerminalParentNode(syntax);
                return pnt.Ok ?? false;
            }

            public (bool ok, BlockSyntax ContainingBlockSyntax, UsingStatementSyntax UsingStatementSyntax) FindBlockOrUsingStatement(InvocationExpressionSyntax syntax)
            {
                BlockSyntax usingDeclarationBlock;
                UsingStatementSyntax usingStateSyntax;
                bool ok;
                ParentNodeType pnt = ParentNodeType.FindTerminalParentNode(syntax);
                if (true == pnt.Ok)
                {
                    switch (pnt.TerminalNode?.Kind())
                    {
                        default:
                        case null:
                            ok = false;
                            usingDeclarationBlock = null;
                            usingStateSyntax = null;
                            break;
                        case SyntaxKind.LocalDeclarationStatement:
                            (ok, usingDeclarationBlock) =
                                FindBlockSyntaxAncestor((LocalDeclarationStatementSyntax) pnt.TerminalNode);
                            usingStateSyntax = null;
                            break;
                        case SyntaxKind.UsingStatement:
                            usingStateSyntax = (UsingStatementSyntax) pnt.TerminalNode;
                            usingDeclarationBlock = null;
                            ok = true;
                            //(ret, pNode) = HasVariableDeclarationChildNode((UsingStatementSyntax)pnt.TerminalNode);
                            //Debug.Assert(!ret || pNode != null);
                            break;
                    }
                }
                else
                {
                    ok = false;
                    usingDeclarationBlock = null;
                    usingStateSyntax = null;
                }

                Debug.Assert(!ok || ((usingStateSyntax == null) != (usingDeclarationBlock == null)));
                Debug.Assert(ok || (usingStateSyntax == null && usingDeclarationBlock == null));
                return (ok, usingDeclarationBlock, usingStateSyntax);

                (bool FoundBlockSyntax, BlockSyntax Bs) FindBlockSyntaxAncestor(LocalDeclarationStatementSyntax ldss)
                {
                    BlockSyntax bs = null;
                    SyntaxNode current = ldss;
                    while (bs == null && current != null)
                    {
                        if (current is BlockSyntax blk)
                        {
                            bs = blk;
                        }
                        else
                        {
                            current = current.Parent;
                        }
                    }

                    return (bs != null, bs);
                }
            }

            public (bool PartOfInlineDeclUsing, VariableDeclarationSyntax ParentNode) IsPartOfInlineDeclUsingConstruct(InvocationExpressionSyntax syntax)
            {
                if (syntax == null) throw new ArgumentNullException(nameof(syntax));
                ParentNodeType pnt = ParentNodeType.FindTerminalParentNode(syntax);
                VariableDeclarationSyntax pNode;
                bool ret;
                if (true == pnt.Ok)
                {
                    switch (pnt.TerminalNode?.Kind())
                    {
                        default:
                        case null:
                            ret = false;
                            pNode = null;
                            break;
                        case SyntaxKind.LocalDeclarationStatement:
                            ret = true;
                            pNode = pnt.TerminalNode.ChildNodes().OfType<VariableDeclarationSyntax>().FirstOrDefault();
                            Debug.Assert(pNode != null);
                            break;
                        case SyntaxKind.UsingStatement:
                            (ret, pNode) = HasVariableDeclarationChildNode((UsingStatementSyntax) pnt.TerminalNode);
                            Debug.Assert( !ret || pNode != null);
                            break;
                    }
                }
                else
                {
                    ret = false;
                    pNode = null;
                }
                Debug.Assert(!ret || pNode != null);
                return (ret, pNode);
            }

            public Type ExpectedTypeOfSyntaxObject => typeof(InvocationExpressionSyntax);

            bool Interfaces.IUsingStatementSyntaxAnalyzer.IsPartOfUsingConstruct(object syntax) =>
                IsPartOfUsingConstruct(
                    (InvocationExpressionSyntax) (syntax ?? throw new ArgumentNullException(nameof(syntax))));
            bool Interfaces.IUsingStatementSyntaxAnalyzer.IsPArtOfInlineDeclUsingConstruct(object syntax) =>
                IsPartOfInlineDeclUsingConstruct((InvocationExpressionSyntax) syntax).PartOfInlineDeclUsing;

            private struct ParentNodeType
            {
                public static ParentNodeType FindTerminalParentNode([NotNull] InvocationExpressionSyntax node)
                {
                    SyntaxNode currentNode = node;
                    (bool IsTerminal, bool? Ok, SyntaxNode parent) result;
                    do
                    {
                        result = AnalyzeParentNode(currentNode);
                        currentNode = result.parent;
                    } while (!result.Ok.HasValue);

                    return new ParentNodeType(result.Ok.Value, currentNode);
                }

                public bool? Ok { get; }

                [CanBeNull] public SyntaxNode TerminalNode { get; }
                private ParentNodeType(bool ok, SyntaxNode terminalNode)
                {
                    Ok = ok;
                    TerminalNode = terminalNode;
                }

                private static (bool IsTerminal, bool? Ok, SyntaxNode parent) AnalyzeParentNode(SyntaxNode node)
                {
                    bool isTerminal;
                    bool? isOk;
                    SyntaxNode parent = node.Parent;
                    switch (parent?.Kind())
                    {
                        case null:
                            isOk = false;
                            isTerminal = true;
                            break;
                        case SyntaxKind.ExpressionStatement:
                            isOk = false;
                            isTerminal = true;
                            break;
                        case SyntaxKind.UsingStatement:
                            isOk = true;
                            isTerminal = true;
                            break;
                        case SyntaxKind.LocalDeclarationStatement:
                            isOk = parent.DescendantTokens().Any(token => token.IsKind(SyntaxKind.UsingKeyword));
                            isTerminal = true;
                            break;
                        default:
                            isOk = null;
                            isTerminal = false;
                            break;
                    }

                    return (isTerminal, isOk, parent);
                }
            }

            private (bool HasIt, VariableDeclarationSyntax Syntax) HasVariableDeclarationChildNode(UsingStatementSyntax syntax)
            {
                VariableDeclarationSyntax ret = syntax.ChildNodes().OfType<VariableDeclarationSyntax>()
                    .FirstOrDefault();
                return (ret != null, ret);
            }
        }

        private static Func<IUsingStatementSyntaxAnalyzer> UsingStatementAnalyzerFactory
        {
            get
            {
                Func<IUsingStatementSyntaxAnalyzer> factory = _usingStatementAnalyzerFactory;
                if (factory == null)
                {
                    Func<IUsingStatementSyntaxAnalyzer> newF = () => CreateStatementAnalyzer();
                    Interlocked.CompareExchange(ref _usingStatementAnalyzerFactory,
                        newF, null);
                    factory = newF;
                    Debug.Assert(factory != null);
                }
                return factory;
            }
        }

        private static volatile Func<IUsingStatementSyntaxAnalyzer> _usingStatementAnalyzerFactory;
    }
}
