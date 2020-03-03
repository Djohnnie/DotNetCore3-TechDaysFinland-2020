using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static System.Console;

namespace _06_HardwareIntrinsics
{
    class Program
    {
        static void Main(string[] args)
        {
            WriteLine("Getting a billion integers...");
            var enumerable = Enumerable.Repeat(1, 1073741824).ToArray();
            var source = new ReadOnlySpan<int>(enumerable);

            int result;

            for (int i = 0; i < 3; i++)
            {
                Write("Calculating a regular sum... ");
                var sw1 = Stopwatch.StartNew();
                result = Sum(source);
                sw1.Stop();
                WriteLine($"{result} ({sw1.ElapsedMilliseconds}ms)");

                Write("Calculating a sum with SIMD support... ");
                var sw2 = Stopwatch.StartNew();
                result = SumSimd(source);
                sw2.Stop();
                WriteLine($"{result} ({sw2.ElapsedMilliseconds}ms)");

                Write("Calculating a sum with Hardware Intrinsics support... ");
                var sw3 = Stopwatch.StartNew();
                result = SumVectorized(source);
                sw3.Stop();
                WriteLine($"{result} ({sw3.ElapsedMilliseconds}ms)");
            }
        }

        static int Sum(ReadOnlySpan<int> source)
        {
            int result = 0;

            foreach (var number in source)
            {
                result += number;
            }

            return result;
        }

        static int SumSimd(ReadOnlySpan<int> source)
        {
            int result = 0;
            Vector<int> resultVector = Vector<int>.Zero;

            int i = 0;
            int lastBlockIndex = source.Length - source.Length % Vector<int>.Count;

            while (i < lastBlockIndex)
            {
                resultVector += new Vector<int>(source.Slice(i));
                i += Vector<int>.Count;
            }

            for (int n = 0; n < Vector<int>.Count; n++)
            {
                result += resultVector[n];
            }

            while (i < source.Length)
            {
                result += source[i];
                i += 1;
            }

            return result;
        }

        static int SumVectorized(ReadOnlySpan<int> source)
        {
            return Sse2.IsSupported ? SumVectorizedSse(source) : SumSimd(source);
        }

        static unsafe int SumVectorizedSse(ReadOnlySpan<int> source)
        {
            int result;

            fixed (int* sourcePointer = source)
            {
                Vector128<int> resultVector = Vector128<int>.Zero;

                int i = 0;
                int lastBlockIndex = source.Length - source.Length % 4;

                while (i < lastBlockIndex)
                {
                    resultVector = Sse2.Add(resultVector, Sse2.LoadVector128(sourcePointer + i));
                    i += 4;
                }

                if (Ssse3.IsSupported)
                {
                    resultVector = Ssse3.HorizontalAdd(resultVector, resultVector);
                    resultVector = Ssse3.HorizontalAdd(resultVector, resultVector);
                }
                else
                {
                    resultVector = Sse2.Add(resultVector, Sse2.Shuffle(resultVector, 0x4E));
                    resultVector = Sse2.Add(resultVector, Sse2.Shuffle(resultVector, 0xB1));
                }
                result = resultVector.ToScalar();

                while (i < source.Length)
                {
                    result += sourcePointer[i];
                    i += 1;
                }
            }

            return result;
        }
    }
}