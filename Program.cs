using System;
using System.Collections.Generic;
using System.Linq;

namespace SoftwareEngineeringProject;

class Program
{
    static void Main(string[] args)
    {
        // On initialise le moteur (Singleton)
        var engine = JobManager.GetInstance();

        // 1. Si aucun argument, on affiche l'aide ou on lance l'UI Console
        if (args.Length == 0)
        {
            Console.WriteLine("EasySave v1.0 - Use arguments: 1-3 or 1;3");
            return;
        }

        // 2. Parsing des arguments (ex: "1-3" ou "1;3")
        List<int> jobIndexesToRun = ParseArguments(args[0]);

        // 3. Exécution des jobs via le moteur
        if (jobIndexesToRun.Any())
        {
            Console.WriteLine($"Starting execution for jobs: {string.Join(", ", jobIndexesToRun)}...");
            engine.ExecuteJob(jobIndexesToRun[0]);
        }
        else
        {
            Console.WriteLine("No valid job index provided.");
        }
    }

    private static List<int> ParseArguments(string input)
    {
        var indexes = new List<int>();

        // Cas "1-3" (Plage)
        if (input.Contains('-'))
        {
            var parts = input.Split('-');
            if (int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
            {
                for (int i = start; i <= end; i++) indexes.Add(i);
            }
        }
        // Cas "1;3" (Liste)
        else if (input.Contains(';'))
        {
            var parts = input.Split(';');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int idx)) indexes.Add(idx);
            }
        }
        // Cas d'un seul index "1"
        else if (int.TryParse(input, out int idx))
        {
            indexes.Add(idx);
        }

        return indexes;
    }
}