using System.IO;
using System.Linq;
using LibGit2Sharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    public static class StringExtensions
    {
        public static string NormalizePath(this string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        public static MethodDeclarationSyntax GetMethodFromLine(this string content, int line)
        {
            var tree = CSharpSyntaxTree.ParseText(content);
            var root = tree.GetCompilationUnitRoot();

            return root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .SingleOrDefault(m => line.IsBetween(m.GetLocation().GetLineSpan()));

        }

        public static Method GetMethodFromLineAndFile(this string fileContent, string file, Line line)
        {
            var methodInfo = fileContent.GetMethodFromLine(line.LineNumber);
            if (methodInfo == null) return null;

            return Method.From(file, methodInfo);
        }

    }
}