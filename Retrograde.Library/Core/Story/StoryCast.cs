using Retrograde.Nouns;
using System.Text;

namespace Retrograde.Story
{
    /// <summary>
    /// A character in the story, identified by their narrative role.
    /// </summary>
    public class StoryRole
    {
        public string RoleId;           // "target", "informant_1", "employer"
        public string RoleType;         // from schema: "hostile_npc", "friendly_npc", "implicit"
        public CharacterTraits Traits;
        public INoun? NounInstance;      // the generated Noun (OutlawNpc, etc.)
        public string Name = "";
        public bool IsFemale;
    }

    /// <summary>
    /// The full cast of a story — all characters, accessible by role.
    /// Built before quest content is generated so every beat can
    /// reference any cast member by role.
    /// </summary>
    public class StoryCast
    {
        public Dictionary<string, StoryRole> Roles { get; } = new();

        public StoryRole GetRole(string roleId) => Roles[roleId];
        public bool HasRole(string roleId) => Roles.ContainsKey(roleId);

        /// <summary>
        /// Create a StoryCast from an existing OutlawNpc (backward compatibility).
        /// </summary>
        public static StoryCast FromOutlawNpc(OutlawNpc npc)
        {
            var cast = new StoryCast();
            cast.Roles["target"] = new StoryRole
            {
                RoleId = "target",
                RoleType = "hostile_npc",
                Traits = npc.Traits,
                NounInstance = npc,
                Name = npc.name,
                IsFemale = npc.female,
            };
            return cast;
        }

        /// <summary>
        /// Inject all cast members into an AI prompt for context.
        /// </summary>
        public void AppendToPrompt(StringBuilder sb)
        {
            sb.AppendLine("## Story Characters");
            foreach (var (id, role) in Roles)
            {
                if (role.RoleType == "implicit") continue;
                sb.AppendLine($"\n### {role.Name} ({id})");
                role.Traits.AppendToPrompt(sb);
            }
        }
    }
}
