using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace CodeAnalysis
{
    public static class CommitExtensions
    {
        public static string GetFileContent(this Commit commit, string relativePath)
        {
            var blob = (Blob)commit[relativePath]?.Target;
            if (blob is null) return null;

            var contentStream = blob.GetContentStream();
            var buffer = new byte[contentStream.Length];
            contentStream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        public static IEnumerable<Method> GetInvolvedMethodInFile(this Commit commit,
            PatchEntryChanges changes,
            Func<Hunk, IEnumerable<Line>> linesStrategy)
        {
            var methods = new List<Method>();

            var fileContent = commit.GetFileContent(changes.Path);
            if (fileContent == null)
                return methods;

            foreach (var chunk in changes.Hunks)
            {
                methods.AddRange(linesStrategy(chunk)
                    .Select(line => fileContent.GetMethodFromLineAndFile(changes.Path, line)));
            }

            return methods.Distinct();
        }
    }
}