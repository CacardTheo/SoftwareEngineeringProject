using System;

namespace SoftwareEngineeringProject
{
    class Program
    // Bootstrapping of the application
    {
        /// <summary>
        /// Entry point of the application.
        /// </summary>
        /// <param name="args">Command line arguments passed to the application.</param>
        static void Main(string[] args)
        {
            try
            {
                // Set console encoding to UTF8 to handle special characters if necessary
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                // Instantiate the View (Presentation Layer)
                ConsoleView view = new ConsoleView();

                // Run the application
                view.Run(args);
            }
            catch (Exception ex)
            {
                // Global error handling to prevent the console from closing abruptly
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"A critical error occurred: {ex.Message}");
                Console.ResetColor();
                
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}