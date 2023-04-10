using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Score keeping for each track
public class ScoreKeeper
{
    // keep track of all instances to calculate the band total
	public static HashSet<ScoreKeeper> instances = new();
    public static double ScoreFromAll
    {
        get
        {
			double sum = 0;
            foreach (var ins in instances)
            {
				sum += ins.Score;
			}
			return sum;
		}
    }

	public double Score { get; private set; } = 0;

	public void AddScore(double points)
    {
        Score += points;
	}

    public ScoreKeeper()
    {
		instances.Add(this);
	}

    public void Destroy()
    {
        instances.Remove(this);
    }
}
