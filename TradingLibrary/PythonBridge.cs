using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TradingLibrary
{
    public class PythonBridge
    {
        public string PythonPath = null;

        private string processError;

        public PythonBridge(string pythonPath) {
            PythonPath = pythonPath;
        }

        private Process getPythonProcess(string executeName)
        {
            //Start a process to launch Python to read bar data and build other timeframes
            //Much faster with python for building new timeframes
            //Using cmd.exe here so can run on Windows 10 IoT OS
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = PythonPath;
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(PythonPath, executeName)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,

            };

            //read errors async
            processError = "";
            p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                processError += e.Data;
            });

            //Start the process
            p.Start();

            //Read errors async (can't have both sync operations)
            p.BeginErrorReadLine();

            return p;
        }

        public string[] RunScript(string path, string[] commands)
        {
            Process p = getPythonProcess(path);

            foreach (string command in commands)
                p.StandardInput.WriteLine(command);

            List<string> output = new List<string>();
            while (!p.StandardOutput.EndOfStream)
                output.Add(p.StandardOutput.ReadLine());

            p.WaitForExit();

            if (processError != "")
                throw new Exception(processError);

            return output.ToArray();

        }
    }
}
