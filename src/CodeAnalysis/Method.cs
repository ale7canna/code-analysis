using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalysis
{
    internal class Method
    {
        public static Method From(string filePath, MethodDeclarationSyntax m)
        {
            var name = m.Identifier.Text;
            var parameters = m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList();
            return new Method
            {
                FilePath = filePath,
                Signature = $"{name}({string.Join(',', parameters)})",
                StartLine = m.GetLocation().GetLineSpan().StartLinePosition.Line,
                EndLine = m.GetLocation().GetLineSpan().EndLinePosition.Line
            };
        }

        public string Signature { get; set; }
        internal int StartLine { get; set; }
        internal int EndLine { get; set; }
        internal int LenghtInLines => EndLine - StartLine;
        internal string FilePath { get; private set; }

        public override bool Equals(object other)
        {
            if (other is Method m)
                return Signature == m.Signature &&
                       FilePath == m.FilePath;

            return false;
        }

        public override int GetHashCode()
        {
            return Signature.GetHashCode() * FilePath.GetHashCode();
        }
    }
}