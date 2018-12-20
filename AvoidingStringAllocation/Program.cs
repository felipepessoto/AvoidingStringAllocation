using BenchmarkDotNet.Running;
using System.IO;

namespace AvoidingStringAllocation
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<ReadColumnFixedWidth>();
        }
    }
}
