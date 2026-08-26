using System;
using System.Collections.Generic;
using YARG.Scores;

namespace YARG.Career
{
    public class CareerProgress
    {
        public int                                  CareerSaveId;
        public Dictionary<Guid, CareerTierProgress> TierProgress;
    }
}