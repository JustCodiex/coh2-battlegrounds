﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Battlegrounds.Compiler {
    
    public static class Archiver {

        public static bool Archive(string archdef, string relativepath, string output) 
            => RunArchiver($" -c \"{archdef}\" -a \"{output}\" -v -r \"{relativepath}\\\"");

        public static bool Extract(string arcfile, string outpath)
            => RunArchiver($" -a \"{arcfile}\" -e \"{outpath}\" -v ");

        private static string GetArchiverFilepath() {
            string path = Pathfinder.GetOrFindCoHPath() + "Archive.exe";
            if (File.Exists(path)) {
                return path;
            } else {
                return string.Empty;
            }
        }

        private static bool RunArchiver(string args) {

            Process archiveProcess = new Process {
                StartInfo = new ProcessStartInfo() {
                    FileName = GetArchiverFilepath(),
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };

            archiveProcess.OutputDataReceived += ArchiveProcess_OutputDataReceived;

            try {

                if (!archiveProcess.Start()) {
                    archiveProcess.Dispose();
                    return false;
                } else {
                    archiveProcess.BeginOutputReadLine();
                }

                Thread.Sleep(1000);

                do {
                    Thread.Sleep(100);
                } while (!archiveProcess.HasExited);

                if (archiveProcess.ExitCode != 0) {
                    int eCode = archiveProcess.ExitCode;
                    Trace.WriteLine($"Archiver has finished with error code = {eCode}");
                    archiveProcess.Dispose();
                    return false;
                }

            } catch {
                archiveProcess.Dispose();
                return false;
            }

            archiveProcess.Dispose();

            return true;

        }

        private static void ArchiveProcess_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (e.Data != null && e.Data != string.Empty && e.Data != " ")
                Trace.WriteLine($"{e.Data}");
        }

    }

}