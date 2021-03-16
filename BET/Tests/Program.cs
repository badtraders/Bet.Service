using System;
using System.Diagnostics.CodeAnalysis;

namespace Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello World!");
                Console.WriteLine(A(null));
                Console.ReadKey();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static string A([NotNull]string v)
        {
            return $"{v}={v}";
        }
    }
}
