using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    internal static class Application
    {
        public static void FindMethodOwner(IEnumerable<string> args)
        {
            var arguments = args.ToList();
            var filePath = arguments.First();
            var lineNumber = Convert.ToInt32(arguments.Skip(1).First());

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
            var root = tree.GetCompilationUnitRoot();

            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Single(m => lineNumber.IsBetween(m.GetLocation().GetLineSpan()));

            Console.WriteLine($"Line with number: {lineNumber} belongs to {method.Identifier.Text} method");
        }

        public static void PrintMethodsInfo(IEnumerable<string> args)
        {
            var filePath = args.First();
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
            var root = tree.GetCompilationUnitRoot();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var m in methods)
            {
                var d = new Dictionary<string, string>
                {
                    {"method", m.Identifier.Text},
                    {"body start", m.GetLocation().GetLineSpan().StartLinePosition.Line.ToString()},
                    {"body end", m.GetLocation().GetLineSpan().EndLinePosition.Line.ToString()}
                };

                var sb = new StringBuilder();
                foreach (var kv in d)
                {
                    sb.Append($"{kv.Key}: {kv.Value}        ");
                }

                Console.WriteLine(sb.ToString());
            }

            Console.ReadLine();
        }
    }
}