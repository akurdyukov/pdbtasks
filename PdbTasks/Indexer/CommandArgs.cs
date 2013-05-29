using System.Collections.Generic;
using System.Text;

namespace PdbTasks.Indexer
{
	public class CommandArgs : ICommandArgs
	{
        private readonly Dictionary<string, string> _dict;

        public CommandArgs(IEnumerable<string> allowedArgs)
		{
			_dict = new Dictionary<string, string>();
			foreach (string arg in allowedArgs)
			{
				if(!_dict.ContainsKey(arg))
					_dict.Add(arg, "_");
			}
		}

		public string this[string key]
		{
			get
			{
			    if (_dict.ContainsKey(key))
					return _dict[key];
			    throw new KeyNotFoundException();
			}

		    set
			{
				if (_dict.ContainsKey(key))
					_dict[key] = value;
				else
					throw new KeyNotFoundException();
			}
        }

	    public override string ToString()
	    {
            var sb = new StringBuilder("<");
	        foreach (KeyValuePair<string, string> pair in _dict)
	        {
	            sb.Append(pair.Key).Append("=").Append(pair.Value).Append(";");
	        }
	        sb.Append(">");
	        return sb.ToString();
	    }
	}
}
