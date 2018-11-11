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

            return root.DescendantNodes().OfType<MethodDeclarationSyntax>().
                SingleOrDefault(m => lineNumber.IsBetween(m.GetLocation().GetLineSpan()));
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

            PrintResult(result);
        }

        private static void PrintResult(Dictionary<string, Dictionary<string, int>> result)
        {
            var keyValuePairs = result.SelectMany(kv => kv.Value.Select(kv1 => (kv.Key, kv1.Key, kv1.Value)));
            var res = keyValuePairs.OrderByDescending(kv => kv.Item3);
            Console.WriteLine(
                $"{NicePrint("File name", 65)} - {NicePrint("Method name", 65)} - {NicePrint("adds/rems count", 65)}");
            foreach (var re in res)
            {
                Console.WriteLine($"{NicePrint(re.Item1, 65)} - {NicePrint(re.Item2, 65)} - {re.Item3}");
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
            var repository = new Repository(args[0]);

            var oldest = repository.Lookup<Commit>(args[1]);
            var latest = repository.Lookup<Commit>(args[2]);
            var fromLatest = repository.Head.Commits.SkipWhile(c => !c.Equals(latest));
            var commits = fromLatest.TakeWhile(c => !c.Equals(oldest)).
                Concat(new List<Commit>{oldest}).
                Reverse().
                ToList();

            var result = new Dictionary<string, Dictionary<string, int>>();
            for (var k = 1; k < commits.Count; k++)
            {
                result = MineMethodsHitsBetweenCommits(result, repository, args[0], commits[k - 1], commits[k]);
            }

            PrintResult(result);
        }

        private static Dictionary<string, Dictionary<string, int>> MineMethodsHitsBetweenCommits(
            Dictionary<string, Dictionary<string, int>> result, Repository repository,
            string repositoryPath, Commit commitFrom, Commit commitTo)
        {
            var compare = repository.Diff.Compare<Patch>(commitFrom.Tree, commitTo.Tree);
            var res = MineMethodInFileChanges(compare, repositoryPath, repository, commitFrom, commitTo);
            return JoinResult(result, res);
        }

        private static Dictionary<string, Dictionary<string, int>> JoinResult(
            Dictionary<string, Dictionary<string, int>> a,
            Dictionary<string, Dictionary<string, int>> b)
        {
            var result = new Dictionary<string, Dictionary<string, int>>();
            foreach (var kv in a)
            {
                result.Add(kv.Key, kv.Value);
            }

            foreach (var kv in b)
            {
                if (!result.ContainsKey(kv.Key))
                    result[kv.Key] = new Dictionary<string, int>();
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

        private static Dictionary<string, Dictionary<string, int>> MineMethodInFileChanges(
            Patch compare, string repositoryPath, Repository repo, Commit from, Commit to)
        {
            var partial = new Dictionary<string, Dictionary<string, int>>();
            repo.Reset(ResetMode.Hard, from);

            foreach (var changes in compare)
            {
                var fp = $"{repositoryPath}{Path.DirectorySeparatorChar}{changes.Path}";
                if (!(File.Exists(fp) && fp.EndsWith(".cs"))) continue;

                partial.Add(changes.Path, new Dictionary<string, int>());
                repo.CheckoutPaths(from.Sha, new List<string>{changes.Path});
                foreach (var chunk in changes.Hunks)
                {
                    foreach (var line in chunk.RemovedLines)
                    {
                        AddMethodFromLineAndFile(fp, line, partial, changes);
                    }
                }

                repo.CheckoutPaths(to.Sha, new List<string>{changes.Path});
                foreach (var chunk in changes.Hunks)
                {
                    foreach (var line in chunk.AddedLines)
                    {
                        AddMethodFromLineAndFile(fp, line, partial, changes);
                    }
                }
            }

            repo.Reset(ResetMode.Hard, to);
            return partial;
        }

        private static void AddMethodFromLineAndFile(string file, Line line, Dictionary<string, Dictionary<string, int>> result, PatchEntryChanges patch)
        {
            var method = GetMethodFromFileAndLine(file, line.LineNumber);
            if (method == null) return;

            if (!result[patch.Path].ContainsKey(method.Identifier.Text))
                result[patch.Path].Add(method.Identifier.Text, 1);
            else
                result[patch.Path][method.Identifier.Text]++;
        }

        private static string NicePrint(string s, int stringWidth)
        {
            var totalWidth = stringWidth;
            var nicePrint = s.PadRight(totalWidth, ' ');
            return nicePrint.Substring(nicePrint.Length - totalWidth, totalWidth);
        }
    }
}