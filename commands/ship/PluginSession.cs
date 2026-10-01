using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    /// <summary>
    /// ONE load of the load order, ONE plugin opened for editing, ONE write. The prologue and
    /// epilogue every record command used to carry its own copy of.
    ///
    /// WHY IT EXISTS (2026-10-01, the A/B/C ladder's batch builder, his "A"): every command
    /// loads the whole load order (~28 s), edits, writes and exits, so a ladder rung that needs
    /// fifteen edits pays fifteen loads, and a failure between two of them leaves a half-built
    /// rung on disk. Splitting each command into a CORE that edits an already-loaded plugin and
    /// a thin wrapper around this session lets `batch` run many cores against one load and one
    /// write, while every command keeps exactly one copy of its rules.
    ///
    /// ⛔ THE ORDER IS THE CONTRACT: Open -> edit (the env, and its LinkCache, are live) ->
    /// Close (disposes the env, which holds the plugin file open) -> Write. A same-path write
    /// with the env still open throws AFTER the edits printed their success lines, and leaves
    /// the old bytes on disk looking like a persisted no-op. Write refuses while the env is open
    /// rather than relying on every caller to remember that.
    /// </summary>
    internal sealed class PluginSession : IDisposable
    {
        public StarfieldMod Mod { get; }
        public ILinkCache Cache => _env?.LinkCache
            ?? throw new InvalidOperationException("PluginSession: the LinkCache is gone once the session is closed");
        public ModKey SfKey { get; }
        public string ModName { get; }
        private readonly string _dataPath;
        private IGameEnvironment<IStarfieldMod, IStarfieldModGetter>? _env;

        private PluginSession(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, StarfieldMod mod,
                              string modname)
        {
            _env = env;
            Mod = mod;
            ModName = modname;
            _dataPath = env.DataFolderPath;
            SfKey = env.LoadOrder[0].ModKey;
        }

        /// <summary>Null, with the reason printed, when the plugin cannot be opened for editing.</summary>
        public static PluginSession? Open(string modname)
        {
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return null;
            }
            var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            if (!env.LoadOrder.ModExists(new ModKey(modname, ModType.Master)))
            {
                Console.WriteLine($"Error: {modname}.esm is not in the load order");
                env.Dispose();
                return null;
            }
            ModPath modPath = System.IO.Path.Combine(env.DataFolderPath, modname + ".esm");
            var mod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
            gen_quest_main.FixNextFormId(mod);
            return new PluginSession(env, mod, modname);
        }

        /// <summary>Release the load order (and the file handle on the plugin). Idempotent.</summary>
        public void Close()
        {
            _env?.Dispose();
            _env = null;
        }

        public void Dispose() => Close();

        /// <summary>The file Write() writes, for a caller that must read its own output back.</summary>
        public string PluginPath => _dataPath + "\\" + ModName + ".esm";

        public void Write()
        {
            if (_env != null)
                throw new InvalidOperationException("PluginSession.Write with the environment still open: Close() first, "
                                                    + "or the write fails after the edits have reported success");
            foreach (var rec in Mod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            Mod.WriteToBinary(PluginPath, gen_quest_main.BuildWriteParams());
        }
    }
}
