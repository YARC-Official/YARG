using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using YARG.Data;

// Score keeping for each track
public class ScoreKeeper: MonoBehaviour {
    // keep track of all instances to calculate the band total
	public static List<ScoreKeeper> instances = new();
    public static double TotalScore {
        get {
			double sum = 0;
            foreach (var ins in instances) {
				sum += ins.score;
			}
			return sum;
		}
    }

	public static void Reset() {
		Debug.Log("Clearing ScoreKeeper instances!");
		instances.Clear();
	}

	public double score { get; private set; } = 0;

	public void Add(double points) {
        score += points;
	}

    public ScoreKeeper() {
		Debug.Log("Creating a ScoreKeeper instance!");
		instances.Add(this);
	}
}
