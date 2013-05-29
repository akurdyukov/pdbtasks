using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PdbTasks.Indexer
{
    // TODO: make SVN path settable
	public class Subversion : ISourceIndexer
	{
        private string _username;
        private string _password;
        private bool _useCredentials;
        private string _commandCreds;
        private IIndexerHost _host;

		public string Name
		{
			get { return "Subversion"; }
		}

		public IIndexerHost Host
		{
			set { _host = value; }
		}

		public void SetCredentials(string userName, string password)
		{
		    if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
		        return;

			_useCredentials = true;
			_username = userName;
			_password = password;

			_commandCreds = String.Format(" --username {0} --password {1}", _username, _password);
		}

		public string GetExtractCommand()
		{
			//The extract command must copy the desired file to the file path stored in the %SRCSRVTRG% variable
			string command;
			if (_useCredentials)
				command = String.Format("svn cat \"%Url%@%Revision%\" --non-interactive{0} > %SRCSRVTRG%",
                    _commandCreds);
			else
				command = "svn cat \"%Url%@%Revision%\" --non-interactive > \"%SRCSRVTRG%\"";
			
			return command;
		}

        private IEnumerable<string> RunSubversion(string args)
        {
            var svn = new Process
            {
                StartInfo =
                {
                    FileName = "svn.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = args
                }
            };
            svn.Start();

            IList<string> result = new List<string>();
            while (!svn.StandardOutput.EndOfStream)
            {
                string line = svn.StandardOutput.ReadLine();
                result.Add(line);
            }
            svn.WaitForExit();
            svn.Close();
            // TODO: check result
            return result;
        }

        private void IndexExternals(string folderPath)
        {
            // Check for any folders that are marked external, as we have to check them individually
            var externals = RunSubversion(String.Format("propget svn:externals \"{0}\" --recursive{1}", folderPath, _commandCreds));
            foreach (string external in externals)
            {
                int index = external.LastIndexOf(" - ", StringComparison.InvariantCulture);
                if (index != -1)
                {
                    var line = external.Remove(index, 3);
                    line = line.Insert(index, "\\");
                    index = line.LastIndexOf(' ');
                    line = line.Substring(0, index);

                    // Line should now contain the path to the folder that is external
                    IndexFolder(line);
                }
            }
        }

		public void IndexFolder(string folderPath)
		{
            IndexExternals(folderPath);

			// Start the process to list out info on all of the files in this folder
		    var infos = RunSubversion(String.Format("info \"{0}\" --recursive{1}", folderPath, _commandCreds));
		    var tempInfoList = ParseInfos(infos, folderPath);

			//Start up another process to find out the status of the files
			//because svn info doesn't tell us that
		    var statuses = RunSubversion(String.Format("status \"{0}\"{1}", folderPath, _commandCreds));
		    foreach (string statusLine in statuses)
		    {
                char status = statusLine[0];
                string filePath = statusLine.Substring(7);

                if (status == 'M')
                {
                    for (int i = tempInfoList.Count - 1; i >= 0; i--)
                    {
                        if (tempInfoList[i].FullPath.Equals(filePath))
                            tempInfoList[i].Status = FileStatus.Modified;
                    }
                }
		    }

			foreach (VersionInfo info in tempInfoList)
			{
				ICommandArgs commandArgs = _host.CreateCommandArgs();
				commandArgs["FilePath"] = info.FullPath;
				commandArgs["Revision"] = info.Revision;
				commandArgs["Url"] = info.Url;
				_host.AddFile(info.FullPath, info.Status, commandArgs);
			}
		}

	    private IList<VersionInfo> ParseInfos(IEnumerable<string> infos, string rootPath)
	    {
            IList<VersionInfo> result = new List<VersionInfo>();

            IEnumerator<string> enumerator = infos.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VersionInfo info = ExtractInfo(enumerator, rootPath);
                if (info == null)
                    continue;
                info.Status = FileStatus.CheckedIn;
                result.Add(info);
            }
	        return result;
	    }

	    public FileStatus GetFileInfo(string filePath, ref ICommandArgs argsOut)
		{
			FileStatus status = GetStatus(filePath);
			if (status != FileStatus.Unversioned)
			{
                var infos = RunSubversion(String.Format("info \"{0}\"{1}", filePath, _commandCreds));
                var tempInfoList = ParseInfos(infos, Path.GetFullPath(filePath));

                if (tempInfoList.Count == 1)
                {
                    var info = tempInfoList[0];

                    argsOut["FilePath"] = filePath;
                    argsOut["Revision"] = info.Revision;
                    argsOut["Url"] = info.Url;
                }
			}

			return status;
		}

		public string[] GetCommandArgs()
		{
			//'Filepath' and 'Revision' are already included, as they are
			//always necessary. Names are case sensitive.
			string[] myArgs = {"Url"};
			return myArgs;
		}

		private VersionInfo ExtractInfo(IEnumerator<string> lineEnumerator, string rootPath)
		{
		    byte setFields = 0x00;
			bool fileType = false;
		    var info = new VersionInfo();

		    do
		    {
		        string line = lineEnumerator.Current;

                //Blank line indicating the end of this segment
                //So we break out and return the data we have found
                if (string.IsNullOrEmpty(line))
                    break;

                KeyValuePair<string, string> kvp = GetKeyValue(line);

                if (kvp.Key.Equals("PATH"))
                {
                    info.FullPath = Path.IsPathRooted(kvp.Value) ? kvp.Value : Path.Combine(rootPath, kvp.Value);
                    info.FullPath = Path.GetFullPath(info.FullPath);
                    setFields |= 0x01;
                }
                else if (kvp.Key.Equals("URL"))
                {
                    info.Url = kvp.Value;
                    setFields |= 0x06;
                }
                else if (kvp.Key.Equals("REVISION"))
                {
                    info.Revision = kvp.Value;
                    setFields |= 0x08;
                }
                else if (kvp.Key.Equals("NODE KIND"))
                {
                    if (kvp.Value.Equals("File", StringComparison.CurrentCultureIgnoreCase))
                        fileType = true;
                }
            } while (lineEnumerator.MoveNext());

		    return (setFields == 0x0F && fileType) ? info : null;
        }

		private FileStatus GetStatus(string filePath)
		{
			var status = FileStatus.Unversioned;

		    var lines = RunSubversion(String.Format("status \"{0}\" -v{1}", filePath, _commandCreds));
		    foreach (string line in lines)
		    {
		        // only one line expected
				switch (line[0])
				{
					case ' ':
						status = FileStatus.CheckedIn;
						break;

					case '?':
						status = FileStatus.Unversioned;
						break;

					default:
						status = FileStatus.Modified;
						break;
				}
		        break;
		    }

			return status;
		}

		private KeyValuePair<string, string> GetKeyValue(string line)
		{
			string key = "";
			string value = "";
			int index = line.IndexOf(':');
			if (index != -1 && line.Length > index + 2)
			{
				key = line.Substring(0, index).ToUpper();
				value = line.Substring(index + 2);
			}

			return new KeyValuePair<string, string>(key, value);
		}

		class VersionInfo
		{
			public string FullPath;
			public string Url;
			public string Revision;
			public FileStatus Status;

			public VersionInfo()
			{
				FullPath = "";
				Url = "";
				Revision = "";
				Status = FileStatus.CheckedIn;
			}
		}
	}
}
