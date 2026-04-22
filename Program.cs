using System;
using SoftwareEngineeringProject.Views;

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
                view.ShowMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"A critical error occurred: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}