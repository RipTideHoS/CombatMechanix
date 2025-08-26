using CombatMechanix.Models;
using CombatMechanix.Services;
using Microsoft.Extensions.Logging;
using System;

namespace CombatMechanix.Services
{
    /// <summary>
    /// Simple demonstration/testing methods for AttackTimingService
    /// This can be called from diagnostic endpoints to verify functionality
    /// </summary>
    public static class AttackTimingServiceTests
    {
        /// <summary>
        /// Run basic tests of the AttackTimingService functionality
        /// </summary>
        public static string RunBasicTests(IAttackTimingService attackTimingService)
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("=== AttackTimingService Basic Tests ===");
            
            try
            {
                // Test 1: First attack (should always be allowed)
                var playerState = new PlayerState
                {
                    PlayerId = "test_player_001",
                    PlayerName = "Test Player",
                    EquipmentAttackSpeed = 2.0m, // 2 attacks per second = 500ms cooldown
                    LastAttackTime = DateTime.MinValue // Never attacked
                };

                var result1 = attackTimingService.ValidateAttackTiming(playerState);
                results.AppendLine($"Test 1 - First Attack: {(result1.IsValid ? "PASS" : "FAIL")} - {result1.Message}");

                // Test 2: Calculate cooldown
                var cooldown = attackTimingService.CalculateAttackCooldown(2.0m);
                var expectedCooldownMs = 500;
                var actualCooldownMs = (int)cooldown.TotalMilliseconds;
                results.AppendLine($"Test 2 - Cooldown Calculation: {(actualCooldownMs == expectedCooldownMs ? "PASS" : "FAIL")} - Expected: {expectedCooldownMs}ms, Actual: {actualCooldownMs}ms");

                // Test 3: Record attack and immediate retry (should fail)
                attackTimingService.RecordAttack(playerState, DateTime.UtcNow);
                var result3 = attackTimingService.ValidateAttackTiming(playerState);
                results.AppendLine($"Test 3 - Immediate Retry: {(!result3.IsValid ? "PASS" : "FAIL")} - {result3.Message}");

                // Test 4: Attack after sufficient cooldown (simulate time passing)
                var futureTime = DateTime.UtcNow.AddMilliseconds(600); // 600ms > 500ms cooldown
                var result4 = attackTimingService.ValidateAttackTiming(playerState, futureTime);
                results.AppendLine($"Test 4 - After Cooldown: {(result4.IsValid ? "PASS" : "FAIL")} - {result4.Message}");

                // Test 5: Different attack speeds
                playerState.EquipmentAttackSpeed = 0.5m; // 0.5 attacks per second = 2000ms cooldown
                var cooldown5 = attackTimingService.CalculateAttackCooldown(0.5m);
                var expectedCooldown5Ms = 2000;
                var actualCooldown5Ms = (int)cooldown5.TotalMilliseconds;
                results.AppendLine($"Test 5 - Slow Weapon: {(actualCooldown5Ms == expectedCooldown5Ms ? "PASS" : "FAIL")} - Expected: {expectedCooldown5Ms}ms, Actual: {actualCooldown5Ms}ms");

                // Test 6: Very fast weapon
                playerState.EquipmentAttackSpeed = 4.0m; // 4 attacks per second = 250ms cooldown
                var cooldown6 = attackTimingService.CalculateAttackCooldown(4.0m);
                var expectedCooldown6Ms = 250;
                var actualCooldown6Ms = (int)cooldown6.TotalMilliseconds;
                results.AppendLine($"Test 6 - Fast Weapon: {(actualCooldown6Ms == expectedCooldown6Ms ? "PASS" : "FAIL")} - Expected: {expectedCooldown6Ms}ms, Actual: {actualCooldown6Ms}ms");

                // Test 7: Next attack time calculation
                playerState.EquipmentAttackSpeed = 1.0m; // 1 attack per second = 1000ms cooldown
                attackTimingService.RecordAttack(playerState, DateTime.UtcNow);
                var nextAttackTime = attackTimingService.CalculateNextAttackTime(playerState, DateTime.UtcNow);
                var timeDiff = (nextAttackTime - DateTime.UtcNow).TotalMilliseconds;
                var isCorrectTiming = timeDiff >= 950 && timeDiff <= 1050; // Allow 50ms tolerance
                results.AppendLine($"Test 7 - Next Attack Time: {(isCorrectTiming ? "PASS" : "FAIL")} - Time diff: {timeDiff:F0}ms");

                results.AppendLine("=== All Tests Completed ===");
                
                return results.ToString();
            }
            catch (Exception ex)
            {
                results.AppendLine($"ERROR: Test execution failed - {ex.Message}");
                return results.ToString();
            }
        }

        /// <summary>
        /// Performance test for attack timing validation
        /// </summary>
        public static string RunPerformanceTest(IAttackTimingService attackTimingService, int iterations = 10000)
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine($"=== AttackTimingService Performance Test ({iterations:N0} iterations) ===");
            
            try
            {
                var playerState = new PlayerState
                {
                    PlayerId = "perf_test_player",
                    PlayerName = "Performance Test Player", 
                    EquipmentAttackSpeed = 2.5m,
                    LastAttackTime = DateTime.UtcNow.AddMilliseconds(-600) // Already past cooldown
                };

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                for (int i = 0; i < iterations; i++)
                {
                    var result = attackTimingService.ValidateAttackTiming(playerState);
                    // Vary the scenario occasionally
                    if (i % 100 == 0)
                    {
                        attackTimingService.RecordAttack(playerState);
                        playerState.LastAttackTime = DateTime.UtcNow.AddMilliseconds(-200); // Reset to simulate failed timing
                    }
                }
                
                stopwatch.Stop();
                
                var totalMs = stopwatch.ElapsedMilliseconds;
                var avgMicroseconds = (stopwatch.ElapsedTicks * 1000000.0) / (System.Diagnostics.Stopwatch.Frequency * iterations);
                
                results.AppendLine($"Total time: {totalMs}ms");
                results.AppendLine($"Average per validation: {avgMicroseconds:F2}μs");
                results.AppendLine($"Validations per second: {(iterations * 1000.0 / totalMs):N0}");
                
                var isPerformant = avgMicroseconds < 100; // Should be under 100 microseconds per call
                results.AppendLine($"Performance: {(isPerformant ? "PASS" : "FAIL")} - {(isPerformant ? "Excellent" : "Needs optimization")}");
                
                results.AppendLine("=== Performance Test Completed ===");
                
                return results.ToString();
            }
            catch (Exception ex)
            {
                results.AppendLine($"ERROR: Performance test failed - {ex.Message}");
                return results.ToString();
            }
        }
    }
}