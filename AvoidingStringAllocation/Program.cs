using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;
using System.IO;
using System.Linq;

namespace AvoidingStringAllocation
{
    class Program
    {
        public static string Path = "Data.txt";
        static void Main(string[] args)
        {
            CreateTempFile();
            var summary = BenchmarkRunner.Run<ReadColumnFixedWidth>();
        }

        static void CreateTempFile()
        {
            if (File.Exists(Path) == false)
            {
                var line = new string('a', 165);
                using (StreamWriter writer = new StreamWriter(Path))
                {
                    for (int i = 0; i < 100_000; i++)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }
    }

    [Config(typeof(MyConfig))]
    public class ReadColumnFixedWidth
    {
        static int[] positions = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [Benchmark(Baseline = true)]
        public void ReadLine_ResultSubstring()
        {
            string[] result = new string[positions.Length];

            using (StreamReader sr = GetReader())
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        result[i] = line.Substring(lastIndex, positions[i]);
                        lastIndex += positions[i];
                    }
                }
            }
        }

        private int GetLineSize()
        {
            return positions.Sum();
        }

        private StreamReader GetReader()
        {
            return new StreamReader(Program.Path);
        }

        [Benchmark]
        public void Read_ResultSpan()
        {
            int bufferSize = GetLineSize();
            char[] buffer = new char[bufferSize];
            ReadOnlySpan<char>[] result = new ReadOnlySpan<char>[positions.Length];

            //Allocate char array of right size
            for (int i = 0; i < positions.Length; i++)
            {
                result[i] = new char[positions[i]];
            }

            using (TextReader sr = GetReader())
            {
                while (sr.Read(buffer, 0, bufferSize) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        result[i] = new ReadOnlySpan<char>(buffer, lastIndex, positions[i]);
                        lastIndex += positions[i];

                        //int.TryParse(result[i], out int x);
                    }
                }
            }
        }

        private class MyConfig : ManualConfig
        {
            public MyConfig()
            {
                Add(MemoryDiagnoser.Default);

                Add(
                    Job.Dry
                    .With(Platform.X64)
                    .With(Jit.RyuJit)
                    .With(Runtime.Clr)
                    .WithLaunchCount(5)
                    .WithInvocationCount(50)
                    //.WithIterationTime(TimeInterval.Millisecond * 200)
                    .WithMaxRelativeError(0.01)
                    .WithId("MySuperJob"));
            }
        }
    }
}
