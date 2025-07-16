namespace QuickMigrate.Models
{

    public enum RestoreStrategy
    {
        OverwriteExisting,  
        SkipExisting,       
        MergeSettings,      
        BackupAndReplace,   
        AskUser 
    }
}
