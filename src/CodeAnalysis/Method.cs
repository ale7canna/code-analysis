using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    internal class Method
    {
        public static Method From(string filePath, MethodDeclarationSyntax m)
        {
            return new Method
            {
                FilePath = filePath,
                Name = m.Identifier.Text,
                Parameters = m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList()
            };
        }

        internal List<string> Parameters { get; private set; }
        internal string Name { get; private set; }
        internal string FilePath { get; private set; }
    }
}