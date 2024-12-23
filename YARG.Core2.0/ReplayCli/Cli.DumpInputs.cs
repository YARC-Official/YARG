namespace ReplayCli;

public partial class Cli
{
    public bool RunDumpInputs()
    {
        foreach (var frame in _replayData.Frames)
        {
            Console.WriteLine("Begin Frame Dump");
            for (int i = 0; i < frame.Inputs.Length; i++)
            {
                var input = frame.Inputs[i];
                Console.WriteLine($"{i}: {input.Time}, {input.Action}, {input.Integer}");
            }
        }
        return true;
    }
}