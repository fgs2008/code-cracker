﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestHelper
{
    public static class TestHelpers
    {
        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromAssembly(typeof(Enumerable).Assembly);
        private static readonly MetadataReference RegexReference = MetadataReference.CreateFromAssembly(typeof(System.Text.RegularExpressions.Regex).Assembly);
        private static readonly MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromAssembly(typeof(CSharpCompilation).Assembly);
        private static readonly MetadataReference CodeAnalysisReference = MetadataReference.CreateFromAssembly(typeof(Compilation).Assembly);
        private static readonly MetadataReference JsonNetReference = MetadataReference.CreateFromAssembly(typeof(JsonConvert).Assembly);

        internal static string DefaultFilePathPrefix = "Test";
        internal static string CSharpDefaultFileExt = "cs";
        internal static string VisualBasicDefaultExt = "vb";
        internal static string CSharpDefaultFilePath = DefaultFilePathPrefix + 0 + "." + CSharpDefaultFileExt;
        internal static string VisualBasicDefaultFilePath = DefaultFilePathPrefix + 0 + "." + VisualBasicDefaultExt;
        internal static string TestProjectName = "TestProject";

        #region Set up compilation and documents
        /// <summary>
        /// Given an array of strings as soruces and a language, turn them into a project and return the documents and spans of it.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Tuple containing the Documents produced from the sources and thier TextSpans if relevant</returns>
        public static Document[] GetDocuments(string[] sources, string language)
        {
            if (language != LanguageNames.CSharp && language != LanguageNames.VisualBasic)
            {
                throw new ArgumentException("Unsupported Language");
            }

            for (int i = 0; i < sources.Length; i++)
            {
                var fileName = language == LanguageNames.CSharp ? "Test" + i + ".cs" : "Test" + i + ".vb";
            }

            var project = CreateProject(sources, language);
            var documents = project.Documents.ToArray();

            if (sources.Length != documents.Length)
            {
                throw new SystemException("Amount of sources did not match amount of Documents created");
            }

            return documents;
        }

        /// <summary>
        /// Create a Document from a string through creating a project that contains it.
        /// </summary>
        /// <param name="source">Classes in the form of a string</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Document created from the source string</returns>
        public static Document CreateDocument(string source, string language = LanguageNames.CSharp)
        {
            return CreateProject(new[] { source }, language).Documents.First();
        }

        /// <summary>
        /// Create a project using the inputted strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <param name="solution">The created workspace containing the project</param>
        /// <returns>A Project created out of the Douments created from the source strings</returns>
        public static Project CreateProject(string[] sources,
            out CustomWorkspace workspace, string language = LanguageNames.CSharp)
        {
            var fileNamePrefix = DefaultFilePathPrefix;
            var fileExt = language == LanguageNames.CSharp ? CSharpDefaultFileExt : VisualBasicDefaultExt;

            var projectId = ProjectId.CreateNewId(debugName: TestProjectName);

            workspace = new CustomWorkspace();

            var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), TestProjectName,
                TestProjectName, language,
                metadataReferences: ImmutableList.Create(
                    CorlibReference, SystemCoreReference, RegexReference,
                    CSharpSymbolsReference, CodeAnalysisReference, JsonNetReference));

            workspace.AddProject(projectInfo);

            const int count = 0;
            foreach (var source in sources)
            {
                var newFileName = fileNamePrefix + count + "." + fileExt;
                workspace.AddDocument(projectId, newFileName, SourceText.From(source));
            }

            var project = workspace.CurrentSolution.GetProject(projectId);
            var newCompilationOptions = project.CompilationOptions.WithSpecificDiagnosticOptions(diagOptions);
            var solution = workspace.CurrentSolution.WithProjectCompilationOptions(projectId, newCompilationOptions);
            var newProject = solution.GetProject(projectId);
            return newProject;
        }

        private static readonly Dictionary<string, ReportDiagnostic> diagOptions = Enumerable.Range(1, 1000).Select(i => $"CC{i :D4}").ToDictionary(id => id, id => ReportDiagnostic.Default);

        /// <summary>
        /// Create a project using the inputted strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <param name="language">The language the source code is in</param>
        /// <returns>A Project created out of the Douments created from the source strings</returns>
        public static Project CreateProject(string[] sources, string language = LanguageNames.CSharp)
        {
            CustomWorkspace workspace;
            return CreateProject(sources, out workspace, language);
        }
        #endregion

        public static async Task<string> FormatSourceAsync(string language, string source)
        {
            var document = CreateDocument(source, language);
            var newDoc = await Formatter.FormatAsync(document);
            return (await newDoc.GetSyntaxRootAsync()).ToFullString();
        }

        /// <summary>
        /// Given a document, turn it into a string based on the syntax root
        /// </summary>
        /// <param name="document">The Document to be converted to a string</param>
        /// <returns>A string contianing the syntax of the Document after formatting</returns>
        public static async Task<string> GetStringFromDocumentAsync(Document document)
        {
            var simplifiedDoc = await Simplifier.ReduceAsync(document, Simplifier.Annotation);
            var root = await simplifiedDoc.GetSyntaxRootAsync();
            root = Formatter.Format(root, Formatter.Annotation, simplifiedDoc.Project.Solution.Workspace);
            return root.GetText().ToString();
        }

        public static string WrapInMethod(this string code, bool isAsync = false)
        {
            return string.Format(@"
    using System;

    namespace ConsoleApplication1
    {{
        class TypeName
        {{
            public {1}void Foo()
            {{
                {0}
            }}
        }}
    }}", code, isAsync ? "async " : "");
        }
    }
}