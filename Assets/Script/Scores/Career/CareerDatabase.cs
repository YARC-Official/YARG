using System;
using System.Collections.Generic;
using SQLite;

namespace YARG.Scores
{
    public class CareerDatabase
    {
        private readonly SQLiteConnection _db;

        public CareerDatabase(SQLiteConnection db)
        {
            _db = db;
        }

        public void Initialize()
        {
            _db.CreateTable<CareerSaves>();
            _db.CreateTable<CareerSaveProfiles>();
            _db.CreateTable<CareerSongCompletions>();
            _db.CreateTable<CareerSongCompletionScores>();
            _db.CreateTable<CareerTierProgress>();
        }

        public List<CareerSongCompletions> GetSongCompletions(int careerSaveId)
        {
            return _db.Query<CareerSongCompletions>(@"SELECT * FROM CareerSongCompletions
                                                    WHERE CareerSaveId = ?",
                                                    careerSaveId);
        }

        public List<CareerTierProgress> GetTierProgress(int careerSaveId)
        {
            return _db.Query<CareerTierProgress>(@"SELECT * FROM CareerTierProgress
                                                    WHERE CareerSaveId = ?",
                                                    careerSaveId);
        }
    }
}