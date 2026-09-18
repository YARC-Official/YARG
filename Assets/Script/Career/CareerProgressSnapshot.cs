using System.Collections.Generic;
using YARG.Scores;

namespace YARG.Career
{
    // Raw progress rows for a single career save, loaded in one go so progress can be
    // evaluated above the database layer (see CareerEvaluation).
    public class CareerProgressSnapshot
    {
        public int CareerSaveId;

        public List<CareerSongCompletions> Completions = new();
        public List<CareerSongCompletionScores> CompletionScores = new();
        public List<CareerTierProgress> TierProgress = new();
    }
}
