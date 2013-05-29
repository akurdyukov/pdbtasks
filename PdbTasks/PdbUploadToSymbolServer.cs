using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace PdbTasks
{
    public class PdbUploadToSymbolServer : Task
    {
        /// <summary>
        /// Files to upload to symserver
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string SymbolServer { get; set; }

        public string ProductName { get; set; }
        public string Version { get; set; }
        public string Comment { get; set; }

        /// <summary>
        /// Path to 'Debugging tools'
        /// </summary>
        public string DebugToolsPath { get; set; }

        public PdbUploadToSymbolServer()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (programFiles.EndsWith("(x86)") && Environment.Is64BitOperatingSystem)
            {
                programFiles = programFiles.Substring(0, programFiles.Length - 6);
            }
            DebugToolsPath = Path.Combine(programFiles,
                Environment.Is64BitOperatingSystem ? "Debugging Tools for Windows (x64)" : "Debugging Tools for Windows");
        }

        public override bool Execute()
        {
            // create new temp dir
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("B"));
            Directory.CreateDirectory(tempDir);

            // copy all upload to temp dir
            foreach (var task in Files)
            {
                if (File.Exists(task.ItemSpec))
                {
                    string fileName = Path.GetFileName(task.ItemSpec);
                    if (fileName == null)
                    {
                        Log.LogMessage("Skipped unknown file {0}", task.ItemSpec);
                        continue;
                    }
                    File.Copy(task.ItemSpec, Path.Combine(tempDir, fileName));
                }
            }

            var symstore = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = Path.Combine(DebugToolsPath, "symstore.exe"),
                    Arguments = String.Format("add /f \"{0}\\*.*\" /s \"{1}\" /t \"{2}\" /v \"{3}\" /c \"{4}\"",
                                              tempDir, SymbolServer, ProductName, Version, Comment)
                }
            };

            symstore.Start();
            symstore.WaitForExit();

            bool result = true;
            if (symstore.ExitCode != 0)
            {
                string errorData = symstore.StandardError.ReadToEnd();
                Log.LogError("Error: Unable to store PDB files\r\n{0}\r\n", errorData);
                result = false;
            }
            else
            {
                Log.LogMessage("PDB files successfully stored\r\n");
            }

            Directory.Delete(tempDir, true);
            return result;
        }
    }
}
