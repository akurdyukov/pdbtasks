namespace PdbTasks.Indexer
{
    public interface ISourceIndexer
    {
        IIndexerHost Host { set; }
        string Name { get; }

        void IndexFolder(string folderPath);
        FileStatus GetFileInfo(string filePath, ref ICommandArgs argsOut);

        /// <summary>
        /// Set username and password to user in source indexer. Affects following IndexFolder request.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        void SetCredentials(string userName, string password);

        string[] GetCommandArgs();

        /// <summary>
        /// Returns command to extract source. Command itself should use %Url%, %Revision% and %SRCSRVTRG% vars
        /// </summary>
        /// <returns></returns>
        string GetExtractCommand();
    }
}