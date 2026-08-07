using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // Repoint every QUST binding of a Papyrus script from one name to another, preserving
    // every property on every binding.
    //
    //   renamescript <modname> <oldname> <newname> [--dry]
    //   e.g. renamescript du_overtime dou_artifact_space_boardingrename_quest
    //                                 dou_artifact_space_boardingrename_qst --dry
    //
    // WHY THIS EXISTS. The Papyrus compiler shipped with the CK refuses a script name over
    // 38 characters. Five DU_Overtime scripts are over it -- they were built with a
    // third-party compiler that is no longer supported and does not know the newer script
    // objects -- so NONE of them can be rebuilt today. Any change to one is unshippable
    // until it is renamed. Proven rather than assumed: an untouched over-long script fails
    // the compiler identically, at (0,0), before a line of its body is read.
    //
    // THE PRESERVE HALF, and it is his ruling: the OLD .pex STAYS ON DISK. A player's save
    // carries the old script name on any quest instance already running, so deleting the
    // compiled script would break a mission in flight. Repointing the RECORDS means nothing
    // new binds the old name, while everything already bound still resolves. Existing works;
    // no more are created. This command does the records half only -- it never touches a
    // .pex, and the old one must be left where it is.
    //
    // WHAT IT WALKS, stated because a completeness check protects exactly the level it
    // enumerates: the quest's own VMAD script list, and each alias's script list. It does
    // NOT rewrite the fragment script (a different naming scheme, Fragments:Quests:QF_*) --
    // it reports one if it sees the old name there rather than silently leaving it. After
    // the write it re-reads the file's raw bytes and counts any surviving occurrence of the
    // old name, so an unwalked surface announces itself instead of shipping quietly.
    class gen_renamescript
    {
        // The compiler's own limit. A rename to something still over it is a no-op dressed
        // as a fix, so it is refused rather than written.
        const int PapyrusNameLimit = 38;

        public static int Generate(string[] args)
        {
            // args: [modname, "renamescript", oldname, newname, ("--dry")?]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: renamescript <modname> <oldname> <newname> [--dry]");
                return 1;
            }
            string modname = args[0];
            string oldName = args[2];
            string newName = args[3];
            bool dry = args.Skip(4).Any(a => string.Equals(a, "--dry", StringComparison.OrdinalIgnoreCase));

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Error: old and new names are the same -- nothing to do.");
                return 1;
            }
            if (newName.Length > PapyrusNameLimit)
            {
                Console.WriteLine($"Error: '{newName}' is {newName.Length} chars; the compiler's limit is {PapyrusNameLimit}.");
                Console.WriteLine("Renaming to another unbuildable name fixes nothing -- refused.");
                return 1;
            }

            StarfieldMod myMod;
            string datapath, modFile;
            int questsTouched = 0, bindingsRenamed = 0, aliasBindingsRenamed = 0;

            // env scoped to close before the write -- a same-path WriteToBinary inside the
            // using throws and leaves the old bytes looking like a persisted no-op.
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                modFile = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                // Collision check BEFORE anything is touched -- two scripts sharing a name is
                // not a rename, it is a merge, and it would silently fuse two property sets.
                bool collides = myMod.Quests.Any(q =>
                    (q.VirtualMachineAdapter?.Scripts?.Any(s => string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                    (q.VirtualMachineAdapter?.Aliases?.Any(a => a.Scripts?.Any(s => string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)) ?? false) ?? false));
                if (collides)
                {
                    Console.WriteLine($"Error: '{newName}' is already bound somewhere in {modname} -- refused (a rename onto a live name is a merge).");
                    return 1;
                }

                foreach (var quest in myMod.Quests)
                {
                    var vma = quest.VirtualMachineAdapter;
                    if (vma == null) continue;
                    bool touched = false;

                    if (vma.Scripts != null)
                        foreach (var sc in vma.Scripts)
                            if (string.Equals(sc.Name, oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                sc.Name = newName;
                                bindingsRenamed++; touched = true;
                            }

                    if (vma.Aliases != null)
                        foreach (var alias in vma.Aliases)
                            if (alias.Scripts != null)
                                foreach (var sc in alias.Scripts)
                                    if (string.Equals(sc.Name, oldName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        sc.Name = newName;
                                        aliasBindingsRenamed++; touched = true;
                                    }

                    // Reported, never rewritten -- see the header.
                    if (vma.Script != null && (vma.Script.Name?.IndexOf(oldName, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                        Console.WriteLine($"  ! {quest.EditorID}: FRAGMENT script names it ({vma.Script.Name}) -- NOT rewritten, yours to judge");

                    if (touched)
                    {
                        questsTouched++;
                        Console.WriteLine($"  {quest.EditorID}  [{quest.FormKey.ID:X6}]");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"'{oldName}' ({oldName.Length} chars) -> '{newName}' ({newName.Length} chars)");
            Console.WriteLine($"  quests touched:        {questsTouched}");
            Console.WriteLine($"  quest-script bindings: {bindingsRenamed}");
            Console.WriteLine($"  alias-script bindings: {aliasBindingsRenamed}");

            if (bindingsRenamed + aliasBindingsRenamed == 0)
            {
                Console.WriteLine($"\nNothing bound '{oldName}' -- nothing written.");
                return 1;
            }
            if (dry)
            {
                Console.WriteLine("\n--dry: nothing written.");
                return 0;
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());

            // Residual scan: the walk above covers the surfaces it enumerates and no more, so
            // ask the written bytes whether anything still names the old script.
            var written = System.IO.File.ReadAllBytes(modFile);
            var needle = System.Text.Encoding.ASCII.GetBytes(oldName);
            int residual = 0;
            for (int i = 0; i + needle.Length <= written.Length; i++)
            {
                bool hit = true;
                for (int j = 0; j < needle.Length; j++)
                    if (written[i + j] != needle[j]) { hit = false; break; }
                if (hit) { residual++; i += needle.Length - 1; }
            }
            Console.WriteLine($"\nResidual occurrences of '{oldName}' in the written plugin: {residual}");
            if (residual > 0)
                Console.WriteLine("  ^ a surface this command does not walk still names it -- find it before shipping.");
            else
                Console.WriteLine("  clean -- nothing in the plugin binds the old name any more.");

            Console.WriteLine($"\nFinished. LEAVE {oldName}.pex ON DISK -- saves with a running instance still need it.");
            Console.WriteLine($"Next: compile {newName}.psc and drop {newName}.pex beside it.");
            return 0;
        }
    }
}
