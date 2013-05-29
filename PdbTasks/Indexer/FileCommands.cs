using System.Collections.Generic;

namespace PdbTasks.Indexer
{
    /// <summary>
    /// Map file name -> command arguments
    /// </summary>
	class FileCommands
	{
        private readonly Dictionary<string, ICommandArgs> _dict;

        public FileCommands()
		{
			_dict = new Dictionary<string, ICommandArgs>();
		}

		public bool ContainsFile(string filePath)
		{
			return _dict.ContainsKey(filePath);
		}

		public void Add(string filePath, ICommandArgs args)
		{
			if (!_dict.ContainsKey(filePath))
				_dict.Add(filePath, args);
		}

		public bool TryGetCommandArgs(string file, out ICommandArgs args)
		{
			bool result = false;
			args = null;
			if(_dict.ContainsKey(file))
			{
				result = true;
				args = _dict[file];
			}
			return result;
		}

		public Dictionary<string, ICommandArgs> GetFileCommands()
		{
			return _dict;
		}
	}
}
