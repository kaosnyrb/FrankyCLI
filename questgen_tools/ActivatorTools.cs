using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class ActivatorTools
    {
        public static string GetWallModel()
        {
            Random random = new Random();

            List<string> wallmodel = new List<string>()
            {
                "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif",

            };

            return wallmodel[random.Next(wallmodel.Count)];
        }

    }
}
