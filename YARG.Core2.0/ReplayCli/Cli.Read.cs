namespace ReplayCli;

public partial class Cli
{
    public bool RunRead()
    {
        PrintReplayMetadata();
        return true;
    }
}