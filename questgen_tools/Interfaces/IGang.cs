using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public interface IGang
    {
        public Mutagen.Bethesda.Starfield.FormList gangList { get; set; }
        public string gangName { get; set; }
        public Mutagen.Bethesda.Starfield.FormList GenerateGang();
    }
}
