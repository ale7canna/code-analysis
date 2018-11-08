using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    internal static class Application
    {
        public static void FindMethodOwner(List<string> args)
        {
            var filePath = args.First();
            var lineNumber = Convert.ToInt32(args.Skip(1).First());

            var method = GetMethodFromFileAndLine(filePath, lineNumber);

            Console.WriteLine(method == null
                ? "No method found."
                : $"Line with number: {lineNumber} belongs to {method.Identifier.Text} method");
        }

        private static MethodDeclarationSyntax GetMethodFromFileAndLine(string filePath, int lineNumber)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
            var root = tree.GetCompilationUnitRoot();

            return root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .SingleOrDefault(m => lineNumber.IsBetween(m.GetLocation().GetLineSpan()));
        }

        public static void PrintMethodsInfo(List<string> args)
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

        public static void ListAuthors(List<string> args)
        {
            var repository = new Repository(args[0]);
            var authors = repository.Commits.Select(c => (c.Author.Name, c.Author.Email)).Distinct()
                .OrderBy(a => a.Item1 + a.Item2);

            foreach (var author in authors)
            {
                Console.WriteLine($"{author.Item1} - {author.Item2}");
            }
        }

        public static void ListDiff(List<string> args)
        {
            var repository = new Repository(args[0]);

            var t1 = repository.Commits.Skip(2).First().Tree;
            var t2 = repository.Commits.First().Tree;
            Patch diff = repository.Diff.Compare<Patch>(t1, t2);
            Console.WriteLine(diff.Content);
            foreach (var add in diff)
            {
                Console.WriteLine("This is a diff patch");
                Console.WriteLine(add);
                Console.WriteLine();
            }
        }

        public static void MethodChanges(List<string> args)
        {
            var repository = new Repository(args[0]);

            var c1 = repository.Lookup<Commit>(args[1]);
            var c2 = repository.Lookup<Commit>(args[2]);

            var result = new Dictionary<string, Dictionary<string, int>>();
            var compare = repository.Diff.Compare<Patch>(c1.Tree, c2.Tree);
            foreach (var c in compare)
            {
                var fp = $"{args[0]}{Path.DirectorySeparatorChar}{c.Path}";
                if (!(File.Exists(fp) && fp.EndsWith(".cs"))) continue;

                result.Add(c.Path, new Dictionary<string, int>());
                var diff = compare[c.Path];
                foreach (var chunk in diff.Hunks)
                {
                    var content = chunk.AddedLines.Concat(chunk.RemovedLines);
                    foreach (var line in content)
                    {
                        var method = GetMethodFromFileAndLine(fp, line.LineNumber);
                        if (method == null)
                            continue;
                        if (!result[c.Path].ContainsKey(method.Identifier.Text))
                            result[c.Path].Add(method.Identifier.Text, 0);
                        result[c.Path][method.Identifier.Text]++;
                    }
                }
            }

            var keyValuePairs = result.SelectMany(kv => kv.Value.Select(kv1 => (kv.Key, kv1.Key, kv1.Value)));
            var res = keyValuePairs.OrderByDescending(kv => kv.Item3);
            Console.WriteLine(
                $"{NicePrint("File name")} - {NicePrint("Method name")} - {NicePrint("adds/rems count")}");
            foreach (var re in res)
            {
                Console.WriteLine($"{NicePrint(re.Item1)} - {NicePrint(re.Item2)} - {re.Item3}");
            }
        }

        private static string NicePrint(string s)
        {
            var totalWidth = 65;
            var nicePrint = s.PadRight(totalWidth, ' ');
            return nicePrint.Substring(nicePrint.Length - totalWidth, totalWidth);
        }
    }
}