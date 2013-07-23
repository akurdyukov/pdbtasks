using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Build.Utilities;

namespace PdbTasks.Indexer
{
    class GenericIndexerHost : IIndexerHost
    {
        private readonly ISourceIndexer _indexer;
        private readonly TaskLoggingHelper _logger;
        private readonly FileCommands _fileCommands;
        private readonly List<string> _allowedArgs;
        private readonly List<LocalFile> _localFiles;
        private readonly DebuggingToolsForWindows _debuggingToolsForWindows;

        private readonly object _sync = new object();

        public bool UseLocalBackup { get; set; }
        public string BackupLocation { get; set; }
        public string SolutionDirectory { get; set; }

        public GenericIndexerHost(ISourceIndexer indexer, TaskLoggingHelper logger)
        {
            _indexer = indexer;
            _logger = logger;
            _allowedArgs = new List<string> { "FilePath", "Revision", "CopyLocal", "CachePath" };
            _allowedArgs.AddRange(_indexer.GetCommandArgs());

            _fileCommands = new FileCommands();
            _localFiles = new List<LocalFile>();
            indexer.Host = this;

            UseLocalBackup = false;

            _debuggingToolsForWindows = new DebuggingToolsForWindows(logger);
        }

        public void IndexPdbFile(string sourcePath, string pdbPath)
        {
            IndexWorkingCopy(sourcePath);
            UpdatePDB(pdbPath);
        }

        /// <summary>
        /// Traverses a directory structure and get the indexer to
        /// gather information on each version file in each directory
        /// </summary>
        /// <param name="path">Path to directory</param>
        private void IndexWorkingCopy(string path)
        {
            if (Directory.Exists(path))
            {
                _logger.LogMessage("Indexing working copy \"{0}\"\r\n", path);
                _indexer.IndexFolder(path);
            }

            SetCachePath();
        }

        public void AddFile(string filePath, FileStatus status, ICommandArgs args)
        {
            lock (_sync)
            {
                if (!_fileCommands.ContainsFile(filePath))
                {
                    if (status != FileStatus.CheckedIn && UseLocalBackup)
                    {
                        //Store a reference to this file so we can copy it later if necessary
                        var fi = new FileInfo(filePath);
                        var lf = new LocalFile
                            {
                                Timestamp = (int)(fi.LastWriteTimeUtc - new DateTime(1970, 1, 1)).TotalSeconds,
                                FilePath = filePath
                            };
                        _localFiles.Add(lf);

                        //Fill arguments for local backup
                        args["CopyLocal"] = "TRUE";
                        args["FilePath"] = filePath;
                        args["Revision"] = lf.Timestamp.ToString(CultureInfo.InvariantCulture);
                    }
                    _fileCommands.Add(filePath, args);

                    _logger.LogMessage("Adding file {0}", filePath);
                }
            }
        }

        private void WriteHeader(StreamWriter sr)
        {
            sr.WriteLine("SRCSRV: ini ------------------------------------------------");
            sr.WriteLine("VERSION=1");
            sr.WriteLine("INDEXVERSION=2");
            sr.WriteLine("VERCTRL={0}", _indexer.Name);
            sr.WriteLine("DATETIME={0}", DateTime.Now.ToString("ddd, dd MMMM yyyy HH:mm", CultureInfo.InvariantCulture));
            sr.WriteLine("SRCSRV: variables ------------------------------------------");

            // Write variables
            int i = 1;
            foreach (string arg in _allowedArgs)
            {
                sr.WriteLine("{0}=%var{1}%", arg, i);
                i++;
            }

            //Build extract target and extract command
            sr.WriteLine("SRCSRVTRG=%targ%\\%CachePath%\\%Revision%\\%fnfile%(%FilePath%)");
            string extractCmd = _indexer.GetExtractCommand();
            sr.WriteLine("SRCSRVCMD=cmd /c \"IF %CopyLocal%==TRUE (ECHO F|xcopy \"{0}\\%CachePath%\\%Revision%\\%fnfile%(%FilePath%)\" %SRCSRVTRG% /Q /Y /Z) ELSE ({1})\"", 
                BackupLocation, extractCmd);
        }

        private IEnumerable<string> GetPDBSourceNames(string pdbFile)
        {
            IList<string> result = new List<string>();

            var srctool = _debuggingToolsForWindows.PrepareToRunTool(Path.Combine("srcsrv", "srctool.exe"), String.Format("\"{0}\" -r", pdbFile));
            srctool.Start();
            while (!srctool.StandardOutput.EndOfStream)
            {
                string file = srctool.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(file))
                    continue;

                // remove leading full path

                result.Add(file);
            }
            srctool.WaitForExit(); // TODO: limit time wait here

            return result;
        }

        private bool WriteIndexingFile(string tempFile, IEnumerable<string> sources)
        {
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
            {
                using (StreamWriter sr = new StreamWriter(fs))
                {
                    bool success = true;
                    try
                    {
                        WriteHeader(sr);

                        //Index each file that we can in the pdb file
                        sr.WriteLine("SRCSRV: source files ---------------------------------------");

                        foreach (var file in sources)
                        {
                            ICommandArgs commandArgs;
                            if (_fileCommands.TryGetCommandArgs(file, out commandArgs))
                            {
                                //Backup the file if necessary
                                if (commandArgs["CopyLocal"].Equals("TRUE") && UseLocalBackup)
                                {
                                    MakeLocalBackup(file, commandArgs);
                                }

                                bool first = true;
                                foreach (string arg in _allowedArgs)
                                {
                                    if (!first)
                                        sr.Write("*");
                                    sr.Write(commandArgs[arg]);
                                    first = false;
                                }
                                sr.Write(Environment.NewLine);
                            }
                            else
                            {
                                _logger.LogWarning("Unknown file status for {0}", file);
                            }
                        }
                        sr.WriteLine("SRCSRV: end ------------------------------------------------");
                    }
                    catch (IOException e)
                    {
                        //That's not good
                        _logger.LogErrorFromException(e);
                        success = false;
                    }
                    finally
                    {
                        sr.Close();
                    }
                    return success;
                }
            }
        }

        private void UpdatePDB(string pdbFile)
        {
            if (!File.Exists(pdbFile))
            {
                _logger.LogError("\"{0}\" does not exist\r\n", pdbFile);
                return;
            }

            string extension = Path.GetExtension(pdbFile);
            if (string.IsNullOrEmpty(extension) || !extension.Equals(".pdb", StringComparison.CurrentCultureIgnoreCase))
            {
                _logger.LogError("\"{0}\" is not a symbol file, skipping.\r\n", pdbFile);
                return;
            }

            IEnumerable<string> sources = GetPDBSourceNames(pdbFile);

            _logger.LogMessage("Indexing PDB \"{0}\"\r\n", pdbFile);

            string tempFile = Path.GetTempFileName();
            bool success = WriteIndexingFile(tempFile, sources);

            if (success)
            {
                var pdbstr = _debuggingToolsForWindows.PrepareToRunTool(Path.Combine("srcsrv", "pdbstr.exe"),
                                          String.Format("-w -p:\"{0}\" -s:srcsrv -i:\"{1}\"", pdbFile, tempFile));
                pdbstr.Start();
                pdbstr.WaitForExit(); // TODO: check result code

                if (pdbstr.ExitCode != 0)
                {
                    string errorData = pdbstr.StandardError.ReadToEnd();
                    _logger.LogError("Error: Unable to index PDB file\r\n{0}\r\n", errorData);
                }
                else
                {
                    _logger.LogMessage("\"{0}\" successfully indexed!\r\n", pdbFile);
                }
            }
            else
            {
                _logger.LogError("Writing PDB \"{0}\" failed\r\n", pdbFile);
            }

            // remove temp files
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        public ICommandArgs CreateCommandArgs()
        {
            return new CommandArgs(_allowedArgs);
        }

        private void MakeLocalBackup(string file, ICommandArgs commandArgs)
        {
            if (!UseLocalBackup || string.IsNullOrEmpty(BackupLocation))
                return;

            if (commandArgs["CopyLocal"].Equals("TRUE"))
            {
                _logger.LogMessage("Creating backup of file {0}\r\n", file);

                var fi = new FileInfo(file);
                string destinationFolder = String.Format("{0}\\{1}\\{2}", BackupLocation, commandArgs["CachePath"], commandArgs["Revision"]);
                string destinationFile = String.Format("{0}\\{1}", destinationFolder, fi.Name);
                if (!File.Exists(destinationFile))
                {
                    if (!Directory.Exists(destinationFolder))
                        Directory.CreateDirectory(destinationFolder);
                    File.Copy(fi.FullName, destinationFile);
                }
            }
        }

        private void SetCachePath()
        {
            if (!UseLocalBackup)
                return;

            Dictionary<string, ICommandArgs> fcms = _fileCommands.GetFileCommands();
            foreach (KeyValuePair<string, ICommandArgs> kvp in fcms)
            {
                if (!kvp.Value["CachePath"].Equals("_")) continue;

                var fi = new FileInfo(kvp.Key);
                if (fi.Directory == null || fi.DirectoryName == null)
                    continue;

                if (fi.FullName.StartsWith(SolutionDirectory, true, CultureInfo.InvariantCulture))
                    kvp.Value["CachePath"] = fi.DirectoryName.Substring(SolutionDirectory.Length + 1);
                else
                {
                    string rootName = fi.Directory.Root.FullName.Replace(":\\", "_");
                    string folderPath = fi.DirectoryName.Substring(fi.Directory.Root.FullName.Length);
                    kvp.Value["CachePath"] = rootName + folderPath;
                }

                if (kvp.Value["CachePath"].Length == 0)
                {
                    kvp.Value["CachePath"] = "root";
                }
            }
        }

        struct LocalFile
        {
            public string FilePath;
            public int Timestamp;
        }
    }
}
