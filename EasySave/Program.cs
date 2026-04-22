using System;
using SoftwareEngineeringProject;

namespace SoftwareEngineeringProject
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                ConsoleView view = new ConsoleView();
                view.Run(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"A critical error occurred: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}