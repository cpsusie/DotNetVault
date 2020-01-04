using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using DotNetVault.Attributes;
using DotNetVault.LockedResources;
using DotNetVault.Logging;
using DotNetVault.Test.Helpers;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeFixVerifier = DotNetVault.Test.Verifiers.CodeFixVerifier;

namespace DotNetVault.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        public static readonly string NotVaultSafeFormatString = Resources.AnalyzerMessageFormat; 
        //No diagnostics expected to show up
        [TestMethod]
        public void ValidateEmpty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestVaultSafeGenericTypeWithVsTypeParam()
        {
            var test = ResourceFiles.VaultSafeGenericTypesTestCases.VaultSafeGenericShouldBeOkExamples;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ValidateNoDiagnosticBecauseOnFaith() 
        {
            var test = ResourceFiles.VaultSafeTestCases.NoDiagnosticBecauseOnFaith;
            VerifyCSharpDiagnostic(test); 
        }

        [TestMethod]
        public void TestVsTpObjCreate()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.VsTpObjectCreationExpDiag;
            VerifyCSharpDiagnostic(test, diag => diag.Count() == 1,
                diag => diag.Id == DotNetVaultAnalyzer.DotNetVault_VsTypeParams_Object_Create &&
                        diag.Location.GetLineSpan().StartLinePosition.Line == 26);
        }

        [TestMethod]
        public void ValidateNoAttribute()
        {
            var test = ResourceFiles.VaultSafeTestCases.NoDiagnosticNoAttribute;
            VerifyCSharpDiagnostic(test);
            TraceLog.Log("Hi mom!");
        }
        [TestMethod]
        public void TestStringIsWhiteListed()
        {
            var test = ResourceFiles.WhiteListTests.TestWhiteListedVaultSafety;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestUriWhiteListed()
        {
            var test = ResourceFiles.WhiteListTests.TestUriWhiteListedVaultSafety;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestUriBandStringBNotWhiteListed()
        {
            var test = ResourceFiles.WhiteListTests.SbAndUriBNotVaultSafe;
            VerifyCSharpDiagnostic(test, col => col.Count() == 2, dx => true);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void DetectsLowercaseLettersInSymbolsWithAttribute()
        {
            var test = ResourceFiles.VaultSafeTestCases.ShouldTriggerDiagnosticNotSealedAndVsAttribute;
            var expected = new DiagnosticResult
            {
                Id = DotNetVaultAnalyzer.DiagnosticId_VaultSafeTypes,
                Message = string.Format(NotVaultSafeFormatString, "ShouldTriggerDiagnosticNotSealedAndVsAttribute"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 18)
                        }
            };
            VerifyCSharpDiagnostic(test, expected);

            test = ResourceFiles.VaultSafeTestCases.ShouldTriggerDiagnosticLowercaseAndVsAttributeV2;
            expected = new DiagnosticResult
            {
                Id = DotNetVaultAnalyzer.DiagnosticId_VaultSafeTypes,
                Message = string.Format(NotVaultSafeFormatString, "ShouldTriggerDiagnosticNotSealedAndVsAttribute"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 7, 18)
                    }
            };
            VerifyCSharpDiagnostic(test, expected);

            test = ResourceFiles.VaultSafeTestCases.ShouldTriggerDiagnosticLowercaseAndVsAttributeV3;
            expected = new DiagnosticResult
            {
                Id = DotNetVaultAnalyzer.DiagnosticId_VaultSafeTypes,
                Message = string.Format(NotVaultSafeFormatString, "ShouldTriggerDiagnosticNotSealedAndVsAttribute"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 7, 18)
                    }
            };
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TestNoDiagInitialVsTypMethInvk()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.MethodInvokeSyntaxTests;
            VerifyCSharpDiagnostic(test, diag => diag.Count() == 1,
                diag => diag.Id == DotNetVaultAnalyzer.DotNetVault_VsTypeParams_Method_Invoke &&
                        diag.Location.GetLineSpan().StartLinePosition.Line == 31);
        }

        [TestMethod]
        public void TestNoDiagInitialVsTypMethInvk_2()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.MethodInvokeSyntaxTests_2;
            VerifyCSharpDiagnostic(test,  diag => diag.Count() == 1,
                diag => diag.Id == DotNetVaultAnalyzer.DotNetVault_VsTypeParams_Method_Invoke &&
                        diag.Location.GetLineSpan().StartLinePosition.Line == 33);
        }

        [TestMethod]
        public void TestNoDiagnosticBecauseAttributeYetAllUpper()
        {
            var test = ResourceFiles.VaultSafeTestCases.NoDiagnosticAttributeYetCompliantCat;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestNoDiagnosticBecauseInUsingConstruct()
        {
            var test = ResourceFiles.UsingMandatoryTestCases.NoDiagnosticPartOfUsingStatement;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestOutOfLineDeclarationCausesDiagnostic()
        {
            var test = ResourceFiles.UsingMandatoryTestCases.EmitsDiagnosticUmRequiresInlineDecl;
            VerifyCSharpDiagnostic(test, diag => diag.Any(),
                diag => diag.Id == DotNetVaultAnalyzer.DiagnosticId_UsingMandatory_Inline);
        }

        [TestMethod]
        public void TestGetDiagnosticNoUsing()
        {
            var test = ResourceFiles.UsingMandatoryTestCases.ShouldTriggerDiagnosticBecauseNoUsing;
            VerifyCSharpDiagnostic(test, diag => diag.Count() == 1, diag => diag.Id == DotNetVaultAnalyzer.DiagnosticId_UsingMandatory);
        }

        [TestMethod]
        public void TestGetDiagnosticMoreComplexWrongUsing()
        {
            var test = ResourceFiles.UsingMandatoryTestCases.DiagnosticALittleMoreComplex;
            VerifyCSharpDiagnostic(test, diag => diag.Count() == 1, diag => 
                diag.Id == DotNetVaultAnalyzer.DiagnosticId_UsingMandatory && diag.Location.GetLineSpan().StartLinePosition.Line == 12 );
        }

        [TestMethod]
        public void TestNoDiagnosticMoreComplexUsingWhereMandated()
        {
            var test = ResourceFiles.UsingMandatoryTestCases.NoDiagnosticALittleMoreComplex;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestVsTp()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.VsTpTypeHaver;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestImmutDictIsVs()
        {
            var test = ResourceFiles.ConditionallyVsImmutableTypes.ImmutableCollectionIsVaultSafe;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestImmutStructIsVs()
        {
            var test = ResourceFiles.ConditionallyVsImmutableTypes.ImmutableStructIsVaultSafe;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestImmutStructIsVsUnmanaged()
        {
            var test = ResourceFiles.ConditionallyVsImmutableTypes.ImmutableStructIsVsUnmanaged;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestStructWithNonReadonlyConditImmutField()
        {
            var test = ResourceFiles.ConditionallyVsImmutableTypes.ConditionallyImmutStructWithNonROImmutF;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestClassWithNonReadonlyConditImmutField()
        {
            var test = ResourceFiles.ConditionallyVsImmutableTypes.ConditionallyImmutableClassNonRoImmutF;
            VerifyCSharpDiagnostic(test, col => col.Count() == 1, diag => diag.Id == DotNetVaultAnalyzer.DiagnosticId_VaultSafeTypes);
        }

        [TestMethod]
        public void IdentifyDelegateCreationTest()
        {
            var test = ResourceFiles.VaultDelegates.QueryTestCases;
            VerifyCSharpDiagnostic(test, col => col.Count() == 1, diag => diag.Id == DotNetVaultAnalyzer.DotNetVault_VsTypeParams_DelegateCreate);
        }

        [TestMethod]
        public void IdentifyDelegateCreationWithNoNonVsCaptureAttrib()
        {
            var test = ResourceFiles.VaultDelegates.NonVsCaptureTestCases;
            VerifyCSharpDiagnostic(test, col => col.Count() == 2,
                dx => dx.Id == DotNetVaultAnalyzer.DotNetVault_VsDelegateCapture);
        }

        [TestMethod]
        public void MakeSureDynamicNeverVaultSafe()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.TestDynamicNeverConsideredVaultSafe;
            VerifyCSharpDiagnostic(test, col => col.Count() == 5, d => true);
        }

        [TestMethod]
        public void EnsureDynamicNotVaultSafeInDelegateCreation()
        {
            var test = ResourceFiles.VaultSafeTypeParamTypeTestCases.DelegateCreationOpsAndDynamic;
            VerifyCSharpDiagnostic(test, col => col.Count() == 1, d => true);
        }

        [TestMethod]
        public void TestVaultSymbolTypeIdentification()
        {
            //need to edit code in analyzer for this test to be meaningful ... no access to vault
            var test = ResourceFiles.VaultSymbolIdentificationTest.TestIdentifyVaultSymbolDeclaration;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void AnalyzerNotHeldAgainstUnitTests()
        {
            var test = ResourceFiles.NullableNotHeldAgainstTests.NullableNotHeldAgainstTest;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void EventArgsOkTest()
        {
            var test = ResourceFiles.NullableNotHeldAgainstTests.EventArgsOkTest;
            VerifyCSharpDiagnostic(test);
        }

        //protected override CodeFixProvider GetCSharpCodeFixProvider()
        //{
        //    return new AnalyzerYetAgainCodeFixProvider();
        //}

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new DotNetVaultAnalyzer();

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a project and return the documents and spans of it.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Tuple containing the Documents produced from the sources and their TextSpans if relevant</returns>
        [UsedImplicitly]
        private static Document[] GetDocuments(string[] sources, string language)
        {
            if (language != LanguageNames.CSharp && language != LanguageNames.VisualBasic)
            {
                throw new ArgumentException("Unsupported Language");
            }

            var project = CreateProject(sources, language);
            var documents = project.Documents.ToArray();

            if (sources.Length != documents.Length)
            {
                throw new InvalidOperationException("Amount of sources did not match amount of Documents created");
            }

            return documents;
        }

        /// <summary>
        /// Create a project using the inputted strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Project created out of the Documents created from the source strings</returns>
        private static Project CreateProject(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileNamePrefix = DefaultFilePathPrefix;
            string fileExt = language == LanguageNames.CSharp ? CSharpDefaultFileExt : VisualBasicDefaultExt;

            var projectId = ProjectId.CreateNewId(debugName: TestProjectName);

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName,
                    language).WithProjectCompilationOptions(projectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(
                        MetadataImportOptions.All))
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CSharpSymbolsReference)
                .AddMetadataReference(projectId, CodeAnalysisReference)
                .AddMetadataReference(projectId, DataContractReference)
                .AddMetadataReference(projectId, AttributeReference)
                .AddMetadataReference(projectId, TestAnalyzerReference)
                .AddMetadataReference(projectId, ImmutableTypesReference)
                .AddMetadataReference(projectId, VaultQueryReference)
                .AddMetadataReference(projectId, UriReference);


            int count = 0;
            foreach (var source in sources)
            {
                var newFileName = fileNamePrefix + count + "." + fileExt;
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, SourceText.From(source));
                count++;
            }
            return solution.GetProject(projectId);
        }
        private static readonly MetadataReference UriReference = MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location);
        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference AttributeReference = MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location);
        private static readonly MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
        private static readonly MetadataReference CodeAnalysisReference = MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);
        private static readonly MetadataReference TestAnalyzerReference = MetadataReference.CreateFromFile(typeof(VaultSafeAttribute).Assembly.Location);
        private static readonly MetadataReference DataContractReference = MetadataReference.CreateFromFile(typeof(DataContractAttribute).Assembly.Location);
        private static readonly MetadataReference ImmutableTypesReference = MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location);
        private static readonly MetadataReference VaultQueryReference =
            MetadataReference.CreateFromFile(typeof(VaultQuery<,>).Assembly.Location);
    }
}
