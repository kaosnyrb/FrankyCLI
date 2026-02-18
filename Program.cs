using FrankyCLI;
using System.Windows.Markup;

Console.WriteLine("FrankyCLI");

//Process the args
for (int i = 0; i < args.Length; i++)
{
    Console.WriteLine(args[i]);
}

if (args.Length < 4)
{
    Console.WriteLine("Requires parameters:");
    Console.WriteLine("modname mode prefix itemname modelfilepath");

    return 1;
}

string modname = args[0];
string mode = args[1];
string prefix = args[2];
string item = args[3];
string modelpath = args[4];

if(modname == "Starfield")
{
    Console.WriteLine("No way am I allowing you to edit Starfield.esm");
    return 1;
}

int res = 0;
switch(mode)
{
    case "struct":
        res = gen_shipstruct.Generate(args);
        break;
    case "flip":
        res = gen_shipflips.Generate(args);
        break;
    case "yrotate":
        res = gen_shipyrotates.Generate(args);
        break;
    case "yrotate45":
        res = shipyfortyfiverotates.Generate(args);
        break;        
    case "cellfix":
        res = gen_cellfixer.Generate(args);
        break;
    case "placer":
        res = gen_placer.Generate(args);
        break;
    case "pluginmerger":
        res = gen_pluginmerger.Generate(args);
        break;
    case "upgradegenerator":
        res = gen_upgradegenerator.Generate(args);
        break;
    case "spaceencounterquest":
        res = gen_spaceencounterquest.Generate(args);
        break;
    case "branchcreator":
        res = gen_branchcreator.Generate(args);
        break;
    case "shipicons":
        res = gen_msicon.Generate(args);
        break;
    case "gen_quest":
        res = gen_quest_main.Generate(args);
        break;

    case "gen_retrograde":
        res = gen_retrograde.Generate(args);
        break;

    case "gen_harness":
        res = gen_harness.Generate(args);
        break;

    case "gen_report":
        res = gen_report.Generate(args);
        break;
    case "gen_worldspace":
        res = gen_worldspace.Generate(args);
        break;
    case "gen_btd_test":
        res = gen_btd_test.Generate(args);
        break;
    case "gen_btd_flatten":
        res = gen_btd_flatten.Generate(args);
        break;
    case "gen_btd_info":
        res = gen_btd_info.Generate(args);
        break;
    case "gen_btd_flatcircle":
        res = gen_btd_flatcircle.Generate(args);
        break;
    case "gen_inspect":
        res = gen_inspect.Generate(args);
        break;
    default:
        Console.WriteLine("No mode provided, valid types are: (struct)");
        break;
}
return res;