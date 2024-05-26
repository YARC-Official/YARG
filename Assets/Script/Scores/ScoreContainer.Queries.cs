namespace YARG.Scores
{
    public static partial class ScoreContainer
    {
        private const string QUERY_SONGS =
            "SELECT Id, SongChecksum FROM GameRecords";

        private const string QUERY_HIGH_SCORES = @"
            SELECT *, MAX(Score) FROM PlayerScores
            INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id
            GROUP BY GameRecords.SongChecksum";

        private const string QUERY_UPDATE_NULL_PERCENTS = @"
            UPDATE PlayerScores
            SET Percent = cast(NotesHit as REAL) / (NotesHit + NotesMissed)
            WHERE Percent IS NULL";

        private const string QUERY_BEST_SCORES_BY_PERCENT = @"
        WITH
        /*  For each song, this retrieves the records with the best score  */
            BestScore as (SELECT *, max(Score)
                            FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id
                            GROUP BY GameRecords.SongChecksum),
        /*  For each song, instrument, and difficulty, this retrieves the records with the best score by percent  */
            BestPercents as (SELECT *, max(Percent)
                            FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id
                            GROUP BY GameRecords.SongChecksum, PlayerScores.Instrument, PlayerScores.Difficulty)
        /*
            Filter the lines from the BestPercents temporary table above where the instrument and difficulty match
            the instrument and difficulty when the highest score was recorded
        */
        SELECT BestPercents.*
        FROM BestPercents INNER JOIN BestScore ON BestScore.Instrument = BestPercents.Instrument
            AND BestScore.Difficulty = BestPercents.Difficulty
            AND BestScore.SongChecksum = BestPercents.SongChecksum";
    }
}