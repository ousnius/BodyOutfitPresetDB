namespace BodyOutfitPresetDB.Utilities
{
    public static class ConsoleUtil
    {
        /// <summary>
        /// Read number (integer) from console line input.
        /// </summary>
        /// <returns>Number</returns>
        public static int ReadNumber()
        {
            bool validInput = false;
            int number;

            do
            {
                string? line = Console.ReadLine();
                if (int.TryParse(line, out number))
                    validInput = true;
                else
                    Console.WriteLine("Invalid number input.");
            } while (!validInput);

            return number;
        }

        /// <summary>
        /// Read yes/no confirmation from console line input.
        /// </summary>
        /// <param name="message">Message to show with WriteLine</param>
        /// <returns>Yes = true, No = false</returns>
        public static bool ReadYesNoConfirmation(string? message = null)
        {
            if (message != null)
                Console.WriteLine(message);

            bool validInput = false;
            bool yesno = false;

            do
            {
                string? line = Console.ReadLine();

                if (line != null && line.Length == 1)
                {
                    char ch = char.ToLowerInvariant(line[0]);
                    bool yes = ch == 'y';
                    bool no = ch == 'n';

                    if (yes)
                    {
                        yesno = true;
                        validInput = true;
                    }
                    else if (no)
                    {
                        yesno = false;
                        validInput = true;
                    }
                }

                if (!validInput)
                    Console.WriteLine("Invalid input.");
            } while (!validInput);

            return yesno;
        }
    }
}
