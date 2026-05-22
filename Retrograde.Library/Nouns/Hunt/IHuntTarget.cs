using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

namespace Retrograde.Interfaces
{
    public interface IHuntTarget
    {
        (IFormLink<IStarfieldMajorRecordGetter> targetList, Npc target, string huntName) GetHuntTarget(string planet);
    }
}
