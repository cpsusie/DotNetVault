using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using DotNetVault.UtilitySources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using DiagnosticVerifier = DotNetVault.Test.Verifiers.DiagnosticVerifier;

namespace DotNetVault.Test
{
    [TestClass]
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public class VaultSafeAnalysisTests
    {
        [TestMethod]
        public void TestReadingTheList()
        {
            var analyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
            string[] expected =
                {typeof(string).FullName, typeof(DateTime).FullName, typeof(Guid).FullName, typeof(TimeSpan).FullName};
            var whiteList = analyzer.WhiteList;
            var fsExempt = analyzer.FieldScanExempt;
            
            Assert.IsTrue(whiteList.SetEquals(fsExempt));
            Assert.IsTrue(whiteList.IsSupersetOf(expected));

        }
        
        [TestMethod]
        public void TestReadingConditionalWhiteList()
        {
            var analyzer = VaultSafeAnalyzerFactorySource.CreateDefaultAnalyzer();
            ImmutableHashSet<string> expected = ImmutableHashSet.Create(new[]
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
            ImmutableHashSet<string> actual = analyzer.ConditionalGenericWhiteList;
            Assert.IsTrue(actual.IsSupersetOf(expected));
        }

        

        [TestMethod]
        public void TestSimplePositiveTypes()
        {
            const string lyingRatName = "DotNetVault.Test.TestCases.LyingRat";
            const string dogNAme = "DotNetVault.Test.TestCases.Dog";
            const string defVsName = "DotNetVault.Test.TestCases.DefaultVaultSafe";
            const string doggiePairName = "DotNetVault.Test.TestCases.DoggiePair";
            var source = ResourceFiles.VaultSafeAnalysisTestCases.Dog;
            var extractionResult = ExtractTypeInfo(source, dogNAme);
            var dateTimeResult = ExtractTypeInfo(source, typeof(DateTime).FullName ?? throw new ArgumentNullException());
            var stringResult = ExtractTypeInfo(source, typeof(string).FullName ?? throw new ArgumentNullException());
            var lyingRatResult = ExtractTypeInfo(source, lyingRatName);
            var defVsResult = ExtractTypeInfo(source, defVsName);
            var doggyPairRes = ExtractTypeInfo(source, doggiePairName);
            Assert.IsTrue(extractionResult.Success);
            Assert.IsTrue(dateTimeResult.Success);
            Assert.IsTrue(stringResult.Success);
            Assert.IsTrue(lyingRatResult.Success);
            Assert.IsTrue(defVsResult.Success);
            Assert.IsTrue(doggyPairRes.Success);

            Assert.IsTrue(Analyzer.IsTypeVaultSafe(extractionResult.TypeSymbol, extractionResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(dateTimeResult.TypeSymbol, dateTimeResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(stringResult.TypeSymbol, stringResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(extractionResult.TypeSymbol, extractionResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(lyingRatResult.TypeSymbol, lyingRatResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(defVsResult.TypeSymbol, defVsResult.Compilation));
            Assert.IsTrue(Analyzer.IsTypeVaultSafe(doggyPairRes.TypeSymbol, doggyPairRes.Compilation));
        }

        [TestMethod]
        public void LoadDiagnosticDescriptorsText()
        {
            const int numDescriptors = 19;
            var descriptors = DotNetVaultAnalyzer.DiagnosticDescriptors;
            Assert.IsTrue(numDescriptors == descriptors.Length);
            foreach (var descriptor in descriptors)
            {
                Console.WriteLine($"Descriptor Id: {descriptor.Id}, Title: {descriptor.Title}," +
                                  $" Descriptor Category: {descriptor.Category}, " +
                                  $"Descriptor description: {descriptor.Description}.");
            }

        }
        [TestMethod]
        public void TestSimpleNegativeCases()
        {
            const string wouldName = "DotNetVault.Test.TestCases.WouldBeVaultSafeIfSoAnnotated";
            const string catName = "DotNetVault.Test.TestCases.CatThatIsntSealed";
            const string bobcatName = "DotNetVault.Test.TestCases.Bobcat";
            var source = ResourceFiles.VaultSafeAnalysisTestCases.Dog;
            var wouldResult = ExtractTypeInfo(source, wouldName);
            var catRes = ExtractTypeInfo(source, catName);
            var bobCatRes = ExtractTypeInfo(source, bobcatName);
            
            Assert.IsTrue(wouldResult.Success);
            Assert.IsTrue(catRes.Success);
            Assert.IsTrue(bobCatRes.Success);

            Assert.IsFalse(Analyzer.IsTypeVaultSafe(wouldResult.TypeSymbol, wouldResult.Compilation));
            Assert.IsFalse(Analyzer.IsTypeVaultSafe(catRes.TypeSymbol, catRes.Compilation));

            //counterpoint to cat

            Assert.IsTrue(Analyzer.IsTypeVaultSafe(bobCatRes.TypeSymbol, bobCatRes.Compilation));
        }

        public static (bool Success, CSharpCompilation Compilation, INamedTypeSymbol TypeSymbol, Exception Error) ExtractTypeInfo(
            [JetBrains.Annotations.NotNull] string source, [JetBrains.Annotations.NotNull] string metaDataName)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (metaDataName == null) throw new ArgumentNullException(nameof(metaDataName));

            Exception error;
            CSharpCompilation compilation;
            INamedTypeSymbol typeSymbol;
            try
            {
                compilation = CreateCompilation(source);
                typeSymbol = compilation.GetTypeByMetadataName(metaDataName);
                error = null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                compilation = null;
                typeSymbol = null;
                error = e;
            }

            return (compilation != null && typeSymbol != null, compilation, typeSymbol, error);
        }

        private static CSharpCompilation CreateCompilation(string source)
        {
            Project p = CreateProject(new[] {source});
            Compilation temp = p.GetCompilationAsync(CancellationToken.None).Result;
            return (CSharpCompilation) temp;
        }
        /// <summary>
        /// Create a project using the inputted strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Project created out of the Documents created from the source strings</returns>
        private static Project CreateProject(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileNamePrefix = DiagnosticVerifier.DefaultFilePathPrefix;
            string fileExt = language == LanguageNames.CSharp ? DiagnosticVerifier.CSharpDefaultFileExt : DiagnosticVerifier.VisualBasicDefaultExt;

            var projectId = ProjectId.CreateNewId(debugName: DiagnosticVerifier.TestProjectName);

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, DiagnosticVerifier.TestProjectName,
                    DiagnosticVerifier.TestProjectName, language).WithProjectCompilationOptions(projectId, 
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All))
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CSharpSymbolsReference)
                .AddMetadataReference(projectId, CodeAnalysisReference)
                .AddMetadataReference(projectId, DataContractReference)
                .AddMetadataReference(projectId, AttributeReference)
                .AddMetadataReference(projectId, TestAnalyzerReference);

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
        internal IVaultSafeTypeAnalyzer Analyzer => TheAnalyzer.Value;

        private static readonly LocklessWriteOnce<IVaultSafeTypeAnalyzer> TheAnalyzer = new LocklessWriteOnce<IVaultSafeTypeAnalyzer>(VaultSafeAnalyzerFactorySource.CreateAnalyzer);

        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference AttributeReference = MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location);
        private static readonly MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
        private static readonly MetadataReference CodeAnalysisReference = MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);
        private static readonly MetadataReference TestAnalyzerReference = MetadataReference.CreateFromFile(typeof(VaultSafeAttribute).Assembly.Location);
        private static readonly MetadataReference DataContractReference = MetadataReference.CreateFromFile(typeof(DataContractAttribute).Assembly.Location);
    }
}
