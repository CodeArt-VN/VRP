using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System;

namespace SmartRouting.Services
{
    public static class RoutingStrategies
    {
        private class StrategyResult
        {
            public string Name { get; set; } = "";
            public double DurationMs { get; set; }
            public bool FoundSolution { get; set; }
        }

        public static Assignment? TryMultipleSolutionStrategies(RoutingModel routing, int numberOfSO, int numberOfVehicles)
        {
            Console.WriteLine("Thử các chiến lược giải khác nhau để tìm phương án tối ưu");

            // Store our strategies in order of preference
            var strategies = new List<(string Name, Func<RoutingSearchParameters> StrategyFunc)>
            {
                ("PathCheapestArc + Guided Local Search", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.PathCheapestArc,
                    LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch, numberOfSO, numberOfVehicles)),

                // ("PathCheapestArc + Simulated Annealing", () => CreateSearchParameters(
                // 	FirstSolutionStrategy.Types.Value.PathCheapestArc,
                // 	LocalSearchMetaheuristic.Types.Value.SimulatedAnnealing, numberOfSO, numberOfVehicles)),

                // ("Savings + Guided Local Search", () => CreateSearchParameters(
                // 	FirstSolutionStrategy.Types.Value.Savings,
                // 	LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch, numberOfSO, numberOfVehicles)),

                // ("Sweep + Tabu Search", () => CreateSearchParameters(
                // 	FirstSolutionStrategy.Types.Value.Sweep,
                // 	LocalSearchMetaheuristic.Types.Value.TabuSearch, numberOfSO, numberOfVehicles)),

                // // Additional strategy for very large instances
                // ("Automatic + Guided Local Search", () => CreateSearchParameters(
				// 	FirstSolutionStrategy.Types.Value.Automatic,
				// 	LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch, numberOfSO, numberOfVehicles)),

                // Last resort with relaxed constraints and extended time
                //("Extended PathCheapestArc", () => CreateExtendedSearchParameters())
            };

            Assignment? solution = null;
            var results = new List<StrategyResult>();

            // Try each strategy in turn until we find a solution
            foreach (var (name, strategyFunc) in strategies)
            {
                var sw = Stopwatch.StartNew();
                Console.WriteLine($"Thử chiến lược: {name}");

                var searchParameters = strategyFunc();
                solution = routing.SolveWithParameters(searchParameters);
                sw.Stop();

                var duration = sw.Elapsed.TotalMilliseconds;
                results.Add(new StrategyResult { Name = name, DurationMs = duration, FoundSolution = solution != null });

                if (solution != null)
                {
                    Console.WriteLine($"Tìm thấy giải pháp với chiến lược '{name}' trong {sw.Elapsed.TotalMilliseconds:F0} ms");
                }
                else
                {
                    Console.WriteLine($"Chiến lược '{name}' không tìm thấy giải pháp sau {sw.Elapsed.TotalMilliseconds:F0} ms");
                }
            }

            PrintDurationChart(results);
            return solution;
        }

        private static RoutingSearchParameters CreateSearchParameters(
            FirstSolutionStrategy.Types.Value firstSolutionStrategy,
            LocalSearchMetaheuristic.Types.Value localSearchMetaheuristic,
			int numberOfSO, int numberOfVehicles)
        {
            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();

            // Set first solution strategy
            searchParameters.FirstSolutionStrategy = firstSolutionStrategy;

            // Set local search metaheuristic
            searchParameters.LocalSearchMetaheuristic = localSearchMetaheuristic;


			// For large instances, limit per-operation time but allow more overall time
			// searchParameters.TimeLimit = new Duration { Seconds = 45 }; // Increased from 30s to 45s
			// searchParameters.LnsTimeLimit = new Duration { Seconds = 1 }; // Increased from 5s to 10s

			var seconds = 5 + 0.5 * numberOfSO + 0.2 * numberOfVehicles;
			searchParameters.TimeLimit    = new Duration { Seconds = (long)seconds };
			searchParameters.LnsTimeLimit = new Duration { Seconds = (long)(seconds / 4) };
			searchParameters.SolutionLimit = 1; // Limit to finding the first solution


            return searchParameters;
        }

        private static RoutingSearchParameters CreateExtendedSearchParameters()
        {
            // This is our fallback with longer timeouts and more exhaustive search
            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();

            // Use most reliable first solution strategy
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;

            // Use guided local search which is most effective for finding feasible solutions
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;

            // Extended time limits based on instance size
            searchParameters.TimeLimit = new Duration { Seconds = 90 }; // Increased from 60s to 90s for large instances
            searchParameters.LnsTimeLimit = new Duration { Seconds = 15 }; // Increased from 10s to 15s

            // More exhaustive parameter settings
            //searchParameters.LogSearch = true; // Enable search logging

            return searchParameters;
        }
       
        private static void PrintDurationChart(List<StrategyResult> results)
        {
            const int barMaxWidth = 50;
            if (results == null || results.Count == 0)
            {
                Console.WriteLine("No results to display.");
                return;
            }

            double maxMs = results.Max(r => r.DurationMs);
            Console.WriteLine("\n--- Thống kê thời gian chạy strategies (ms) ---");
            foreach (var r in results)
            {
                int barLen = (int)(r.DurationMs / maxMs * barMaxWidth);
                char barChar = r.FoundSolution ? '#' : '-';
                string bar = new string(barChar, barLen > 0 ? barLen : 1);
                Console.WriteLine($"{r.Name.PadRight(30).Substring(0,30)} |{bar.PadRight(barMaxWidth)}| {r.DurationMs:F0} ms");
            }
            Console.WriteLine(new string('-', barMaxWidth + 35));
        }
    }
}
