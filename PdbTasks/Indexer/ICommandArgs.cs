namespace PdbTasks.Indexer
{
    public interface ICommandArgs
    {
        string this[string key] { get; set; }
    }
}