using EasySave.Localization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoftwareEngineeringProject;

class Program
{
    private static ILanguage _language;
    private static LanguageFactory _languageFactory = new LanguageFactory();
    static void Main(string[] args)
    {
        // Ici j'ai ajouté une sélection de langue au démarrage, mais on pourrait aussi la faire via un menu ou un fichier de config ( On verra plus tard )

        Console.WriteLine("Select language (en/fr): ");
        string lang = Console.ReadLine();
        _language = _languageFactory.CreateLanguage(lang);

        // On initialise le moteur (Singleton)
        var engine = JobManager.GetInstance();

        // 1. Si aucun argument, on affiche l'aide ou on lance l'UI Console
        if (args.Length == 0)
        {
            Console.WriteLine(_language.GetText(LanguageKeys.APP_HELP));
            return;
        }

        // 2. Parsing des arguments (ex: "1-3" ou "1;3")
        List<int> jobIndexesToRun = ParseArguments(args[0]);

        // 3. Exécution des jobs via le moteur
        if (jobIndexesToRun.Any())
        {
            Console.WriteLine(
                _language.GetText(LanguageKeys.START_EXECUTION)
                + string.Join(", ", jobIndexesToRun)
                );
            engine.ExecuteJob(jobIndexesToRun[0]);
        }
        else
        {
            Console.WriteLine(_language.GetText(LanguageKeys.NO_JOB));
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