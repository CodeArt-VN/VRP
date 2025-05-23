using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace SmartRouting.Services
{
    public static class RoutingStrategies
    {
        public static Assignment? TryMultipleSolutionStrategies(RoutingModel routing, bool isLargeInstance)
        {
            Console.WriteLine("Thử các chiến lược giải khác nhau để tìm phương án tối ưu");
            
            // Store our strategies in order of preference
            var strategies = new List<(string Name, Func<RoutingSearchParameters> StrategyFunc)>
            {
                ("PathCheapestArc + Guided Local Search", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.PathCheapestArc,
                    LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch,
                    isLargeInstance)),
                    
                ("PathCheapestArc + Simulated Annealing", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.PathCheapestArc,
                    LocalSearchMetaheuristic.Types.Value.SimulatedAnnealing, 
                    isLargeInstance)),
                    
                ("Savings + Guided Local Search", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.Savings,
                    LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch,
                    isLargeInstance)),
                    
                ("Sweep + Tabu Search", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.Sweep,
                    LocalSearchMetaheuristic.Types.Value.TabuSearch,
                    isLargeInstance)),
                    
                // Additional strategy for very large instances
                ("Automatic + Guided Local Search", () => CreateSearchParameters(
                    FirstSolutionStrategy.Types.Value.Automatic,
                    LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch,
                    isLargeInstance)),
                    
                // Last resort with relaxed constraints and extended time
                ("Extended PathCheapestArc", () => CreateExtendedSearchParameters(isLargeInstance))
            };

            Assignment? solution = null;
            
            // Try each strategy in turn until we find a solution
            foreach (var (name, strategyFunc) in strategies)
            {
                var strategyStart = DateTime.Now;
                Console.WriteLine($"Thử chiến lược: {name}");
                
                var searchParameters = strategyFunc();
                solution = routing.SolveWithParameters(searchParameters);
                
                var strategyEnd = DateTime.Now;
                var duration = (strategyEnd - strategyStart).TotalMilliseconds;
                
                if (solution != null)
                {
                    Console.WriteLine($"Tìm thấy giải pháp với chiến lược '{name}' trong {duration} ms");
                    break;
                }
                else
                {
                    Console.WriteLine($"Chiến lược '{name}' không tìm thấy giải pháp sau {duration} ms");
                }
            }
            
            return solution;
        }

        private static RoutingSearchParameters CreateSearchParameters(
            FirstSolutionStrategy.Types.Value firstSolutionStrategy,
            LocalSearchMetaheuristic.Types.Value localSearchMetaheuristic,
            bool isLargeInstance)
        {
            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            
            // Set first solution strategy
            searchParameters.FirstSolutionStrategy = firstSolutionStrategy;
            
            // Set local search metaheuristic
            searchParameters.LocalSearchMetaheuristic = localSearchMetaheuristic;
            
            // Time limits
            if (isLargeInstance)
            {
                // For large instances, limit per-operation time but allow more overall time
                searchParameters.TimeLimit = new Duration { Seconds = 45 }; // Increased from 30s to 45s
                searchParameters.LnsTimeLimit = new Duration { Seconds = 10 }; // Increased from 5s to 10s
            }
            else
            {
                // For smaller instances, we can afford more search time
                searchParameters.TimeLimit = new Duration { Seconds = 10 };
                searchParameters.LnsTimeLimit = new Duration { Seconds = 2 };
            }
            
            return searchParameters;
        }

        private static RoutingSearchParameters CreateExtendedSearchParameters(bool isLargeInstance)
        {
            // This is our fallback with longer timeouts and more exhaustive search
            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            
            // Use most reliable first solution strategy
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
            
            // Use guided local search which is most effective for finding feasible solutions
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
            
            // Extended time limits based on instance size
            if (isLargeInstance)
            {
                searchParameters.TimeLimit = new Duration { Seconds = 90 }; // Increased from 60s to 90s for large instances
            }
            else
            {
                searchParameters.TimeLimit = new Duration { Seconds = 60 };
            }
            
            searchParameters.LnsTimeLimit = new Duration { Seconds = 15 }; // Increased from 10s to 15s
            
            // More exhaustive parameter settings
            searchParameters.LogSearch = true; // Enable search logging
            
            return searchParameters;
        }
    }
}
