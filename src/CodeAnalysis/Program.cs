using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
                PrintMethodsInfo(args);
            else if (args.Length == 2)
                FindMethodOwner(args);
        }

        private static void FindMethodOwner(string[] args)
        {
            var filePath = args[0];
            var lineNumber = Convert.ToInt32(args[1]);

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
            var root = tree.GetCompilationUnitRoot();

            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Single(m => lineNumber.IsBetween(m.GetLocation().GetLineSpan()));

            Console.WriteLine($"Line with number: {lineNumber} belongs to {method.Identifier.Text} method");
        }

        private static void PrintMethodsInfo(string[] args)
        {
            var filePath = args[0];
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