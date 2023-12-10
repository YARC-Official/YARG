namespace YARG.Scores
{
    // Currently, this is unused

    public static class ScoreDatabaseQueries
    {
        private const string PLAYER_GAME_HISTORY = "player_game_history";
        private const string GAME_HISTORY_TABLE  = "game_history";

        private const string PLAYERS_TABLE = "players";

        private const string CREATE_PROFILES_TABLE =
            "CREATE TABLE IF NOT EXISTS " + PLAYERS_TABLE + @" (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL
            )";

        private const string CREATE_GAME_HISTORY_TABLE =
            "CREATE TABLE IF NOT EXISTS " + GAME_HISTORY_TABLE + @" (
                id INTEGER PRIMARY KEY NOT NULL,
                date INTEGER NOT NULL,
                song_checksum BLOB NOT NULL,
                song_name TEXT NOT NULL,
                song_artist TEXT NOT NULL,
                song_charter TEXT NOT NULL,
                song_speed REAL NOT NULL,
                band_score INTEGER NOT NULL,
                band_stars INTEGER NOT NULL,
                replay_file_name TEXT NOT NULL,
                replay_checksum TEXT NOT NULL
            )";

        private const string CREATE_PLAYER_GAME_HISTORY_TABLE =
            "CREATE TABLE IF NOT EXISTS " + PLAYER_GAME_HISTORY + @" (
                id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                profile_id TEXT NOT NULL,
                game_id INTEGER NOT NULL,
                instrument INTEGER NOT NULL,
                difficulty INTEGER NOT NULL,
                score INTEGER NOT NULL,
                stars INTEGER NOT NULL,
                notes_hit INTEGER NOT NULL,
                notes_missed INTEGER NOT NULL,
                is_fc INTEGER NOT NULL,
                date INTEGER NOT NULL,
                FOREIGN KEY (profile_id) REFERENCES " + PLAYERS_TABLE + @" (id),
                FOREIGN KEY (game_id) REFERENCES " + GAME_HISTORY_TABLE + @" (id)
            )";

        public const string CREATE_TABLES = CREATE_PROFILES_TABLE + ";" + CREATE_GAME_HISTORY_TABLE + ";" +
            CREATE_PLAYER_GAME_HISTORY_TABLE;
    }
}