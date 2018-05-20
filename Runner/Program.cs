using SmvGenerator;
using System;
using System.Diagnostics;
using System.IO;

namespace Runner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 4)
            {
                int minLength = int.Parse(args[0]);
                int maxLength = int.Parse(args[1]);
                int n = int.Parse(args[2]);
                string inFilename = args[3];
                string outFilename = args[4];

                bool vacuity = false;
                bool negative = false;

                for (int i = 5; i < args.Length; ++i)
                {
                    if (args[i] == "-vacuity")
                        vacuity = true;
                    else if (args[i] == "-negative")
                        negative = true;
                    else throw new ArgumentException("Unknown argument '" + args[i] + "'");
                }

                if (!File.Exists(inFilename))
                {
                    Console.WriteLine($"File '{inFilename}' not found");
                    return;
                }

                inFilename = Path.GetFullPath(inFilename);
                outFilename = Path.GetFullPath(outFilename);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
                Generator.GenerateLogEvenTraceDistribution(minLength, maxLength, n, inFilename, outFilename, vacuity, negative);
            }
            else
            {
                Console.WriteLine("\nusage: runner.exe minLength maxLength NTraces input output " +
                        "[-vacuity] [-negative]\n\n" +
                        "example use: runner.exe 5 15 1000 model.decl log -vacuity\n\n\n" +
                        "\targuments:" +
                        "minLength - integer number, minimal length of trace\n\n" +
                        "maxLength - integer number, maximal length of trace\n\n" +
                        "NTraces - integer number, minimal length of trace\n\n" +
                        "input - name of input file (model); relative or absolute location\n\n" +
                        "output - name of output file (smv)\n\n" +
                        "\toptional parameters:\n\n" +
                        "-vacuity - all constraints in the model will be activated at least once for each trace\n\n" +
                        "-negative - all trace will have at least one constraint violated\n\n");
            }
        }
    }
}
