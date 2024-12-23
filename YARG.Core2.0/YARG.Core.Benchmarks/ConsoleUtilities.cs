using System;

namespace YARG.Core.Benchmarks
{
    // These are definitely overkill lol, but they make things a lot neater
    public static class ConsoleUtilities
    {
        public static void WriteMenuHeader(string headerText, bool padTop = true)
        {
            if (padTop)
            {
                // Padding between previous section and new section
                Console.WriteLine();
            }

            // Write header
            string dashes = new('-', headerText.Length);
            Console.WriteLine(dashes);
            Console.WriteLine(headerText);
            Console.WriteLine(dashes);

            // Padding between header and contents
            Console.WriteLine();
        }

        public static ConsoleKey WaitForKey(string message = "Press any key to continue...")
        {
            Console.WriteLine(message);
            return Console.ReadKey(intercept: true).Key;
        }

        public static string PromptTextInput(string message, Func<string, string> validate)
        {
            Console.Write(message);
            while (true)
            {
                string input = Console.ReadLine();
                string error = validate(input);
                if (!string.IsNullOrEmpty(error))
                {
                    Console.Write($"{error} Please try again: ");
                    continue;
                }

                return input;
            }
        }

        public static int PromptIntegerInput(string message)
        {
            return PromptIntegerInput(message, int.MinValue, int.MaxValue);
        }

        public static int PromptIntegerInput(string message, int min, int max)
        {
            string response = PromptTextInput(message, (input) =>
            {
                if (!int.TryParse(input, null, out int selection) || selection < min || selection > max)
                    return "Invalid value.";

                return null;
            });
            return int.Parse(response);
        }

        public static bool PromptYesNo(string message)
        {
            string response = PromptTextInput($"{message} (y/n) ", (input) =>
            {
                if (string.IsNullOrWhiteSpace(input) ||
                    !input.StartsWith("y", StringComparison.CurrentCultureIgnoreCase) ||
                    !input.StartsWith("n", StringComparison.CurrentCultureIgnoreCase))
                    return "Invalid response.";

                return null;
            });

            return response.StartsWith("y", StringComparison.CurrentCultureIgnoreCase);
        }

        public static int PromptChoice(string title, params string[] args)
        {
            // Title
            Console.WriteLine(title);

            // Options
            for (int i = 0; i < args.Length; i++)
            {
                Console.WriteLine($"{i + 1}. " + args[i]);
            }

            return PromptIntegerInput("Selection: ", 1, args.Length) - 1;
        }
    }
}