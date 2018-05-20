using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace SmvGenerator
{
    public class Generator
    {
        private const string intermediateSmvFilename = "temp.smv";
        static StringBuilder sb = new StringBuilder();
        static AutoResetEvent ev = new AutoResetEvent(false);

        private static void RunSMV(int minL, int maxL, int N, string inFileName, StringBuilder sb)
        {
            Stopwatch sw = new Stopwatch();
            long total = 0;

            StringBuilder times = new StringBuilder();
            string log = string.Empty;
            sw.Start();


            string dbjson = File.ReadAllText(inFileName + ".smv.db.json");
            var dataBinding = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(dbjson);

            sb.Append(GenerateLogFromSMV(i => times.AppendLine($"{i},{sw.ElapsedMilliseconds}"), N, minL, inFileName + ".smv", dataBinding));

            sw.Stop();
            total += sw.ElapsedMilliseconds;

            //WriteLog(outFilename, log);
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        public static void GenerateLogEvenTraceDistribution(int minLength, int maxLength, int nTraces, string inFilename, string outFilename, bool vacuity, bool negative)
        {
            File.WriteAllText("ltlspecxml.txt", $"go_msat\nmsat_check_ltlspec_bmc -k {maxLength + 4}\nshow_traces -p4\nquit\n");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<log xes.version=\"1.0\" xes.features=\"nested-attributes\" openxes.version=\"1.0RC7\" xmlns=\"http://www.xes-standard.org/\">");
            nTraces /= (maxLength - minLength + 1);
            for (int i = minLength; i <= maxLength; ++i)
            {
                Console.WriteLine($"generation for {i} out of {maxLength}");
                GenerateSmv(i, i, nTraces, inFilename, inFilename, vacuity, negative);
                RunSMV(i, i, nTraces, inFilename, sb);
            }

            sb.AppendLine("</log>");
            File.WriteAllText(outFilename, sb.ToString());
        }

        private static void GenerateSmv(int minLength, int maxLength, int n, string inFilename, string outFilename, bool vacuity, bool negative)
        {
            string smvGenPath = "SmvGenerator.jar";
            if (!File.Exists(smvGenPath))
            {
                throw new FileNotFoundException("SmvGenerator.jar not found in " + smvGenPath);
            }

            StartWait("java",
                $"-jar \"{smvGenPath}\" " +
                $"{minLength} {maxLength} {n} \"{inFilename}\" \"{outFilename}.smv\"{MaybeOption(" -vacuity", vacuity)}{MaybeOption(" -negative", negative)}");
        }

        private static object MaybeOption(string v, bool e)
        {
            return e ? v : string.Empty;
        }

        static void StartWait(string exe, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            var ps = new Process();
            ps.StartInfo = psi;
            ps.Start();
            ps.WaitForExit();
            Console.WriteLine(ps.StandardError.ReadToEnd());
            Console.WriteLine(ps.StandardOutput.ReadToEnd());
        }
        
        private static string GenerateLogFromSMV(Action<int> logTimeForIthTrace, int limit, int length, string inFilename, Dictionary<string, List<string>> dataBinding)
        {
            StringBuilder log = new StringBuilder();
            string ltl = string.Empty;
            for (int i = 1; i <= limit; ++i)
            {
                File.Delete(intermediateSmvFilename);
                File.Copy(inFilename, intermediateSmvFilename);
                File.AppendAllText(intermediateSmvFilename, ltl);
                using (Process ps = new Process())
                {
                    ps.StartInfo.UseShellExecute = false;
                    ps.StartInfo.RedirectStandardError = true;
                    ps.StartInfo.RedirectStandardOutput = true;
                    ps.StartInfo.FileName = @".\NuXMV.exe";
                    ps.StartInfo.Arguments = $@"-dynamic -load .\ltlspecxml.txt .\temp.smv";
                    ps.OutputDataReceived += Ps_OutputDataReceived;
                    ps.ErrorDataReceived += Ps_ErrorDataReceived;
                    ps.StartInfo.RedirectStandardInput = true;

                    ps.Start();
                    ps.BeginOutputReadLine();
                    ps.BeginErrorReadLine();

                    ev.WaitOne();
                    string xml = sb.ToString();
                    int xmlStartIndex = xml.IndexOf("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                    if (xmlStartIndex >= 0)
                    {
                        xml = xml.Substring(xmlStartIndex);
                        var trace = ParseXML(xml, dataBinding);
                        log.AppendLine(FormatTrace(trace));
                        ltl = ltl + " | " + ExcludeTrace(trace);
                        sb = new StringBuilder();
                        logTimeForIthTrace(i);
                    }

                    ps.StandardInput.WriteLine("quit");
                    ps.WaitForExit();

                }
            }

            //File.WriteAllText("lastltl.txt", ltl);
            return log.ToString();
        }

        private static void WriteLog(string outFilename, string log)
        {
            string xmlPrefix = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<log xes.version=\"1.0\" xes.features=\"nested-attributes\" openxes.version=\"1.0RC7\" xmlns=\"http://www.xes-standard.org/\">\n";
            string xmlPostfix = "</log>\n";
            File.WriteAllText(outFilename, xmlPrefix);
            File.AppendAllText(outFilename, log);
            File.AppendAllText(outFilename, xmlPostfix);
            Console.WriteLine($"written at '{outFilename}'");
        }

        private static string GetValue(XElement element, string attr)
        {
            return element.Elements().First(x => x.FirstAttribute.Value == attr).Value;
        }

        private static string ExcludeTrace(List<State> trace)
        {
            StringBuilder ltl = new StringBuilder("first & ");
            int pc = 0;
            foreach (var i in trace)
            {
                if (pc != 0)
                    ltl.Append(" & X (");

                ltl.Append("state = ").Append(i.Name);
                foreach (var j in i.Data.Keys)
                    ltl.Append(" & ").Append(j).Append(" = ").Append(i.Data[j]);

                ++pc;
            }

            for (int i = 1; i < pc; ++i)
                ltl.Append(')');

            return ltl.ToString();
        }

        private static List<State> ParseXML(string xmltext, Dictionary<string, List<string>> dataBinding)
        {
            List<State> trace = new List<State>();
            var xml = XElement.Parse(xmltext);
            int index = 0;
            foreach (var i in xml.Elements())
            {
                if (i.Elements().Count() == 0)
                    break;

                var element = i.Elements().First();
                State state = new State();
                state.EventId = ++index;
                state.Name = GetValue(element, "state");

                if (state.Name == "_tail")
                    break;

                if (dataBinding.ContainsKey(state.Name))
                {
                    foreach (var j in dataBinding[state.Name])
                        state.Data.Add(j, GetValue(element, j));
                }

                trace.Add(state);
            }

            return trace;
        }

        private static string FormatTrace(List<State> trace, int id = 0)
        {
            StringBuilder strace = new StringBuilder();
            strace.AppendLine("\t<trace>");
            strace.AppendLine($"\t\t<string key=\"concept:name\" value=\"Case No. {id}\"/>");
            foreach (var i in trace)
            {
                strace.AppendLine("\t\t<event>");
                strace.AppendLine($"\t\t\t<string key=\"concept:name\" value=\"{i.Name}\"/>");
                foreach (var j in i.Data.Keys)
                    strace.AppendLine($"\t\t\t<string key=\"{j}\" value=\"{i.Data[j]}\"/>");

                //strace.AppendLine("<string key=\"lifecycle: transition\" value=\"complete\"/>");
                //strace.AppendLine("<date key=\"time:timestamp\" value=\"2018-02-18T00:34:56.013+02:00\"/>");
                strace.AppendLine("\t\t</event>");
            }

            strace.AppendLine("\t</trace>");
            return strace.ToString();
        }


        private static void Ps_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                if (!e.Data.Contains("Warning: -load is deprecated"))
                    Console.WriteLine(e.Data);
                if (e.Data.Contains("There are no traces currently available."))
                    ev.Set();
            }
        }

        private static void Ps_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            sb.AppendLine(e.Data);
            if (e.Data.Contains("</counter-example>"))
                ev.Set();
        }
    }
}
