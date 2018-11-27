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

            var method = File.ReadAllText(filePath).GetMethodFromLine(lineNumber);

            Console.WriteLine(method == null
                ? "No method found."
                : $"Line with number: {lineNumber} belongs to {method.Identifier.Text} method");
        }

        private static MethodDeclarationSyntax GetMethodFromFileAndLine(int lineNumber, string textToParse)
        {
            var tree = CSharpSyntaxTree.ParseText(textToParse);
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

            var result = new Dictionary<string, Dictionary<Method, int>>();
            var compare = repository.Diff.Compare<Patch>(c1.Tree, c2.Tree);
            foreach (var c in compare)
            {
                var fp = $"{args[0]}{Path.DirectorySeparatorChar}{c.Path}";
                if (!(File.Exists(fp) && fp.EndsWith(".cs"))) continue;

                result.Add(c.Path, new Dictionary<Method, int>());
                var diff = compare[c.Path];
                foreach (var chunk in diff.Hunks)
                {
                    var content = chunk.AddedLines.Concat(chunk.RemovedLines);
                    foreach (var line in content)
                    {
                        var methodInfo = GetMethodFromFileAndLine(line.LineNumber, File.ReadAllText(fp));
                        if (methodInfo == null)
                            continue;

                        var method = Method.From(fp, methodInfo);
                        if (!result[c.Path].ContainsKey(method))
                            result[c.Path].Add(method, 0);
                        result[c.Path][method]++;
                    }
                }
            }

            PrintResult(result);
        }

        private static void PrintResult(Dictionary<string, Dictionary<Method, int>> result)
        {
            var keyValuePairs = result.SelectMany(kv => kv.Value.Select(kv1 => (kv1.Key.FilePath, kv1.Key, kv1.Value)));
            var res = keyValuePairs.OrderByDescending(kv => kv.Item3);
            Console.WriteLine("file;method;changes(add/rems count)");
            foreach (var re in res)
            {
                Console.WriteLine($"{re.Item1};{re.Item2.Signature};{re.Item3}");
            }
        }

        public static void BranchChanges(List<string> args)
        {
            var repository = new Repository(args[0]);

            var result = repository.Branches.Where(b => b.IsRemote).Select(b => (b.CanonicalName, b.Commits.Count()));
            result = result.OrderByDescending(kv => kv.Item2);

            Console.WriteLine($"{NicePrint("branch name", 100)} - revs count");
            foreach (var re in result)
            {
                Console.WriteLine($"{NicePrint(re.Item1, 100)} - {re.Item2}");
            }
        }

        public static void MethodChangesHits(List<string> args)
        {
            var repository = new Repository(args[0].NormalizePath());

            var oldest = repository.Lookup<Commit>(args[1]);
            var latest = repository.Lookup<Commit>(args[2]);
            var commits = repository.Head.Commits
                .SkipWhile(c => !c.Equals(latest))
                .TakeWhile(c => !c.Equals(oldest))
                .Concat(new List<Commit> {oldest})
                .Reverse()
                .ToList();

            var result = ProcessResult(commits, repository);

            PrintResult(result);
        }

        private static Dictionary<string, Dictionary<Method, int>> ProcessResult(List<Commit> commits, IRepository repository)
        {
            var result = new Dictionary<string, Dictionary<Method, int>>();
            for (var k = 1; k < commits.Count; k++)
            {
                var commitFrom = commits[k - 1];
                var commitTo = commits[k];
                var compare = repository.Diff.Compare<Patch>(commitFrom.Tree, commitTo.Tree);
                var res = MineMethodsChangedBetweenCommits(compare, commitFrom, commitTo);
                result = JoinResult(result, res);
            }

            return result;
        }

        private static Dictionary<string, Dictionary<Method, int>> JoinResult(
            Dictionary<string, Dictionary<Method, int>> a,
            Dictionary<string, Dictionary<Method, int>> b)
        {
            var result = new Dictionary<string, Dictionary<Method, int>>();
            foreach (var kv in a)
            {
                result.Add(kv.Key, kv.Value);
            }

            foreach (var kv in b)
            {
                if (!result.ContainsKey(kv.Key))
                    result[kv.Key] = new Dictionary<Method, int>();
                foreach (var k in kv.Value)
                {
                    if (result[kv.Key].ContainsKey(k.Key))
                        result[kv.Key][k.Key]++;
                    else
                        result[kv.Key][k.Key] = 1;
                }
            }

            return result;
        }

        private static Dictionary<string, Dictionary<Method, int>> MineMethodsChangedBetweenCommits(
            Patch compare, Commit from, Commit to)
        {
            var partial = new Dictionary<string, Dictionary<Method, int>>();

            foreach (var changes in compare)
            {
                if (!changes.Path.EndsWith(".cs")) continue;

                var partialFrom = from.GetInvolvedMethodInFile(changes, h => h.RemovedLines);
                var partialTo = to.GetInvolvedMethodInFile(changes, h => h.AddedLines);
                var p = partialFrom.Concat(partialTo)
                    .Where(i => i != null)
                    .GroupBy(m => m, (m, ms) => (m, ms.Count()))
                    .ToDictionary(t => t.Item1, _ => 1);

                partial.Add(changes.Path, p);
            }

            return partial;
        }

        private static string NicePrint(string s, int stringWidth)
        {
            var totalWidth = stringWidth;
            var nicePrint = s.PadRight(totalWidth, ' ');
            return nicePrint.Substring(nicePrint.Length - totalWidth, totalWidth);
        }

        public static void RepositoryMethods(List<string> args)
        {
            var directory = args.First();
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            var result = files.SelectMany(MethodsInfo).OrderByDescending(i => i.LenghtInLines).Select(i =>
                $"{i.FilePath};{i.Signature};{i.LenghtInLines}");
            Console.WriteLine("file;method;length");
            foreach (var row in result)
            {
                Console.WriteLine(row);
            }
        }

        private static IEnumerable<Method> MethodsInfo(string filePath)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath));
            var root = tree.GetCompilationUnitRoot();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            return methods.Select(m => Method.From(filePath, m));
        }

        public static void JoinMethodsInformation(List<string> args)
        {
            var methodHits = ReadCsv(args[0]);
            var methodInRepo = ReadCsv(args[1]);

            var result = methodHits.Join(methodInRepo,
                hits => (hits.file, hits.method),
                repo => (repo.file, repo.method),
                (m, r) => (m.file, m.method, m.value * r.value));

            Console.WriteLine("file;method;changes * length");
            foreach (var res in result)
            {
                Console.WriteLine($"{res.Item1};{res.Item2};{res.Item3}");
            }
        }

        private static IEnumerable<(string file, string method, int value)> ReadCsv(string filepath)
        {
            return File.ReadAllLines(filepath).
                Skip(1).
                Select(l => l.Split(';')).
                Select(l => (l[0], l[1], int.Parse(l[2])));
        }
    }
}