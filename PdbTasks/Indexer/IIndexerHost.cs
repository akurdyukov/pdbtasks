namespace PdbTasks.Indexer
{
    public interface IIndexerHost
    {
        void AddFile(string filePath, FileStatus status, ICommandArgs args);
        ICommandArgs CreateCommandArgs();
    }
}