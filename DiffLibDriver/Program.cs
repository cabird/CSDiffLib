using System.Runtime.InteropServices;
using CSDiffLib;

namespace DiffLibDriver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Test3();
        }

        static void Test1()
        {
            var a = "I like traffic lights".Split(' ').ToList();
            var b = "I love traffic lights a lot".Split (' ').ToList();

            var a2 = "abxcd".ToList();
            var b2 = "abcd".ToList();
            
            var sd = new SequenceMatcher<char>(a2, b2);
            var foo = sd.GetMatchingBlocks();
            foreach (var match in foo)
            {
                Console.WriteLine(match);
            }
            Console.WriteLine(foo);
        }

        static void Test2()
        {
            var a = "I like traffic lights".Split(' ').ToList();
            var b = "I love traffic lights a lot".Split(' ').ToList();

            var a2 = "qabxcd".ToList();
            var b2 = "abycdf".ToList();

            var sd = new SequenceMatcher<char>(a2, b2);
            var foo = sd.GetOpCodes();
            foreach (var opcode in foo)
            {
                Console.WriteLine(opcode.ToString(a2, b2));
            }
            Console.WriteLine(foo);
        }

        static void Test3()
        {
            var a = Enumerable.Range(1, 39).Select(x => x.ToString()).ToList();
            var b = Enumerable.Range(1, 39).Select(x => x.ToString()).ToList();
            b.Insert(8, "i");
            b[20] += "x";
            for (var i = 0; i < 5; i++)
            {
                b.RemoveAt(23);
            }
            b[30] += 'y';

            Console.WriteLine("a: " + String.Join(" ", a));
            Console.WriteLine("b: " + String.Join(" ", b));


            var sd = new SequenceMatcher<string>(a, b);
            var foo = sd.GetOpCodes();
            foreach (var opcode in foo)
            {
                Console.WriteLine(opcode.ToString(a, b));
            }
            Console.WriteLine(foo);
        }
    }
}