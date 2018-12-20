using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AvoidingStringAllocation
{
    [Config(typeof(MyConfig))]
    [MemoryDiagnoser]
    public class ReadColumnFixedWidth
    {
        private const long expected = 370370370000000;
        const string Path = "Data.txt";
        private readonly static int[] positions = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private readonly static StreamReader reader = new StreamReader(Path);

        private int GetLineSize()
        {
            int size = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                size += positions[i];
            }

            return size + Environment.NewLine.Length;
        }

        private static StreamReader GetReader()
        {
            reader.BaseStream.Position = 0;
            reader.DiscardBufferedData();
            return reader;
        }

        [Benchmark]
        public void Minimum()
        {
            GetReader();
        }

        [Benchmark(Baseline = true)]
        public long ReadLine_ResultSubstring()
        {
            long total = 0;

            TextReader sr = GetReader();
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        string result = line.Substring(lastIndex, positions[i]);
                        total += int.Parse(result);

                        lastIndex += positions[i];
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long ReadLine_ResultCharArray()
        {
            char[] result = new char[positions.Max()];
            long total = 0;

            TextReader sr = GetReader();
            {
                string line;
                while ((line = sr.ReadLine()) != null)//Allocation
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        int fieldLength = positions[i];

                        for (int j = 0; j < fieldLength; j++)
                        {
                            result[j] = line[lastIndex + j];
                        }

                        total += int.Parse(new string(result, 0, fieldLength));//Allocation
                        lastIndex += fieldLength;
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long Read_ResultCharArray()
        {
            int bufferSize = GetLineSize();
            char[] lineBuffer = new char[bufferSize];
            char[] result = new char[positions.Max()];

            long total = 0;

            TextReader sr = GetReader();
            {
                while (sr.Read(lineBuffer, 0, bufferSize) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        int fieldLength = positions[i];

                        for (int j = 0; j < fieldLength; j++)
                        {
                            result[j] = lineBuffer[lastIndex + j];
                        }

                        total += int.Parse(new string(result, 0, fieldLength));//Allocation
                        lastIndex += fieldLength;
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long Read_ResultCharArraySpan()
        {
            int bufferSize = GetLineSize();
            char[] lineBuffer = new char[bufferSize];
            char[] result = new char[positions.Max()];

            long total = 0;

            TextReader sr = GetReader();
            {
                while (sr.Read(lineBuffer, 0, bufferSize) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        int fieldLength = positions[i];

                        for (int j = 0; j < fieldLength; j++)
                        {
                            result[j] = lineBuffer[lastIndex + j];
                        }

                        total += int.Parse(new ReadOnlySpan<char>(result, 0, fieldLength));//Span
                        lastIndex += fieldLength;
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long Read_ResultUseUnsafeCharPointer()
        {
            int bufferSize = GetLineSize();
            char[] lineBuffer = new char[bufferSize];
            string[] result = new string[positions.Length];

            //Allocate empty strings of right size
            for (int i = 0; i < positions.Length; i++)
            {
                result[i] = new string(' ', positions[i]);
            }

            long total = 0;

            TextReader sr = GetReader();
            {
                while (sr.Read(lineBuffer, 0, bufferSize) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        int fieldLength = positions[i];

                        unsafe
                        {
                            fixed (char* chars = result[i])
                            {
                                for (int j = 0; j < fieldLength; j++)
                                {
                                    chars[j] = lineBuffer[lastIndex + j];
                                }
                            }
                        }

                        total += int.Parse(result[i]);
                        lastIndex += fieldLength;
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long Read_ResultSpanStackAlloc()
        {
            return Read_ResultSpan(true);
        }

        [Benchmark]
        public long Read_ResultSpanCharArray()
        {
            return Read_ResultSpan(false);
        }

        private long Read_ResultSpan(bool stack)
        {
            int bufferSize = GetLineSize();
            Span<char> buffer = stack ? stackalloc char[bufferSize] : new char[bufferSize];
            long total = 0;

            TextReader sr = GetReader();
            {
                while (sr.Read(buffer) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        ReadOnlySpan<char> result = buffer.Slice(lastIndex, positions[i]);
                        lastIndex += positions[i];
                        total += int.Parse(result);
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [Benchmark]
        public long Read_ResultSpanNative()
        {
            int bufferSize = GetLineSize();
            IntPtr ptr = Marshal.AllocHGlobal(bufferSize * 2);
            Span<char> buffer;
            unsafe { buffer = new Span<char>((char*)ptr, bufferSize); }
            long total = 0;

            TextReader sr = GetReader();
            {
                while (sr.Read(buffer) > 0)
                {
                    int lastIndex = 0;

                    for (int i = 0; i < positions.Length; i++)
                    {
                        ReadOnlySpan<char> result = buffer.Slice(lastIndex, positions[i]);
                        lastIndex += positions[i];
                        total += int.Parse(result);
                    }
                }
            }

            if (total != expected)
            {
                throw new Exception("Wrong result");
            }

            return total;
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (File.Exists(Path) == false)
            {
                var line = new string('1', 165);
                using (StreamWriter writer = new StreamWriter(Path))
                {
                    for (int i = 0; i < 100_000; i++)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }

        private class MyConfig : ManualConfig
        {
            public MyConfig()
            {
                //Add(Job.Default.With(Platform.X64).With(CsProjCoreToolchain.NetCoreApp21));
                Add(Job.Dry.With(Platform.X64).With(CsProjCoreToolchain.NetCoreApp21));
            }
        }
    }
}
