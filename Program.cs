using FrankyCLI;

Console.WriteLine("FrankyCLI. Build new ship part.");

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
    case "cellfix":
        res = gen_cellfixer.Generate(args);
        break;
    default:
        Console.WriteLine("No mode provided, valid types are: (struct)");
        break;
}
return res;