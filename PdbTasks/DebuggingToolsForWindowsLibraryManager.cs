using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Utilities;

namespace PdbTasks
{
    public class DebuggingToolsForWindowsLibraryManager
    {
        private static readonly string debugToolsPath;

        private readonly TaskLoggingHelper logger;

        static DebuggingToolsForWindowsLibraryManager()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (programFiles.EndsWith("(x86)") && Environment.Is64BitOperatingSystem)
            {
                programFiles = programFiles.Substring(0, programFiles.Length - 6);
            }
            
            debugToolsPath = Path.Combine(programFiles, Environment.Is64BitOperatingSystem ? "Debugging Tools for Windows (x64)" : "Debugging Tools for Windows");
        }

        public DebuggingToolsForWindowsLibraryManager(TaskLoggingHelper logger)
        {
            this.logger = logger;
        }

        public Process PrepareToRunTool(string toolRelativePath, string arguments)
        {
            var toolFullPath = Path.Combine(debugToolsPath, toolRelativePath);
            if (!File.Exists(toolFullPath))
            {
                logger.LogError("Error: Unable to find '{0}'. Debugging Tools for Windows might not be installed", toolFullPath);
            }

            return new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = toolFullPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
        }
    }
}
