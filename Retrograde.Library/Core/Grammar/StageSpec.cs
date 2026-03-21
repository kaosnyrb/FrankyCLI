using System.Collections.Generic;

namespace Retrograde.Grammar
{
    /// <summary>
    /// Per-stage specification computed by the GrammarEngine before any quest generation begins.
    /// Carries the grammar's structural decisions for one stage of the quest chain.
    /// </summary>
    public class StageSpec
    {
        public StageRole Role;
        public float Tension;
        public string Mood;
        public string RoleDescription;

        /// <summary>
        /// Characters from the web assigned to this stage.
        /// </summary>
        public List<WebCharacter> Characters = new List<WebCharacter>();

        /// <summary>
        /// Pre-formatted entity manifest XML string for this stage.
        /// Built by GrammarEngine after character and location assignment.
        /// </summary>
        public string EntityManifest = "";
    }
}
