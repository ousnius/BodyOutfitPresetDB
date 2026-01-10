using Microsoft.Extensions.Configuration;
using BodyOutfitPresetDB.Programs;
using BodyOutfitPresetDB.Utilities;

var configurationBuilder = new ConfigurationBuilder();
var configuration = configurationBuilder.AddUserSecrets<Program>().Build();

Console.WriteLine("Select database to start:");
Console.WriteLine("1 = Outfit Mods");
Console.WriteLine("2 = BodySlide Presets");

int number = ConsoleUtil.ReadNumber();
if (number == 1)
{
    Console.WriteLine("Starting outfits console...");
    return OutfitsConsole.Run();
}
else if (number == 2)
{
    Console.WriteLine("Starting presets console...");
    return PresetsConsole.Run(configuration);
}

return 0; 
