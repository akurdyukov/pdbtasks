using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using PdbTasks.Indexer;

namespace PdbTasks
{
    public class PdbIndexFromSvn : Task
    {
        public string LocalSvnPath { get; set; }
        public string ClientSvnPath { get; set; }

        /// <summary>
        /// PDB to index
        /// </summary>
        [Required]
        public ITaskItem SourcePdb { get; set; }

        [Required]
        public string SourceDirectory { get; set; }

        /// <summary>
        /// Subversion user name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Subversion password
        /// </summary>
        public string Password { get; set; }

        public override bool Execute()
        {
            ISourceIndexer indexer = new Subversion();
            indexer.SetCredentials(UserName, Password);

            var host = new GenericIndexerHost(indexer, Log) {UseLocalBackup = false};

            // index all files in path
            host.IndexPdbFile(SourceDirectory, SourcePdb.ItemSpec);

            return true;
        }
    }
}
