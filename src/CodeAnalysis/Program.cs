using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeAnalysis
{
    internal static class Program
    {
        private static readonly Dictionary<string, Action<IEnumerable<string>>> Features =
            new Dictionary<string, Action<IEnumerable<string>>>
            {
                {"all-methods", Application.PrintMethodsInfo},
                {"method-owner", Application.FindMethodOwner},
                {"list-authors", Application.ListAuthors}
            };

        private static void Main(string[] args)
        {
            Features[args[0]](args.Skip(1));
        }
    }
}