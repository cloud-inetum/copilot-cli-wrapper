using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LoopDetection {
    public class SmartLoopDetector {
        private readonly List<string> historyBuffer;
        private readonly int bufferSize;
        private double confidenceScore;

        public SmartLoopDetector(int size) {
            bufferSize = size;
            historyBuffer = new List<string>(bufferSize);
            confidenceScore = 0.0;
        }

        public void AddToHistory(string input) {
            if (historyBuffer.Count >= bufferSize) {
                historyBuffer.RemoveAt(0);
            }
            historyBuffer.Add(input);
        }

        public bool DetectLoop(string pattern) {
            string combinedHistory = string.Join("|", historyBuffer);
            MatchCollection matches = Regex.Matches(combinedHistory, pattern);
            if (matches.Count > 0) {
                confidenceScore = CalculateConfidenceScore(matches.Count);
                return true;
            }
            return false;
        }

        private double CalculateConfidenceScore(int matchCount) {
            confidenceScore = (double)matchCount / bufferSize;
            return confidenceScore;
        }

        public double GetConfidenceScore() {
            return confidenceScore;
        }
    }
}