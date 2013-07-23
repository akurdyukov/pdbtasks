using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace PdbTasks
{
    public class PdbUploadToSymbolServer : Task
    {
        private readonly DebuggingToolsForWindows _debuggingToolsForWindows;

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

        public PdbUploadToSymbolServer()
        {
            _debuggingToolsForWindows = new DebuggingToolsForWindows(Log);
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
                
            const string symStoreTool = "symstore.exe";
            string arguments = String.Format("add /f \"{0}\\*.*\" /s \"{1}\" /t \"{2}\" /v \"{3}\" /c \"{4}\"", tempDir, SymbolServer, ProductName, Version, Comment);
            var symstore = _debuggingToolsForWindows.PrepareToRunTool(symStoreTool,
                arguments);

            symstore.Start();
            symstore.WaitForExit();

            bool result = true;
            if (symstore.ExitCode != 0)
            {
                string errorData = symstore.StandardError.ReadToEnd();
                string outputData = symstore.StandardOutput.ReadToEnd();
                Log.LogError("Error: Unable to store PDB files ({0} exit code is {1}):\r\n{2}\r\n{3}\r\nArguments were: {4}", symStoreTool, symstore.ExitCode, errorData, outputData, arguments);
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
