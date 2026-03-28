using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SmartLoopDetectorTests
{
    [TestClass]
    public class SmartLoopDetectorTests
    {
        [TestMethod]
        public void TestExactLoopDetection()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = new List<string> { "A", "B", "C", "A" };

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsTrue(result.Contains("A"));
        }

        [TestMethod]
        public void TestPatternBasedDetection_WithNumberVariations()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = new List<string> { "Pattern1", "Pattern2", "Pattern1_123", "Pattern2_456" };

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsTrue(result.Contains("Pattern1"));
        }

        [TestMethod]
        public void TestEmailTimestampVariations()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = new List<string> { "test@example.com", "test+1@example.com", "test@example.com", "2023-03-28T11:09:17Z" };

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsTrue(result.Contains("test@example.com"));
        }

        [TestMethod]
        public void TestMultipleCycles()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = new List<string> { "X", "Y", "X", "Z", "Y" };

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsTrue(result.Contains("X") && result.Contains("Y"));
        }

        [TestMethod]
        public void TestHighSpeedOutputDetection()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = Enumerable.Range(1, 1000).Select(i => i.ToString()).ToList();
            input.Add("1");  // Introduce a loop

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsTrue(result.Contains("1"));
        }

        [TestMethod]
        public void TestNormalOperationWithoutLoops()
        {
            // Arrange
            var detector = new SmartLoopDetector();
            var input = new List<string> { "M", "N", "O", "P" };

            // Act
            var result = detector.DetectLoops(input);

            // Assert
            Assert.IsFalse(result.Any());
        }
    }
}